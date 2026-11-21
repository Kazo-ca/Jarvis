using System.Globalization;
using System.Text.Json.Serialization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.ClaudeCode.Announcements;

/// <summary>What Jarvis announces for the Claude Code sessions of a folder (ADR 0021, section 9).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnnouncementLevel>))]
public enum AnnouncementLevel
{
    /// <summary>The ends and the waits. The default.</summary>
    All,

    /// <summary>Only when a session waits for the user.</summary>
    WaitsOnly,

    /// <summary>Nothing, neither said nor written.</summary>
    None,
}

/// <summary>The choice made for one folder.</summary>
/// <param name="Folder">The folder of the sessions.</param>
/// <param name="Level">What is announced for it.</param>
public sealed record FolderAnnouncements(string Folder, AnnouncementLevel Level);

/// <summary>Hours of every day during which nothing is said (ADR 0021, section 9). A range may pass midnight.</summary>
/// <param name="From">The first minute of the silence, <c>HH:mm</c>, local time.</param>
/// <param name="To">The minute the silence ends, <c>HH:mm</c>, local time.</param>
public sealed record QuietRange(string From, string To)
{
    /// <summary>Whether <paramref name="time"/> is in the range.</summary>
    /// <param name="time">A local time of day.</param>
    /// <returns><see langword="true"/> from <see cref="From"/> included to <see cref="To"/> excluded.</returns>
    public bool Holds(TimeOnly time) =>
        Parse(From) is { } from && Parse(To) is { } to && from != to && time.IsBetween(from, to);

    /// <summary>Reads <c>HH:mm</c>.</summary>
    /// <param name="text">The text of a setting or of a field.</param>
    /// <returns>The time, or <see langword="null"/> when the text is not one.</returns>
    public static TimeOnly? Parse(string? text) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
}

/// <summary>
/// Settings of the announcements of the Claude Code sessions (US2.12; ADR 0021, section 9), section
/// <c>ClaudeCode.Announcements</c> of <c>config.json</c>. By default everything is announced and there are no
/// quiet hours.
/// </summary>
public sealed record ClaudeAnnouncementsOptions
{
    /// <summary>The most quiet ranges that can be set.</summary>
    public const int MostRanges = 6;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<ClaudeAnnouncementsOptions> Section { get; } = new("ClaudeCode.Announcements", () => new ClaudeAnnouncementsOptions(), Validate);

    /// <summary>The folders whose choice is not the default one.</summary>
    public IReadOnlyList<FolderAnnouncements> Folders { get; init; } = [];

    /// <summary>The quiet hours of every day. Empty by default.</summary>
    public IReadOnlyList<QuietRange> QuietHours { get; init; } = [];

    /// <summary>What is announced for <paramref name="folder"/>.</summary>
    /// <param name="folder">The folder of a session, or <see langword="null"/> when it is not known.</param>
    /// <returns>The choice of the user, or <see cref="AnnouncementLevel.All"/>.</returns>
    public AnnouncementLevel LevelOf(string? folder) =>
        folder is not null && Folders?.FirstOrDefault(known => Same(known.Folder, folder)) is { } choice ? choice.Level : AnnouncementLevel.All;

    /// <summary>These settings with the choice of <paramref name="folder"/> changed.</summary>
    /// <param name="folder">A folder.</param>
    /// <param name="level">What is announced for it; the default one removes its line.</param>
    /// <returns>The new settings.</returns>
    public ClaudeAnnouncementsOptions WithLevel(string folder, AnnouncementLevel level)
    {
        var others = (Folders ?? []).Where(known => !Same(known.Folder, folder));
        return this with { Folders = [.. level == AnnouncementLevel.All ? others : others.Append(new FolderAnnouncements(folder, level))] };
    }

    /// <summary>Whether <paramref name="time"/> is in one of the quiet ranges.</summary>
    /// <param name="time">A local time of day.</param>
    /// <returns><see langword="true"/> during the quiet hours.</returns>
    public bool IsQuiet(TimeOnly time) => QuietHours is not null && QuietHours.Any(range => range.Holds(time));

    private static bool Same(string left, string right) =>
        string.Equals(left.TrimEnd('/', '\\'), right.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Validate(ClaudeAnnouncementsOptions value)
    {
        var problems = new List<string>();
        if (value.Folders is null || value.Folders.Any(folder => folder is null || string.IsNullOrWhiteSpace(folder.Folder)))
        {
            problems.Add("A folder of the announcements has no path.");
        }

        if (value.QuietHours is null || value.QuietHours.Count > MostRanges)
        {
            problems.Add($"At most {MostRanges} quiet ranges can be set.");
        }
        else if (value.QuietHours.Any(range => range is null || QuietRange.Parse(range.From) is null || QuietRange.Parse(range.To) is null))
        {
            problems.Add("A quiet range goes from a time to another, written HH:mm.");
        }
        else if (value.QuietHours.Any(range => range.From == range.To))
        {
            problems.Add("A quiet range starts and ends at two different times.");
        }

        return problems;
    }
}
