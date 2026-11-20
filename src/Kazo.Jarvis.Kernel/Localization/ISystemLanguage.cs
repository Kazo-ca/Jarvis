using System.Globalization;

namespace Kazo.Jarvis.Kernel.Localization;

/// <summary>
/// The display language of the operating system, from which the language of the assistant is proposed
/// until the user chooses one (ADR 0008, section 1). The Windows host reads it from Windows; the Web host
/// simulates it.
/// </summary>
public interface ISystemLanguage
{
    /// <summary>The display language of Windows, as it was when the application started.</summary>
    CultureInfo DisplayCulture { get; }
}
