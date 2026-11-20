using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>
/// Keeps the default model (US1.9, ADR 0007, section 5): its client follows the settings, the model is loaded at
/// start and again after a change, and <see cref="EnsureLoadedAsync"/> reloads it before a turn when Ollama freed
/// it. Each change of loading is announced with <see cref="AssistantModelLoadChanged"/>.
/// </summary>
public sealed partial class AssistantModelKeeper : BackgroundService, IAssistantModel
{
    /// <summary>
    /// Delays between two attempts when the preload fails, for instance while Docker starts Ollama after a
    /// Windows logon. After the last one Jarvis waits for a turn or a change of settings.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
        [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];

    private readonly ILlmConnector connector;
    private readonly IMessageBus bus;
    private readonly TimeProvider time;
    private readonly ILogger<AssistantModelKeeper> logger;
    private readonly IDisposable watch;
    private readonly SemaphoreSlim loading = new(1, 1);
    private readonly SemaphoreSlim changed = new(0, 1);
    private readonly Lock gate = new();
    private Current current;
    private ModelLoadState state;

    /// <summary>Creates the keeper and reads the settings.</summary>
    /// <param name="settings">The settings, for <see cref="LlmProvidersOptions.Section"/>.</param>
    /// <param name="connector">Reaches the providers.</param>
    /// <param name="bus">Where the loading is announced.</param>
    /// <param name="time">The clock of the retries.</param>
    /// <param name="logger">Logs the loads and their failures.</param>
    public AssistantModelKeeper(ISettingsStore settings, ILlmConnector connector, IMessageBus bus, TimeProvider time, ILogger<AssistantModelKeeper> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.connector = connector;
        this.bus = bus;
        this.time = time;
        this.logger = logger;

        // Watch before reading: a save between the two is not missed.
        watch = settings.Watch(LlmProvidersOptions.Section, Apply);
        current = Build(settings.Read(LlmProvidersOptions.Section).Default?.ToProvider());
        state = current.Provider is null ? ModelLoadState.NotConfigured : ModelLoadState.Unknown;
    }

    /// <inheritdoc />
    public LlmProvider? Provider => Volatile.Read(ref current).Provider;

    /// <inheritdoc />
    public IChatClient? Client => Volatile.Read(ref current).Client;

    /// <inheritdoc />
    public ModelLoadState LoadState
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    /// <inheritdoc />
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        var provider = Provider ?? throw new LlmException(LlmFailure.NotConfigured, "No default provider is configured.");
        if (await connector.IsLoadedAsync(provider, cancellationToken).ConfigureAwait(false) == false)
        {
            await LoadAsync(provider, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        watch.Dispose();
        Volatile.Read(ref current).Client?.Dispose();
        loading.Dispose();
        changed.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Loads the default model at start, again after each change of settings, retrying a few times when it fails.</summary>
    /// <param name="stoppingToken">Stops with Jarvis.</param>
    /// <returns>The background task.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PreloadWithRetriesAsync(stoppingToken).ConfigureAwait(false);
                await changed.WaitAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PreloadWithRetriesAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (Provider is not { Tuning.KeepLoaded: true } provider || !provider.Kind.IsLocal())
            {
                return;
            }

            try
            {
                await LoadAsync(provider, stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (LlmException) when (attempt < RetryDelays.Count)
            {
                // Logged and announced by LoadAsync. A change of settings ends the wait early.
                await WaitForChangeAsync(RetryDelays[attempt], stoppingToken).ConfigureAwait(false);
            }
            catch (LlmException)
            {
                return;
            }
        }
    }

    /// <summary>Waits for <paramref name="delay"/>, or less when the settings change (the signal is left for the main loop).</summary>
    private async Task WaitForChangeAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var delayTask = Task.Delay(delay, time, timeout.Token);
        var changeTask = changed.WaitAsync(timeout.Token);
        var first = await Task.WhenAny(delayTask, changeTask).ConfigureAwait(false);
        await timeout.CancelAsync().ConfigureAwait(false);
        if (first == changeTask && changeTask.IsCompletedSuccessfully)
        {
            // Give the signal back: the main loop restarts the preload with the new provider.
            changed.Release();
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    private async Task LoadAsync(LlmProvider provider, CancellationToken cancellationToken)
    {
        await loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have loaded it while this one waited.
            if (LoadState == ModelLoadState.Loaded && ReferenceEquals(provider, Provider)
                && await connector.IsLoadedAsync(provider, cancellationToken).ConfigureAwait(false) != false)
            {
                return;
            }

            await AnnounceAsync(provider, ModelLoadState.Loading, null).ConfigureAwait(false);
            var started = time.GetTimestamp();
            try
            {
                await connector.PreloadAsync(provider, cancellationToken).ConfigureAwait(false);
            }
            catch (LlmException exception)
            {
                LogLoadFailed(provider.Id, provider.Model, exception.Failure, exception.Message);
                await AnnounceAsync(provider, ModelLoadState.Failed, exception.Failure).ConfigureAwait(false);
                throw;
            }
            catch (OperationCanceledException)
            {
                await AnnounceAsync(provider, ModelLoadState.Unknown, null).ConfigureAwait(false);
                throw;
            }

            LogLoaded(provider.Id, provider.Model, (long)time.GetElapsedTime(started).TotalMilliseconds);

            // Only Ollama says whether a model is in memory: for the others the load state stays unknown.
            var loaded = provider.Kind == LlmProviderKind.Ollama ? ModelLoadState.Loaded : ModelLoadState.Unknown;
            await AnnounceAsync(provider, loaded, null).ConfigureAwait(false);
        }
        finally
        {
            loading.Release();
        }
    }

    private async Task AnnounceAsync(LlmProvider provider, ModelLoadState next, LlmFailure? failure)
    {
        lock (gate)
        {
            // A provider replaced meanwhile keeps the state the change gave it.
            if (!ReferenceEquals(provider, Volatile.Read(ref current).Provider))
            {
                return;
            }

            state = next;
        }

        try
        {
            await bus.PublishAsync(new AssistantModelLoadChanged(provider.Name, provider.Model, next, failure)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPublishFailed(exception);
        }
    }

    private void Apply(LlmProvidersOptions options)
    {
        var next = options.Default?.ToProvider();
        if (next == Provider)
        {
            return;
        }

        Current previous;
        lock (gate)
        {
            previous = Interlocked.Exchange(ref current, Build(next));
            state = next is null ? ModelLoadState.NotConfigured : ModelLoadState.Unknown;
        }

        previous.Client?.Dispose();
        LogChanged(next?.Id, next?.Model);

        // Wake the background loop, once: a signal already waiting is enough.
        try
        {
            if (changed.CurrentCount == 0)
            {
                changed.Release();
            }
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private Current Build(LlmProvider? provider) => new(provider, provider is null ? null : connector.CreateClient(provider));

    [LoggerMessage(Level = LogLevel.Information, Message = "Default model {Model} of provider {Provider} loaded in {Milliseconds} ms")]
    private partial void LogLoaded(string provider, string model, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Default model {Model} of provider {Provider} could not be loaded: {Failure}, {Detail}")]
    private partial void LogLoadFailed(string provider, string model, LlmFailure failure, string detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Default model is now {Model} of provider {Provider}")]
    private partial void LogChanged(string? provider, string? model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The change of loading of the model could not be announced")]
    private partial void LogPublishFailed(Exception exception);

    private sealed record Current(LlmProvider? Provider, IChatClient? Client);
}
