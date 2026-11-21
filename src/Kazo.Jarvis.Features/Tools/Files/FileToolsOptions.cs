using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Tools.Files;

/// <summary>
/// Settings of the file tools (ADR 0009, section 5; ADR 0017, section 3), section <c>Tools.Files</c> of
/// <c>config.json</c>: the allowed folders, and how much text one call returns.
/// </summary>
public sealed record FileToolsOptions
{
    /// <summary>The smallest and the largest <see cref="ResultCharacters"/>: the tool loop cuts every result at 2 000 characters.</summary>
    public const int LeastResultCharacters = 200, MostResultCharacters = 2000;

    /// <summary>The most allowed folders.</summary>
    public const int MostFolders = 50;

    /// <summary>The section.</summary>
    public static SettingsSection<FileToolsOptions> Section { get; } = new("Tools.Files", () => new FileToolsOptions(), Validate);

    /// <summary>The folders the file tools may read, as the user wrote them. Empty: the tools see nothing.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    /// <summary>The most characters one call returns to the model.</summary>
    public int ResultCharacters { get; init; } = MostResultCharacters;

    private static IReadOnlyList<string> Validate(FileToolsOptions value)
    {
        var problems = new List<string>();
        if (value.Folders is null)
        {
            problems.Add("The list of allowed folders is missing.");
        }
        else
        {
            if (value.Folders.Count > MostFolders)
            {
                problems.Add($"At most {MostFolders} folders can be allowed.");
            }

            if (value.Folders.Any(string.IsNullOrWhiteSpace))
            {
                problems.Add("An allowed folder has no path.");
            }
        }

        if (value.ResultCharacters is < LeastResultCharacters or > MostResultCharacters)
        {
            problems.Add($"The size of a result is between {LeastResultCharacters} and {MostResultCharacters} characters.");
        }

        return problems;
    }
}
