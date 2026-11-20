using System.Globalization;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// The Windows display language (ADR 0008, section 1), read once when the host starts. Kazo.Jarvis never
/// changes the thread culture (the assistant language goes through the resources), so this is the language
/// Windows gave the process.
/// </summary>
internal sealed class WindowsSystemLanguage : ISystemLanguage
{
    /// <inheritdoc />
    public CultureInfo DisplayCulture { get; } = CultureInfo.CurrentUICulture;
}
