namespace Kazo.Jarvis.Features.Settings.Window;

/// <summary>
/// The page selected in the settings window. Kept outside the component because a language change builds
/// the window again (ADR 0008, section 1): the user stays on the page where the language was changed. A request
/// for a given page (the setting that fixes a fault, US1.17) changes it while the window is open.
/// </summary>
public sealed class SettingsWindowState
{
    private string? selectedKey;

    /// <summary>Raised after <see cref="SelectedKey"/> changed, on any thread.</summary>
    public event Action? Changed;

    /// <summary>Key of the selected page, or <see langword="null"/> for the first one.</summary>
    public string? SelectedKey
    {
        get => Volatile.Read(ref selectedKey);
        set
        {
            if (Interlocked.Exchange(ref selectedKey, value) != value)
            {
                Changed?.Invoke();
            }
        }
    }
}
