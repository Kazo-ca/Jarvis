using Kazo.Jarvis.Kernel.Desktop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Kazo.Jarvis.Windows.Startup;

/// <summary>
/// <see cref="ILaunchAtLogin"/> of the Windows host: the value <c>Kazo.Jarvis</c> of the user's
/// <c>Run</c> key. A development build (run from <c>bin\Debug</c> or <c>bin\Release</c>) does not register
/// itself, so that trying a branch never leaves it starting at every sign-in; set the environment variable
/// <c>KAZO_JARVIS_REGISTER_STARTUP=1</c> to check the registration from such a build.
/// </summary>
internal sealed partial class RegistryLaunchAtLogin : ILaunchAtLogin
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Kazo.Jarvis";
    private const string ForceVariable = "KAZO_JARVIS_REGISTER_STARTUP";

    private readonly ILogger<RegistryLaunchAtLogin> logger;

    /// <summary>Creates the service.</summary>
    public RegistryLaunchAtLogin(ILogger<RegistryLaunchAtLogin> logger) => this.logger = logger;

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    /// <inheritdoc />
    public bool SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }

        var executable = Environment.ProcessPath;
        if (executable is null)
        {
            return false;
        }

        if (IsDevelopmentBuild(executable) && Environment.GetEnvironmentVariable(ForceVariable) != "1")
        {
            LogDevelopmentBuild(executable, ForceVariable);
            return false;
        }

        var command = $"\"{executable}\"";
        if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue(ValueName, command);
        }

        return true;
    }

    private static bool IsDevelopmentBuild(string executable) =>
        executable.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
        || executable.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Information, Message = "Development build {Executable}: start with Windows not registered (set {Variable}=1 to register it).")]
    private partial void LogDevelopmentBuild(string executable, string variable);
}
