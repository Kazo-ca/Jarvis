using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Catalog;

/// <summary>What the tools page computes, apart from the component so that it is tested without a browser.</summary>
public static class ToolCatalog
{
    /// <summary>
    /// A rough size, in tokens, of what the model receives for <paramref name="tools"/> (ADR 0017, section 4): name,
    /// description and schema of each, at about four characters a token. An estimate to compare with the context,
    /// not a count.
    /// </summary>
    /// <param name="tools">The tools given to the model.</param>
    /// <param name="language">The language of the descriptions.</param>
    /// <returns>The estimate.</returns>
    public static int EstimateTokens(IEnumerable<ITool> tools, AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(language);

        // 20 characters a tool for the JSON around its three parts.
        var characters = tools.Sum(tool => tool.Name.Length + tool.Describe(language).Length + tool.Schema.GetRawText().Length + 20);
        return (characters + 3) / 4;
    }
}
