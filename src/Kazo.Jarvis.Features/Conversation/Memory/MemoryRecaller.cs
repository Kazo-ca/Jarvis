using System.Globalization;
using System.Resources;
using System.Text;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>
/// What the model is reminded of (ADR 0019, section 6): a delimited block, preceded by the fixed rule that says it
/// is data, with the summary and the facts that fit in one eighth of the context. Pinned facts go first, then the
/// most recent; the others stay in the store, where <c>search_memory</c> finds them.
/// </summary>
public sealed class MemoryRecaller : IConversationMemory
{
    private static readonly ResourceManager Texts = new(typeof(MemoryTexts));

    private readonly MemoryBook book;
    private readonly ISettingsStore settings;

    /// <summary>Creates the recaller.</summary>
    /// <param name="book">What is kept.</param>
    /// <param name="settings">The settings, for <see cref="MemoryOptions.Section"/>.</param>
    public MemoryRecaller(MemoryBook book, ISettingsStore settings)
    {
        this.book = book;
        this.settings = settings;
    }

    /// <summary>The characters the memory may take in a context of <paramref name="contextLength"/> tokens: one eighth, at about 3 characters per token.</summary>
    /// <param name="contextLength">The context of the model, in tokens.</param>
    /// <returns>The budget, in characters.</returns>
    public static int Budget(int contextLength) => Math.Max(0, contextLength) * 3 / 8;

    /// <summary>
    /// What fits in <paramref name="budget"/> characters. The summary never takes more than half of it while facts
    /// want the room: a long summary would otherwise leave nothing of the user.
    /// </summary>
    /// <param name="facts">Every fact.</param>
    /// <param name="summary">The summary, or <see langword="null"/>.</param>
    /// <param name="budget">The budget, in characters.</param>
    /// <returns>The facts sent, in the order they are written, and the part of the summary sent.</returns>
    public static (IReadOnlyList<MemoryFact> Facts, string Summary) Select(IReadOnlyList<MemoryFact> facts, string? summary, int budget)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var text = (summary ?? string.Empty).Trim();
        var left = budget - Math.Min(text.Length, budget / 2);
        var sent = new List<MemoryFact>();
        foreach (var fact in facts.OrderByDescending(fact => fact.Pinned).ThenByDescending(fact => fact.At))
        {
            // A fact that does not fit is passed over; a shorter and older one may still fit.
            if (fact.Text.Length <= left)
            {
                left -= fact.Text.Length;
                sent.Add(fact);
            }
        }

        var room = budget - sent.Sum(fact => fact.Text.Length);
        return (sent, text.Length <= room ? text : room > 1 ? text[..(room - 1)].TrimEnd() + "…" : string.Empty);
    }

    /// <inheritdoc />
    public async ValueTask<MemoryRecall> RecallAsync(AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(language);
        var options = settings.Read(MemoryOptions.Section);
        if (!options.Enabled)
        {
            // Off: nothing is sent, and the history is what it was before the memory existed.
            return MemoryRecall.Nothing;
        }

        var facts = await book.FactsAsync(cancellationToken).ConfigureAwait(false);
        var summary = await book.SummaryAsync(cancellationToken).ConfigureAwait(false);
        var (sent, summarySent) = Select(facts, summary?.Text, Budget(contextLength));
        return new MemoryRecall(Block(language, sent, summarySent), TimeSpan.FromMinutes(options.SilenceMinutes));
    }

    /// <summary>The block written in the system message, or <see langword="null"/> when there is nothing to recall.</summary>
    internal static string? Block(AssistantLanguage language, IReadOnlyList<MemoryFact> facts, string summary)
    {
        if (facts.Count == 0 && summary.Length == 0)
        {
            return null;
        }

        var block = new StringBuilder();
        block.Append(Text("Block_Rule", language)).Append('\n').Append(Text("Block_Start", language)).Append('\n');
        if (summary.Length > 0)
        {
            block.Append(Text("Block_Summary", language)).Append(' ').Append(summary.Replace('[', '(').Replace(']', ')')).Append('\n');
        }

        if (facts.Count > 0)
        {
            block.Append(Text("Block_Facts", language)).Append('\n');
            foreach (var fact in facts)
            {
                block.Append("- ").Append(fact.Text).Append('\n');
            }
        }

        return block.Append(Text("Block_End", language)).ToString();
    }

    /// <summary>A fixed text of the memory, in <paramref name="language"/>.</summary>
    internal static string Text(string key, AssistantLanguage language, params object[] arguments)
    {
        var text = Texts.GetString(key, language.Culture) ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, arguments);
    }
}

/// <summary>Marker of the resources <c>MemoryTexts.resx</c> and <c>MemoryTexts.fr.resx</c>.</summary>
public sealed class MemoryTexts
{
    private MemoryTexts()
    {
    }
}

/// <summary>The instructions given to the local model for the two background tasks of the memory.</summary>
public static class MemoryPrompts
{
    /// <summary>The instructions of the extraction of facts (ADR 0019, section 4).</summary>
    /// <param name="language">The language of the turn.</param>
    /// <returns>The instructions.</returns>
    public static string Extraction(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return MemoryRecaller.Text("Prompt_Extraction", language, MemoryFact.MostCharacters);
    }

    /// <summary>The instructions of the summary (ADR 0019, section 5).</summary>
    /// <param name="language">The language of the turn.</param>
    /// <returns>The instructions.</returns>
    public static string Summary(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return MemoryRecaller.Text("Prompt_Summary", language, MemorySummary.MostCharacters);
    }
}
