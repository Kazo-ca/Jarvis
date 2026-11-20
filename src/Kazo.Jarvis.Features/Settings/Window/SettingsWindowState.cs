namespace Kazo.Jarvis.Features.Settings.Window;

/// <summary>
/// The page selected in the settings window. Kept outside the component because a language change builds
/// the window again (ADR 0008, section 1): the user stays on the page where the language was changed.
/// </summary>
public sealed class SettingsWindowState
{
    /// <summary>Key of the selected page, or <see langword="null"/> for the first one.</summary>
    public string? SelectedKey { get; set; }
}
