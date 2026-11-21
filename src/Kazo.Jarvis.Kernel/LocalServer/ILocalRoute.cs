namespace Kazo.Jarvis.Kernel.LocalServer;

/// <summary>
/// One route of the local server, given by a slice. The server has checked the token before the route is called;
/// the body is a piece of data, never an instruction (ADR 0009, principle 2).
/// </summary>
public interface ILocalRoute
{
    /// <summary>The path, starting with a slash, for example <c>/hook/task.start</c>. Only <c>POST</c> is served.</summary>
    string Path { get; }

    /// <summary>Handles one request.</summary>
    /// <param name="request">The request, its token already checked.</param>
    /// <param name="cancellationToken">Cancelled when the caller leaves or the server stops.</param>
    /// <returns>The answer.</returns>
    ValueTask<LocalResponse> HandleAsync(LocalRequest request, CancellationToken cancellationToken);
}

/// <summary>Who called a route, told by the token received.</summary>
public enum LocalCaller
{
    /// <summary>A session started by Jarvis, with the token of this run.</summary>
    JarvisSession,

    /// <summary>A hook the user installed in their own Claude Code settings, with the token kept in the vault. It reaches <c>/hook/…</c> only.</summary>
    UserHook,
}

/// <summary>A request received by the local server.</summary>
/// <param name="Path">The path called.</param>
/// <param name="Caller">Who called.</param>
/// <param name="Headers">The headers, their names compared without case. The token is not among them.</param>
/// <param name="Body">The body, as text.</param>
public sealed record LocalRequest(string Path, LocalCaller Caller, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>The header by which a session started by Jarvis names its task (ADR 0013, section 7).</summary>
    public const string TaskHeader = "X-Relais-Task";

    /// <summary>The task named by the caller, if any.</summary>
    public string? TaskId => Headers.TryGetValue(TaskHeader, out var id) && id.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}

/// <summary>The answer of a route.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">A JSON body, or nothing.</param>
public sealed record LocalResponse(int Status, string? Body = null)
{
    /// <summary>Done, nothing to say: a hook that receives no body goes on (ADR 0021, section 6).</summary>
    public static LocalResponse Done { get; } = new(204);

    /// <summary>The body could not be read.</summary>
    public static LocalResponse BadRequest { get; } = new(400);

    /// <summary>What the request names does not exist.</summary>
    public static LocalResponse NotFound { get; } = new(404);
}
