using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Tools.Servers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>
/// The tool servers of the user (ADR 0018, sections 1 to 4): connects those that are on, in the background, puts
/// their tools in the registry, and keeps the approval of each tool tied to the fingerprint of what the tool says
/// it does. Nothing here is a fault of the assistant: a server that cannot be reached only loses its tools.
/// </summary>
public sealed partial class McpServerHub : IHostedService, IAsyncDisposable
{
    private readonly ISettingsStore settings;
    private readonly IToolRegistry registry;
    private readonly ISecretVault? vault;
    private readonly IToolServerConnector? connector;
    private readonly TimeProvider time;
    private readonly ILogger logger;
    private readonly SemaphoreSlim sync = new(1, 1);
    private readonly Lock gate = new();
    private readonly Dictionary<string, McpServerSession> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Registered>> tools = new(StringComparer.Ordinal);
    private IDisposable? serversWatch;
    private IDisposable? activationWatch;
    private bool started;

    /// <summary>Creates the hub.</summary>
    /// <param name="settings">The settings: <see cref="McpServersOptions.Section"/> and <see cref="ToolActivation.Section"/>.</param>
    /// <param name="registry">The registry the tools of the servers enter.</param>
    /// <param name="vault">The vault that holds the values of the headers; <see langword="null"/> on a host without one.</param>
    /// <param name="connector">What speaks to a server; <see langword="null"/> on a host that has none.</param>
    /// <param name="time">The clock of the waits between two attempts.</param>
    /// <param name="logger">The technical log. It never receives a header value nor the arguments of a call.</param>
    public McpServerHub(
        ISettingsStore settings,
        IToolRegistry registry,
        ISecretVault? vault = null,
        IToolServerConnector? connector = null,
        TimeProvider? time = null,
        ILogger<McpServerHub>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(registry);
        this.settings = settings;
        this.registry = registry;
        this.vault = vault;
        this.connector = connector;
        this.time = time ?? TimeProvider.System;
        this.logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <summary>Raised when the state of a server or its tools changed; on any thread.</summary>
    public event Action? Changed;

    /// <summary>Whether this host can speak to a tool server.</summary>
    public bool Available => connector is not null;

    /// <summary>Every server of the settings with where its connection is and its tools.</summary>
    public IReadOnlyList<McpServerStatus> Statuses
    {
        get
        {
            var options = settings.Read(McpServersOptions.Section);
            var activation = settings.Read(ToolActivation.Section).Switches;
            var statuses = new List<McpServerStatus>();
            foreach (var server in options.Servers ?? [])
            {
                McpServerSession? session;
                List<Registered> registered;
                lock (gate)
                {
                    sessions.TryGetValue(server.Name, out session);
                    registered = tools.TryGetValue(server.Name, out var found) ? [.. found] : [];
                }

                var approved = server.Approved ?? new Dictionary<string, string>();
                statuses.Add(new McpServerStatus(
                    server,
                    session?.State ?? McpServerState.Disabled,
                    session?.Failure,
                    session?.AnnouncedName,
                    [.. registered.Select(entry => new McpToolStatus(
                        entry.Tool.ServerTool.Name,
                        entry.Tool.Name,
                        entry.Tool.ServerTool.Description,
                        entry.Tool.ServerClass,
                        entry.Tool.DeclaredClass,
                        activation is not null && activation.GetValueOrDefault(entry.Tool.Name),
                        approved.TryGetValue(entry.Tool.ServerTool.Name, out var fingerprint) && !string.Equals(fingerprint, entry.Tool.Fingerprint, StringComparison.Ordinal),
                        EstimateTokens(entry.Tool.ServerTool)))]));
            }

            return statuses;
        }
    }

    /// <summary>A rough size of a tool in the context of the model: its three parts and the JSON around them, four characters a token.</summary>
    /// <param name="tool">The tool, as its server describes it.</param>
    /// <returns>The estimate, in tokens.</returns>
    public static int EstimateTokens(ToolServerTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var schema = tool.Schema.ValueKind == System.Text.Json.JsonValueKind.Undefined ? 0 : tool.Schema.GetRawText().Length;
        return (tool.Name.Length + tool.Description.Length + schema + 20 + 3) / 4;
    }

    /// <summary>Whether an address is on this computer or on the local network, to say so on the page (ADR 0018, section 6).</summary>
    /// <param name="address">The address of a server.</param>
    /// <returns><see langword="true"/> for a loopback, private or link-local address, <c>localhost</c>, a name without a dot or a <c>.local</c> name.</returns>
    public static bool IsLocal(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var host = address.IdnHost.TrimEnd('.');
        if (System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            if (System.Net.IPAddress.IsLoopback(ip))
            {
                return true;
            }

            var bytes = ip.GetAddressBytes();
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                ? bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254)
                : ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC;
        }

        return !host.Contains('.', StringComparison.Ordinal)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        started = true;
        serversWatch = settings.Watch(McpServersOptions.Section, changed => { _ = ReconcileAsync(); });
        activationWatch = settings.Watch(ToolActivation.Section, changed => { _ = RecordApprovalsAsync(); });

        // In the background: a server that does not answer must not hold the start of Jarvis.
        _ = ReconcileAsync();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        started = false;
        serversWatch?.Dispose();
        activationWatch?.Dispose();
        serversWatch = null;
        activationWatch = null;
        List<McpServerSession> all;
        lock (gate)
        {
            all = [.. sessions.Values];
            sessions.Clear();
        }

        // Closing a session stops the program of a server started by command (ADR 0018, section 1: closed at « Quitter »).
        foreach (var session in all)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    /// <summary>Adds a server, off or on as <paramref name="server"/> says, and stores the values of its headers in the vault.</summary>
    /// <param name="server">The new server. Its name must be free.</param>
    /// <param name="headerValues">The value of each header of the server that has one, by header name.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The outcome of the writing of the settings.</returns>
    public async Task<SettingsWriteResult> AddAsync(McpServerOptions server, IReadOnlyDictionary<string, string>? headerValues = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        var options = settings.Read(McpServersOptions.Section);
        if (options.Find(server.Name) is not null)
        {
            return new SettingsWriteResult(SettingsWriteOutcome.Invalid, [$"Two tool servers are named '{server.Name}'."]);
        }

        var changed = options.With(server);
        var problems = McpServersOptions.Section.Validate(changed);
        if (problems.Count > 0)
        {
            // Before the vault: a refused server must leave no secret behind.
            return new SettingsWriteResult(SettingsWriteOutcome.Invalid, problems);
        }

        if (vault is not null && headerValues is not null)
        {
            foreach (var header in server.Headers.Where(header => headerValues.TryGetValue(header, out var value) && !string.IsNullOrEmpty(value)))
            {
                await vault.SetAsync(McpServersOptions.HeaderSecret(server.Name, header), new SecretValue(headerValues[header]), cancellationToken).ConfigureAwait(false);
            }
        }

        return await settings.WriteAsync(McpServersOptions.Section, changed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a server, the values of its headers and the switches of its tools.</summary>
    /// <param name="name">The name of the server.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The outcome of the writing of the settings.</returns>
    public async Task<SettingsWriteResult> RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        var options = settings.Read(McpServersOptions.Section);
        if (options.Find(name) is not { } server)
        {
            return SettingsWriteResult.Saved;
        }

        List<string> names;
        lock (gate)
        {
            names = tools.TryGetValue(name, out var found) ? [.. found.Select(entry => entry.Tool.Name)] : [];
        }

        var result = await settings.WriteAsync(McpServersOptions.Section, options.Without(name), cancellationToken).ConfigureAwait(false);
        if (result.Outcome != SettingsWriteOutcome.Saved)
        {
            return result;
        }

        if (vault is not null)
        {
            foreach (var header in server.Headers ?? [])
            {
                await vault.DeleteAsync(McpServersOptions.HeaderSecret(name, header), cancellationToken).ConfigureAwait(false);
            }
        }

        // A later server with the same name must not inherit a switch that is on.
        var activation = settings.Read(ToolActivation.Section);
        var on = names.Where(tool => activation.Switches?.GetValueOrDefault(tool) == true).ToList();
        if (on.Count > 0)
        {
            foreach (var tool in on)
            {
                activation = activation.With(tool, false);
            }

            await settings.WriteAsync(ToolActivation.Section, activation, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Changes one server of the settings.</summary>
    /// <param name="name">The name of the server.</param>
    /// <param name="change">Gives the new value from the current one.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The outcome of the writing of the settings.</returns>
    public async Task<SettingsWriteResult> ChangeAsync(string name, Func<McpServerOptions, McpServerOptions> change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        var options = settings.Read(McpServersOptions.Section);
        return options.Find(name) is { } server
            ? await settings.WriteAsync(McpServersOptions.Section, options.With(change(server)), cancellationToken).ConfigureAwait(false)
            : SettingsWriteResult.Saved;
    }

    /// <summary>
    /// Raises the class of a tool, or gives it back the class of its server (ADR 0018, section 4). A class is never
    /// lowered under what the server declares: the declaration of a server is not a proof, the choice of the user
    /// can only be stricter.
    /// </summary>
    /// <param name="name">The name of the server.</param>
    /// <param name="tool">The name of the tool on the server.</param>
    /// <param name="raised">« Sensible » or « Critique »; anything else removes the choice.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The outcome of the writing of the settings.</returns>
    public Task<SettingsWriteResult> RaiseClassAsync(string name, string tool, ActionClass? raised, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            name,
            server =>
            {
                var classes = new Dictionary<string, ActionClass>(server.Classes ?? new Dictionary<string, ActionClass>(), StringComparer.Ordinal);
                if (raised is ActionClass.Sensitive or ActionClass.Critical)
                {
                    classes[tool] = raised.Value;
                }
                else
                {
                    classes.Remove(tool);
                }

                return server with { Classes = classes };
            },
            cancellationToken);

    /// <summary>« Tester la connexion » (ADR 0018, section 2): a connection of its own, the list of the tools, then closed.</summary>
    /// <param name="name">The name of a server of the settings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>The named cause of a failure, or how many tools the server offers.</returns>
    public async Task<McpConnectionTest> TestAsync(string name, CancellationToken cancellationToken = default)
    {
        if (connector is null || settings.Read(McpServersOptions.Section).Find(name) is not { } server)
        {
            return new McpConnectionTest(ToolServerFailure.Unreachable, 0, null);
        }

        try
        {
            var endpoint = await EndpointOfAsync(server, cancellationToken).ConfigureAwait(false);
            var connection = await connector.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            await using (connection.ConfigureAwait(false))
            {
                var found = await connection.ListToolsAsync(cancellationToken).ConfigureAwait(false);
                return new McpConnectionTest(null, found.Count, connection.AnnouncedName);
            }
        }
        catch (ToolServerException exception)
        {
            LogTestFailed(logger, name, exception.Failure);
            return new McpConnectionTest(exception.Failure, 0, null);
        }
    }

    /// <summary>Tries to connect a server now: after a change of a header value, or without waiting for the next attempt.</summary>
    /// <param name="name">The name of the server.</param>
    /// <returns>When the attempt ended.</returns>
    public Task RetryAsync(string name)
    {
        McpServerSession? session;
        lock (gate)
        {
            sessions.TryGetValue(name, out session);
        }

        return session?.RetryNowAsync() ?? Task.CompletedTask;
    }

    /// <summary>The endpoint of a server, with the values of its headers read from the vault.</summary>
    internal async ValueTask<ToolServerEndpoint> EndpointOfAsync(McpServerOptions server, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (vault is not null && !server.ByCommand)
        {
            foreach (var header in server.Headers ?? [])
            {
                if (await vault.GetAsync(McpServersOptions.HeaderSecret(server.Name, header), cancellationToken).ConfigureAwait(false) is { } value)
                {
                    headers[header] = value.Reveal();
                }
            }
        }

        return new ToolServerEndpoint
        {
            Name = server.Name,
            Address = server.ByCommand ? null : server.Uri,
            Command = server.ByCommand ? server.Command!.Trim() : null,
            Arguments = server.Arguments ?? [],
            Headers = headers,
        };
    }

    /// <summary>
    /// Puts the tools a server offers now in the registry (ADR 0018, section 3). A tool that is on with another
    /// fingerprint than the one the user approved is turned off before it enters the registry.
    /// </summary>
    internal async Task SyncToolsAsync(McpServerSession session, IReadOnlyList<ToolServerTool> offered, CancellationToken cancellationToken)
    {
        await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Unregister(session.Name);
            var approved = settings.Read(McpServersOptions.Section).Find(session.Name)?.Approved ?? new Dictionary<string, string>();
            var activation = settings.Read(ToolActivation.Section);
            var turnedOff = false;
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var found = new List<McpTool>();
            foreach (var offer in offered.Where(offer => !string.IsNullOrWhiteSpace(offer.Name)).OrderBy(offer => offer.Name, StringComparer.Ordinal))
            {
                if (NameFor(session.Name, offer.Name, taken) is not { } name)
                {
                    LogToolSkipped(logger, session.Name, offer.Name);
                    continue;
                }

                taken.Add(name);
                var tool = new McpTool(session, settings, offer, name);
                var isOn = activation.Switches?.GetValueOrDefault(name) == true;
                if (isOn && !(approved.TryGetValue(offer.Name, out var fingerprint) && string.Equals(fingerprint, tool.Fingerprint, StringComparison.Ordinal)))
                {
                    activation = activation.With(name, false);
                    turnedOff = true;
                    LogToolTurnedOff(logger, session.Name, offer.Name);
                }

                found.Add(tool);
            }

            if (turnedOff)
            {
                await settings.WriteAsync(ToolActivation.Section, activation, cancellationToken).ConfigureAwait(false);
            }

            var registered = new List<Registered>();
            foreach (var tool in found)
            {
                try
                {
                    registered.Add(new Registered(tool, registry.Register(tool)));
                }
                catch (ArgumentException)
                {
                    // Taken between the check and now by another server: left out until the next connection.
                    LogToolSkipped(logger, session.Name, tool.ServerTool.Name);
                }
            }

            lock (gate)
            {
                tools[session.Name] = registered;
            }
        }
        finally
        {
            sync.Release();
        }

        Notify();
    }

    /// <summary>Removes the tools of a server from the registry.</summary>
    internal async Task ForgetToolsAsync(McpServerSession session)
    {
        await sync.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (gate)
            {
                // A newer session of the same server may already own the tools.
                if (sessions.TryGetValue(session.Name, out var current) && !ReferenceEquals(current, session))
                {
                    return;
                }
            }

            Unregister(session.Name);
        }
        finally
        {
            sync.Release();
        }

        Notify();
    }

    /// <summary>Tells the pages that something changed.</summary>
    internal void Notify() => Changed?.Invoke();

    /// <summary>Starts, stops and replaces the sessions so that they match the settings.</summary>
    internal async Task ReconcileAsync()
    {
        if (!started || connector is null)
        {
            Notify();
            return;
        }

        var wanted = (settings.Read(McpServersOptions.Section).Servers ?? []).Where(server => server.Enabled).ToDictionary(server => server.Name, StringComparer.Ordinal);
        var ended = new List<McpServerSession>();
        var begun = new List<McpServerSession>();
        lock (gate)
        {
            foreach (var (name, session) in sessions.ToList())
            {
                if (!wanted.TryGetValue(name, out var server) || !server.SameConnection(session.Options) || server.Kind != session.Options.Kind)
                {
                    sessions.Remove(name);
                    ended.Add(session);
                }
            }

            foreach (var server in wanted.Values.Where(server => !sessions.ContainsKey(server.Name)))
            {
                var session = new McpServerSession(this, server, connector, time);
                sessions[server.Name] = session;
                begun.Add(session);
            }
        }

        // Outside every lock: a session that connects holds its own lock while it gives its tools to the hub.
        foreach (var session in ended)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var session in begun)
        {
            session.Start();
        }

        Notify();
    }

    /// <summary>
    /// The user turned a tool on, here or on the tools page: the fingerprint of what the tool says now is kept as
    /// what the user approved.
    /// </summary>
    internal async Task RecordApprovalsAsync()
    {
        try
        {
            await sync.WaitAsync().ConfigureAwait(false);
            try
            {
                var activation = settings.Read(ToolActivation.Section).Switches;
                var options = settings.Read(McpServersOptions.Section);
                var changed = options;
                List<KeyValuePair<string, List<Registered>>> all;
                lock (gate)
                {
                    all = [.. tools.Select(pair => new KeyValuePair<string, List<Registered>>(pair.Key, [.. pair.Value]))];
                }

                foreach (var (name, registered) in all)
                {
                    if (changed.Find(name) is not { } server)
                    {
                        continue;
                    }

                    var approved = new Dictionary<string, string>(server.Approved ?? new Dictionary<string, string>(), StringComparer.Ordinal);
                    var dirty = false;
                    foreach (var entry in registered.Where(entry => activation?.GetValueOrDefault(entry.Tool.Name) == true))
                    {
                        if (!approved.TryGetValue(entry.Tool.ServerTool.Name, out var fingerprint) || !string.Equals(fingerprint, entry.Tool.Fingerprint, StringComparison.Ordinal))
                        {
                            approved[entry.Tool.ServerTool.Name] = entry.Tool.Fingerprint;
                            dirty = true;
                        }
                    }

                    if (dirty)
                    {
                        changed = changed.With(server with { Approved = approved });
                    }
                }

                if (!ReferenceEquals(changed, options))
                {
                    await settings.WriteAsync(McpServersOptions.Section, changed).ConfigureAwait(false);
                }
            }
            finally
            {
                sync.Release();
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LogApprovalFailed(logger, exception);
        }

        Notify();
    }

    /// <summary>
    /// The name of a tool in the registry (ADR 0018, section 3): the name of the server as it is, or prefixed by the
    /// name of its server when the name is taken. <see langword="null"/> when even that is taken.
    /// </summary>
    private string? NameFor(string server, string offered, HashSet<string> taken)
    {
        // What every model accepts as a function name: letters, digits, underscores and hyphens, 64 at most.
        var plain = InvalidNameCharacter().Replace(offered, "_");
        plain = plain.Length > 64 ? plain[..64] : plain;
        if (registry.Find(plain) is null && !taken.Contains(plain))
        {
            return plain;
        }

        var prefixed = server.Replace('-', '_') + "__" + plain;
        prefixed = prefixed.Length > 64 ? prefixed[..64] : prefixed;
        return registry.Find(prefixed) is null && !taken.Contains(prefixed) ? prefixed : null;
    }

    private void Unregister(string server)
    {
        List<Registered>? old;
        lock (gate)
        {
            tools.Remove(server, out old);
        }

        foreach (var entry in old ?? [])
        {
            entry.Registration.Dispose();
        }
    }

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    private static partial Regex InvalidNameCharacter();

    [LoggerMessage(Level = LogLevel.Information, Message = "The connection test of the tool server '{Server}' failed: {Failure}.")]
    private static partial void LogTestFailed(ILogger logger, string server, ToolServerFailure failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tool '{Tool}' of the tool server '{Server}' changed since it was approved: it is turned off.")]
    private static partial void LogToolTurnedOff(ILogger logger, string server, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tool '{Tool}' of the tool server '{Server}' is left out: its name is taken.")]
    private static partial void LogToolSkipped(ILogger logger, string server, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The approval of a tool of a tool server could not be saved.")]
    private static partial void LogApprovalFailed(ILogger logger, Exception exception);

    private sealed record Registered(McpTool Tool, IDisposable Registration);
}
