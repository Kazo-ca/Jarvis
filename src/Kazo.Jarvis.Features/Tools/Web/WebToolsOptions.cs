using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Features.Tools.Web;

/// <summary>
/// Settings of the web tools (ADR 0017, section 3; ADR 0022, section 4), section <c>Tools.Web</c> of
/// <c>config.json</c>. Whether the tools are on is in <c>Tools.Activation</c>, with every other tool.
/// </summary>
public sealed record WebToolsOptions
{
    /// <summary>The smallest and the largest <see cref="ResultCharacters"/>: the tool loop cuts every result at 2 000 characters.</summary>
    public const int LeastResultCharacters = 200, MostResultCharacters = 2000;

    /// <summary>The largest <see cref="Results"/>.</summary>
    public const int MostResults = 10;

    /// <summary>The most <see cref="LocalExceptions"/>.</summary>
    public const int MostExceptions = 20;

    /// <summary>The section.</summary>
    public static SettingsSection<WebToolsOptions> Section { get; } = new("Tools.Web", () => new WebToolsOptions(), Validate);

    /// <summary>The search engine whose page the browser of Jarvis reads. DuckDuckGo had the most stable page in spike E15.</summary>
    public WebSearchEngine Engine { get; init; } = WebSearchEngine.DuckDuckGo;

    /// <summary>How many results a search returns.</summary>
    public int Results { get; init; } = 5;

    /// <summary>The most characters one call returns to the model.</summary>
    public int ResultCharacters { get; init; } = MostResultCharacters;

    /// <summary>
    /// Hosts of this machine or of the local network that may be read all the same, set by hand (ADR 0017,
    /// section 3): a name or an address, without scheme nor port.
    /// </summary>
    public IReadOnlyList<string> LocalExceptions { get; init; } = [];

    private static IReadOnlyList<string> Validate(WebToolsOptions value)
    {
        var problems = new List<string>();
        if (!Enum.IsDefined(value.Engine))
        {
            problems.Add("The search engine is not known.");
        }

        if (value.Results is < 1 or > MostResults)
        {
            problems.Add($"A search returns 1 to {MostResults} results.");
        }

        if (value.ResultCharacters is < LeastResultCharacters or > MostResultCharacters)
        {
            problems.Add($"The size of a result is between {LeastResultCharacters} and {MostResultCharacters} characters.");
        }

        if (value.LocalExceptions is null)
        {
            problems.Add("The list of local exceptions is missing.");
        }
        else
        {
            if (value.LocalExceptions.Count > MostExceptions)
            {
                problems.Add($"At most {MostExceptions} local exceptions can be listed.");
            }

            if (value.LocalExceptions.Any(host => Uri.CheckHostName(host?.Trim('[', ']') ?? string.Empty) == UriHostNameType.Unknown))
            {
                problems.Add("A local exception is a host name or an address, without scheme nor port.");
            }
        }

        return problems;
    }
}
