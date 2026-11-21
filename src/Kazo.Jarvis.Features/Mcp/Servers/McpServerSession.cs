using System.Text.Json;
using Kazo.Jarvis.Kernel.Tools.Servers;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>
/// The connection to one tool server and its life (ADR 0018, section 2). A server that cannot be reached is not a
/// fault of the assistant: its tools leave the list given to the model and the connection is tried again, after 5
/// seconds then twice as long each time, up to 5 minutes. A server started by command that stops keeps its tools
/// and is started again at the next call, three times an hour at most.
/// </summary>
internal sealed class McpServerSession : IAsyncDisposable
{
    /// <summary>The wait before the second attempt.</summary>
    public static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(5);

    /// <summary>The longest wait between two attempts.</summary>
    public static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(5);

    /// <summary>How many times an hour a stopped command is started again.</summary>
    public const int RestartsAnHour = 3;

    private readonly McpServerHub hub;
    private readonly IToolServerConnector connector;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private readonly List<DateTimeOffset> restarts = [];
    private IToolServerConnection? connection;
    private TimeSpan retry = FirstRetry;
    private int generation;

    public McpServerSession(McpServerHub hub, McpServerOptions options, IToolServerConnector connector, TimeProvider time)
    {
        this.hub = hub;
        this.connector = connector;
        this.time = time;
        Options = options;
    }

    /// <summary>The name of the server in the settings.</summary>
    public string Name => Options.Name;

    /// <summary>The server as it was when the session started. A change of how it is reached starts a new session.</summary>
    public McpServerOptions Options { get; }

    /// <summary>Where the connection is.</summary>
    public McpServerState State { get; private set; } = McpServerState.Connecting;

    /// <summary>Why the last attempt failed.</summary>
    public ToolServerFailure? Failure { get; private set; }

    /// <summary>The name the server gives itself.</summary>
    public string? AnnouncedName { get; private set; }

    /// <summary>Starts the first attempt, in the background.</summary>
    public void Start() => _ = ConnectInBackgroundAsync();

    /// <summary>Tries now, whatever the wait under way: the « Réessayer » button.</summary>
    /// <returns>When the attempt ended.</returns>
    public Task RetryNowAsync()
    {
        retry = FirstRetry;
        return ConnectInBackgroundAsync();
    }

    /// <summary>
    /// Calls a tool (ADR 0018, section 2). On a network error, one more attempt, without a new session for a server
    /// reached by address; a stopped command is started again first. A second failure is thrown to the tool, which
    /// returns it to the model.
    /// </summary>
    public async ValueTask<ToolServerResult> CallAsync(string tool, JsonElement arguments, CancellationToken cancellationToken)
    {
        ToolServerException? last = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var current = connection;
            if (current is null)
            {
                if (!Options.ByCommand)
                {
                    throw last ?? new ToolServerException(Failure ?? ToolServerFailure.Unreachable);
                }

                current = await RestartForCallAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await current.CallAsync(tool, arguments, cancellationToken).ConfigureAwait(false);
            }
            catch (ToolServerException exception) when (exception.Failure is ToolServerFailure.Unreachable or ToolServerFailure.Closed)
            {
                last = exception;
                if (Options.ByCommand)
                {
                    await LoseAsync(current, exception.Failure).ConfigureAwait(false);
                }
            }
        }

        if (!Options.ByCommand && connection is { } failed)
        {
            // Two failures in a row: the server is gone. Its tools leave the list and the attempts start again.
            await LoseAsync(failed, last!.Failure).ConfigureAwait(false);
        }

        throw last!;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref generation);
        await stop.CancelAsync().ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CloseAsync().ConfigureAwait(false);
            State = McpServerState.Disabled;
        }
        finally
        {
            gate.Release();
        }

        await hub.ForgetToolsAsync(this).ConfigureAwait(false);
    }

    private async Task ConnectInBackgroundAsync()
    {
        try
        {
            await ConnectAsync(stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ToolServerException)
        {
            // Already in the state; a server reached by address is tried again later.
        }
    }

    /// <summary>Connects, reads the tools and gives them to the hub. Throws the failure after writing it in the state.</summary>
    private async Task<IToolServerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var mine = Interlocked.Increment(ref generation);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseAsync().ConfigureAwait(false);
            Set(McpServerState.Connecting, null);
            try
            {
                var endpoint = await hub.EndpointOfAsync(Options, cancellationToken).ConfigureAwait(false);
                var opened = await connector.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                try
                {
                    var tools = await opened.ListToolsAsync(cancellationToken).ConfigureAwait(false);
                    await hub.SyncToolsAsync(this, tools, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await opened.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                connection = opened;
                AnnouncedName = opened.AnnouncedName;
                retry = FirstRetry;
                Set(McpServerState.Connected, null);
                _ = opened.Closed.ContinueWith(
                    _ => LoseAsync(opened, ToolServerFailure.Closed),
                    CancellationToken.None,
                    TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Default);
                return opened;
            }
            catch (ToolServerException exception)
            {
                await FailAsync(exception.Failure, mine).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IToolServerConnection> RestartForCallAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        lock (restarts)
        {
            restarts.RemoveAll(moment => now - moment >= TimeSpan.FromHours(1));
            if (restarts.Count >= RestartsAnHour)
            {
                throw new ToolServerException(ToolServerFailure.Closed);
            }

            restarts.Add(now);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stop.Token);
        return await ConnectAsync(linked.Token).ConfigureAwait(false);
    }

    /// <summary>The connection <paramref name="lost"/> ended or stopped answering.</summary>
    private async Task LoseAsync(IToolServerConnection lost, ToolServerFailure failure)
    {
        if (stop.IsCancellationRequested)
        {
            return;
        }

        var mine = Volatile.Read(ref generation);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(connection, lost))
            {
                return;
            }

            await CloseAsync().ConfigureAwait(false);
            if (Options.ByCommand)
            {
                // Its tools stay: the next call starts the program again.
                Set(McpServerState.Stopped, failure);
                return;
            }

            await FailAsync(failure, mine).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Writes a failure in the state. For a server reached by address: removes its tools and plans the next attempt.</summary>
    private async Task FailAsync(ToolServerFailure failure, int mine)
    {
        if (Options.ByCommand)
        {
            Set(State == McpServerState.Stopped ? McpServerState.Stopped : McpServerState.Failed, failure);
            return;
        }

        await hub.ForgetToolsAsync(this).ConfigureAwait(false);
        Set(McpServerState.Failed, failure);
        var wait = retry;
        retry = retry + retry > LongestRetry ? LongestRetry : retry + retry;
        _ = RetryLaterAsync(wait, mine);
    }

    private async Task RetryLaterAsync(TimeSpan wait, int mine)
    {
        try
        {
            await Task.Delay(wait, time, stop.Token).ConfigureAwait(false);
            if (Volatile.Read(ref generation) == mine)
            {
                await ConnectInBackgroundAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CloseAsync()
    {
        var current = connection;
        connection = null;
        if (current is not null)
        {
            try
            {
                await current.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Closing a dead connection may throw anything; there is nothing left to do with it.
            }
        }
    }

    private void Set(McpServerState state, ToolServerFailure? failure)
    {
        State = state;
        Failure = failure;
        hub.Notify();
    }
}
