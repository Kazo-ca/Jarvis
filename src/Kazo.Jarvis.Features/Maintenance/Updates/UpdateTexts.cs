using System.Globalization;
using System.Resources;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>
/// Visible texts of the updates slice, in English (<c>UpdateTexts.resx</c>) and French
/// (<c>UpdateTexts.fr.resx</c>), after the current UI culture (ADR 0008). Work item
/// stage1/5-shared-ui-language may serve the same files through <c>IStringLocalizer</c>.
/// </summary>
public static class UpdateTexts
{
    private static readonly ResourceManager Resources = new("Kazo.Jarvis.Features.Maintenance.Updates.UpdateTexts", typeof(UpdateTexts).Assembly);

    /// <summary>Updates</summary>
    public static string WindowTitle => Get(nameof(WindowTitle));

    /// <summary>Installed version: {0}</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the current UI language.</returns>
    public static string CurrentVersion(string value) => string.Format(CultureInfo.CurrentUICulture, Get(nameof(CurrentVersion)), value);

    /// <summary>unknown</summary>
    public static string UnknownVersion => Get(nameof(UnknownVersion));

    /// <summary>Jarvis has not looked for a new version yet.</summary>
    public static string Idle => Get(nameof(Idle));

    /// <summary>Looking for a new version…</summary>
    public static string Checking => Get(nameof(Checking));

    /// <summary>This is the newest version.</summary>
    public static string UpToDate => Get(nameof(UpToDate));

    /// <summary>Version {0} is available.</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the current UI language.</returns>
    public static string Available(string value) => string.Format(CultureInfo.CurrentUICulture, Get(nameof(Available)), value);

    /// <summary>Downloading version {0}…</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the current UI language.</returns>
    public static string Downloading(string value) => string.Format(CultureInfo.CurrentUICulture, Get(nameof(Downloading)), value);

    /// <summary>Jarvis closes to install version {0}, then starts again.</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the current UI language.</returns>
    public static string Installing(string value) => string.Format(CultureInfo.CurrentUICulture, Get(nameof(Installing)), value);

    /// <summary>GitHub could not be reached. Check the connection, then try again.</summary>
    public static string FailedUnreachable => Get(nameof(FailedUnreachable));

    /// <summary>The downloaded file is damaged. Try again.</summary>
    public static string FailedCorrupted => Get(nameof(FailedCorrupted));

    /// <summary>The updater could not start. The details are in the log.</summary>
    public static string FailedLaunch => Get(nameof(FailedLaunch));

    /// <summary>This development build does not install updates.</summary>
    public static string NotInstallable => Get(nameof(NotInstallable));

    /// <summary>Check for updates</summary>
    public static string CheckButton => Get(nameof(CheckButton));

    /// <summary>Install version {0}</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the current UI language.</returns>
    public static string InstallButton(string value) => string.Format(CultureInfo.CurrentUICulture, Get(nameof(InstallButton)), value);

    /// <summary>Release notes</summary>
    public static string ReleaseNotes => Get(nameof(ReleaseNotes));

    /// <summary>Check automatically (asks GitHub every 6 hours)</summary>
    public static string AutomaticLabel => Get(nameof(AutomaticLabel));

    private static string Get(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
