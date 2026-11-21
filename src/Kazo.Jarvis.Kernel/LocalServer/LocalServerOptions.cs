using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Kernel.LocalServer;

/// <summary>
/// The settings of the local server, section <c>Local.Server</c> of <c>config.json</c>. In the kernel because the
/// native server and the slices that write its address both read it.
/// </summary>
public sealed record LocalServerOptions
{
    /// <summary>The port used when none is set.</summary>
    public const int DefaultPort = 47821;

    /// <summary>The lowest port accepted: below, a port needs rights Jarvis does not have.</summary>
    public const int LeastPort = 1024;

    /// <summary>The highest port.</summary>
    public const int MostPort = 65535;

    /// <summary>The section.</summary>
    public static SettingsSection<LocalServerOptions> Section { get; } = new("Local.Server", () => new LocalServerOptions(), Validate);

    /// <summary>
    /// Where the vault keeps the token of the hooks installed in the user's Claude Code settings (ADR 0021,
    /// section 3). It opens <c>/hook/…</c> only. No token in the vault: no such hook is accepted.
    /// </summary>
    public static SecretRef UserHookToken { get; } = new("ClaudeCode.UserHookToken");

    /// <summary>
    /// The port, fixed: the hooks installed in the user's settings must know it (ADR 0021, section 3).
    /// </summary>
    public int Port { get; init; } = DefaultPort;

    private static IReadOnlyList<string> Validate(LocalServerOptions value) =>
        value.Port is < LeastPort or > MostPort ? [$"The port goes from {LeastPort} to {MostPort}."] : [];
}
