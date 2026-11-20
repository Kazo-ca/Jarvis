using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>
/// Visible texts of the updates slice, in English (<c>UpdateTexts.resx</c>) and French
/// (<c>UpdateTexts.fr.resx</c>), in the language of the assistant (ADR 0008), served through
/// <c>IStringLocalizer</c>.
/// </summary>
public sealed class UpdateTexts
{
    private readonly IStringLocalizer texts;

    /// <summary>Creates the texts.</summary>
    /// <param name="localizers">Gives the texts in the assistant language (stage1/5-shared-ui-language).</param>
    public UpdateTexts(IStringLocalizerFactory localizers)
    {
        ArgumentNullException.ThrowIfNull(localizers);
        texts = localizers.Create(typeof(UpdateTexts));
    }

    /// <summary>Updates</summary>
    public string WindowTitle => texts[nameof(WindowTitle)];

    /// <summary>Installed version: {0}</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the assistant language.</returns>
    public string CurrentVersion(string value) => texts[nameof(CurrentVersion), value];

    /// <summary>unknown</summary>
    public string UnknownVersion => texts[nameof(UnknownVersion)];

    /// <summary>Jarvis has not looked for a new version yet.</summary>
    public string Idle => texts[nameof(Idle)];

    /// <summary>Looking for a new version…</summary>
    public string Checking => texts[nameof(Checking)];

    /// <summary>This is the newest version.</summary>
    public string UpToDate => texts[nameof(UpToDate)];

    /// <summary>Version {0} is available.</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the assistant language.</returns>
    public string Available(string value) => texts[nameof(Available), value];

    /// <summary>Downloading version {0}…</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the assistant language.</returns>
    public string Downloading(string value) => texts[nameof(Downloading), value];

    /// <summary>Jarvis closes to install version {0}, then starts again.</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the assistant language.</returns>
    public string Installing(string value) => texts[nameof(Installing), value];

    /// <summary>GitHub could not be reached. Check the connection, then try again.</summary>
    public string FailedUnreachable => texts[nameof(FailedUnreachable)];

    /// <summary>The downloaded file is damaged. Try again.</summary>
    public string FailedCorrupted => texts[nameof(FailedCorrupted)];

    /// <summary>The updater could not start. The details are in the log.</summary>
    public string FailedLaunch => texts[nameof(FailedLaunch)];

    /// <summary>This development build does not install updates.</summary>
    public string NotInstallable => texts[nameof(NotInstallable)];

    /// <summary>Check for updates</summary>
    public string CheckButton => texts[nameof(CheckButton)];

    /// <summary>Install version {0}</summary>
    /// <param name="value">The value for <c>{0}</c>.</param>
    /// <returns>The text in the assistant language.</returns>
    public string InstallButton(string value) => texts[nameof(InstallButton), value];

    /// <summary>Release notes</summary>
    public string ReleaseNotes => texts[nameof(ReleaseNotes)];

    /// <summary>Check automatically (asks GitHub every 6 hours)</summary>
    public string AutomaticLabel => texts[nameof(AutomaticLabel)];
}
