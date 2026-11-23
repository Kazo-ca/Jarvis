namespace Kazo.Jarvis.Kernel.LocalServer;

/// <summary>
/// The local server of Jarvis (ADR 0021, section 3): an HTTP server inside the process, listening on
/// <c>127.0.0.1</c> only, never on the network. It is a native service of the host; the slices give it their
/// routes as <see cref="ILocalRoute"/>. No route answers without a token.
/// </summary>
public interface ILocalServer
{
    /// <summary>Raised when <see cref="Status"/> changes.</summary>
    event Action? Changed;

    /// <summary>Whether the server listens, and on which port.</summary>
    LocalServerStatus Status { get; }

    /// <summary>
    /// The token of the sessions started by Jarvis: drawn at each start, kept in memory only, given to a session
    /// by its environment. It opens every route.
    /// </summary>
    string SessionToken { get; }
}

/// <summary>The state of the local server.</summary>
/// <param name="Listening">Whether it accepts requests.</param>
/// <param name="Port">The port it listens on, or the port asked when it does not listen.</param>
/// <param name="Problem">Why it does not listen, when it should; never shown as is to the user without a sentence around.</param>
public sealed record LocalServerStatus(bool Listening, int Port, string? Problem = null);
