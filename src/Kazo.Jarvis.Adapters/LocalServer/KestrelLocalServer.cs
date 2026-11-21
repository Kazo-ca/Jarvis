using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Kazo.Jarvis.Adapters.LocalServer;

/// <summary>
/// The local server of Jarvis (ADR 0021, section 3): Kestrel inside the process, bound to <c>127.0.0.1</c> and to
/// nothing else, so that it never opens to the network. Every request is refused before anything else if it does
/// not carry a known token: the token of this run for the sessions Jarvis starts, or the token of the vault for
/// the hooks the user installed, which opens <c>/hook/…</c> only. A port already taken is told by
/// <see cref="Status"/>, it never stops Jarvis.
/// </summary>
public sealed partial class KestrelLocalServer : ILocalServer, IHostedService, IAsyncDisposable
{
    /// <summary>The largest body accepted: a hook sends the input of one tool, not a file.</summary>
    internal const long MostBody = 1024 * 1024;

    /// <summary>The path of the MCP server of Jarvis, which carries the <c>approve</c> tool.</summary>
    public const string McpPath = "/mcp";

    /// <summary>The name of the permission tool, as Claude Code is told by <c>--permission-prompt-tool mcp__jarvis__approve</c>.</summary>
    public const string ApproveTool = "approve";

    private const string Bearer = "Bearer ";
    private const string TaskItem = "jarvis.task";

    private readonly ISettingsStore settings;
    private readonly Dictionary<string, ILocalRoute> routes;
    private readonly ISecretVault? vault;
    private readonly IClaudeApprovals? approvals;
    private readonly ILogger<KestrelLocalServer>? logger;
    private readonly int? fixedPort;
    private readonly SemaphoreSlim turn = new(1, 1);
    private readonly byte[] sessionToken;
    private IDisposable? watch;
    private WebApplication? app;
    private volatile LocalServerStatus status;
    private bool stopped;

    /// <summary>Creates the server; it listens once started.</summary>
    /// <param name="settings">The settings, for <see cref="LocalServerOptions.Section"/>.</param>
    /// <param name="routes">The routes given by the slices.</param>
    /// <param name="vault">The vault, for the token of the user's hooks; without it no such hook is accepted.</param>
    /// <param name="logger">The log. A token is never written to it.</param>
    /// <param name="approvals">Who decides the requests of the <c>approve</c> tool; without it every request is denied.</param>
    public KestrelLocalServer(ISettingsStore settings, IEnumerable<ILocalRoute> routes, ISecretVault? vault = null, ILogger<KestrelLocalServer>? logger = null, IClaudeApprovals? approvals = null)
        : this(settings, routes, vault, logger, null, approvals)
    {
    }

    internal KestrelLocalServer(ISettingsStore settings, IEnumerable<ILocalRoute> routes, ISecretVault? vault, ILogger<KestrelLocalServer>? logger, int? fixedPort, IClaudeApprovals? approvals = null)
    {
        this.approvals = approvals;
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(routes);
        this.settings = settings;
        this.vault = vault;
        this.logger = logger;
        this.fixedPort = fixedPort;
        this.routes = new Dictionary<string, ILocalRoute>(StringComparer.Ordinal);
        foreach (var route in routes)
        {
            // Two slices on one path is a mistake of the build: the first stays, it is told.
            if (!this.routes.TryAdd(route.Path, route) && logger is not null)
            {
                LogDuplicate(logger, route.Path);
            }
        }

        SessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        sessionToken = Encoding.UTF8.GetBytes(SessionToken);
        status = new LocalServerStatus(false, fixedPort ?? settings.Read(LocalServerOptions.Section).Port);
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public LocalServerStatus Status => status;

    /// <inheritdoc />
    public string SessionToken { get; }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ListenAsync().ConfigureAwait(false);
        if (fixedPort is null)
        {
            // A new port takes effect at once; the hooks installed with the old one must be installed again.
            watch ??= settings.Watch(LocalServerOptions.Section, changed => { _ = ListenAsync(); });
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        watch?.Dispose();
        watch = null;
        await turn.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            stopped = true;
            await CloseAsync().ConfigureAwait(false);
        }
        finally
        {
            turn.Release();
        }

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        turn.Dispose();
    }

    private async Task ListenAsync()
    {
        await turn.WaitAsync().ConfigureAwait(false);
        try
        {
            var port = fixedPort ?? settings.Read(LocalServerOptions.Section).Port;
            if (stopped || (app is not null && status.Listening && status.Port == port))
            {
                return;
            }

            await CloseAsync().ConfigureAwait(false);
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore().ConfigureKestrel(kestrel =>
            {
                // The loopback address, never « any address »: the server must not be reachable from the network.
                kestrel.Listen(IPAddress.Loopback, port);
                kestrel.AddServerHeader = false;
                kestrel.Limits.MaxRequestBodySize = MostBody;
            });

            // The MCP server of Jarvis (ADR 0021, section 3), without a session of its own: one request, one answer.
            var context = new HttpContextAccessor();
            builder.Services.AddRoutingCore();
            builder.Services.AddSingleton<IHttpContextAccessor>(context);
            builder.Services
                .AddMcpServer(options => options.ServerInfo = new() { Name = "jarvis", Version = "1" })
                .WithHttpTransport(options => options.Stateless = true)
                .WithTools([ApproveToolOf(context)]);
            var built = builder.Build();
            built.Use(ServeAsync);
            built.UseRouting();
            built.MapMcp(McpPath);
            try
            {
                await built.StartAsync().ConfigureAwait(false);
                app = built;
                var bound = built.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
                    .Select(address => new Uri(address).Port)
                    .FirstOrDefault() ?? port;
                status = new LocalServerStatus(true, bound);
                if (logger is not null)
                {
                    LogListening(logger, bound);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or System.Net.Sockets.SocketException)
            {
                await built.DisposeAsync().ConfigureAwait(false);
                status = new LocalServerStatus(false, port, exception.Message);
                if (logger is not null)
                {
                    LogNotListening(logger, port, exception);
                }
            }
        }
        finally
        {
            turn.Release();
        }

        Changed?.Invoke();
    }

    private async Task CloseAsync()
    {
        if (app is { } running)
        {
            app = null;
            status = status with { Listening = false };
            await running.StopAsync().ConfigureAwait(false);
            await running.DisposeAsync().ConfigureAwait(false);
        }
    }

    private McpServerTool ApproveToolOf(IHttpContextAccessor context) => McpServerTool.Create(
        async (string tool_name, JsonElement input, CancellationToken cancellationToken, string? tool_use_id = null) =>
        {
            // The answer is a JSON text, the form Claude Code expects from a permission tool (spike E12).
            var task = context.HttpContext?.Items[TaskItem] as string;
            ClaudeApproval answer;
            try
            {
                answer = approvals is null
                    ? ClaudeApproval.Deny("Jarvis cannot decide permissions right now.")
                    : await approvals.AskAsync(new ClaudeApprovalRequest(task, tool_name, input.Clone(), tool_use_id), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Whatever fails, the session does not write.
                if (logger is not null)
                {
                    LogRouteFailed(logger, McpPath, exception);
                }

                answer = ClaudeApproval.Deny("Jarvis could not decide.");
            }

            return answer.Allowed
                ? new JsonObject { ["behavior"] = "allow", ["updatedInput"] = JsonNode.Parse(input.GetRawText()) }.ToJsonString()
                : new JsonObject { ["behavior"] = "deny", ["message"] = answer.Message ?? "Denied by Jarvis." }.ToJsonString();
        },
        new McpServerToolCreateOptions
        {
            Name = ApproveTool,
            Description = "Asks Jarvis (the user) whether Claude Code may use a tool.",
        });

    private async Task ServeAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        var path = request.Path.Value ?? string.Empty;

        // The token first: without it, a caller learns nothing, not even which routes exist.
        if (await CallerAsync(request, context.RequestAborted).ConfigureAwait(false) is not { } caller)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (caller == LocalCaller.UserHook && !path.StartsWith(HookPaths.Prefix, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (path == McpPath)
        {
            // Only a session Jarvis started reaches here (the token of the user's hooks was refused above).
            if (request.Headers.TryGetValue(LocalRequest.TaskHeader, out var named) && named.ToString().Trim() is { Length: > 0 } task)
            {
                context.Items[TaskItem] = task;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        if (!routes.TryGetValue(path, out var route))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!HttpMethods.IsPost(request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        LocalResponse response;
        try
        {
            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    headers[header.Key] = header.Value.ToString();
                }
            }

            response = await route.HandleAsync(new LocalRequest(path, caller, headers, body), context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not BadHttpRequestException)
        {
            // A route that fails must not take the server down, nor tell the caller why.
            if (logger is not null)
            {
                LogRouteFailed(logger, path, exception);
            }

            response = new LocalResponse(StatusCodes.Status500InternalServerError);
        }

        context.Response.StatusCode = response.Status;
        if (response.Body is { Length: > 0 } text)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(text, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private async ValueTask<LocalCaller?> CallerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var given = Encoding.UTF8.GetBytes(header[Bearer.Length..].Trim());
        if (given.Length == 0)
        {
            return null;
        }

        // Compared in fixed time: the time of a refusal must not tell how much of a token was right.
        if (CryptographicOperations.FixedTimeEquals(given, sessionToken))
        {
            return LocalCaller.JarvisSession;
        }

        if (vault is null)
        {
            return null;
        }

        try
        {
            var kept = await vault.GetAsync(LocalServerOptions.UserHookToken, cancellationToken).ConfigureAwait(false);
            return kept is not null && CryptographicOperations.FixedTimeEquals(given, Encoding.UTF8.GetBytes(kept.Reveal()))
                ? LocalCaller.UserHook
                : null;
        }
        catch (SecretVaultException)
        {
            // A vault that cannot be read gives no access.
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The local server listens on 127.0.0.1:{Port}.")]
    private static partial void LogListening(ILogger logger, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The local server could not listen on 127.0.0.1:{Port}.")]
    private static partial void LogNotListening(ILogger logger, int port, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Two routes of the local server have the path {Path}: the first one stays.")]
    private static partial void LogDuplicate(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The route {Path} of the local server failed.")]
    private static partial void LogRouteFailed(ILogger logger, string path, Exception exception);
}
