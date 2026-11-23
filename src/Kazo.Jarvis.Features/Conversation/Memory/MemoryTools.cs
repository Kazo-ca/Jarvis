using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>What the three tools of the memory share: reading an argument, comparing words, writing a result.</summary>
internal static partial class MemoryToolSupport
{
    /// <summary>Results are data, with accents left readable.</summary>
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static JsonElement TextSchema(string property, string description) => JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object> { [property] = new { type = "string", description } },
        ["required"] = new[] { property },
    });

    public static string? Text(JsonElement arguments, string property) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static string Result(object value) => JsonSerializer.Serialize(value, Json);

    public static string Date(MemoryFact fact) => fact.At.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The words of a text that carry meaning: three letters or more, without case.</summary>
    public static IReadOnlyList<string> Words(string? text) =>
        [.. Word().Matches((text ?? string.Empty).ToLowerInvariant()).Select(match => match.Value).Where(word => word.Length >= 3).Distinct(StringComparer.Ordinal)];

    /// <summary>How many of <paramref name="words"/> are in <paramref name="fact"/>.</summary>
    public static int Score(MemoryFact fact, IReadOnlyList<string> words)
    {
        var own = Words(fact.Text);
        return words.Count(word => own.Contains(word, StringComparer.Ordinal));
    }

    [GeneratedRegex(@"[\p{L}\p{Nd}]+")]
    private static partial Regex Word();
}

/// <summary>
/// The tool <c>remember_fact</c> (ADR 0019, section 2): « souviens-toi que… ». Class « Effet courant »: no
/// confirmation, the call shows in the conversation. Refused once the turn holds a content read from outside
/// (section 3): its text comes from the model, which may repeat what it read.
/// </summary>
public sealed class RememberFactTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "remember_fact";

    private readonly MemoryBook book;
    private readonly ISettingsStore settings;

    /// <summary>Creates the tool.</summary>
    /// <param name="book">What is kept.</param>
    /// <param name="settings">The settings, for the general switch.</param>
    public RememberFactTool(MemoryBook book, ISettingsStore settings)
    {
        this.book = book;
        this.settings = settings;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public bool RefusedAfterOutsideContent => true;

    /// <inheritdoc />
    public JsonElement Schema { get; } = MemoryToolSupport.TextSchema("text", "The fact, in one sentence.");

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return MemoryRecaller.Text("Remember_Description", language);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!settings.Read(MemoryOptions.Section).Enabled)
        {
            throw new ToolException("The memory is turned off in the settings: nothing was kept.");
        }

        return await book.AddAsync(MemoryToolSupport.Text(arguments, "text"), FactSource.Asked, null, cancellationToken).ConfigureAwait(false) switch
        {
            FactOutcome.Added => MemoryToolSupport.Result(new { remembered = true }),
            FactOutcome.AlreadyKnown => MemoryToolSupport.Result(new { remembered = true, already_known = true }),
            FactOutcome.Empty => throw new ToolException("The fact is missing."),
            FactOutcome.TooLong => throw new ToolException($"The fact is longer than {MemoryFact.MostCharacters} characters: say it in one short sentence."),
            FactOutcome.LooksLikeSecret => throw new ToolException("This looks like a password, a key or a card number: Jarvis never keeps one."),
            FactOutcome.Full => throw new ToolException($"The memory holds {MemoryBook.MostFacts} facts: the user must erase some on the memory page."),
            _ => throw new ToolException("The memory cannot be written right now."),
        };
    }
}

/// <summary>
/// The tool <c>remember_agent_fact</c> (ADR 0025, section 5): what an agent learns while working and that serves
/// it alone (« Articulé est la lampe du bureau »). Every agent but the Jarvis agent sees it; the fact goes to the
/// memory of the agent that calls. Same guards as <see cref="RememberFactTool"/>.
/// </summary>
public sealed class RememberAgentFactTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "remember_agent_fact";

    private readonly MemoryBook book;
    private readonly ISettingsStore settings;

    /// <summary>Creates the tool.</summary>
    /// <param name="book">What is kept.</param>
    /// <param name="settings">The settings, for the general switch.</param>
    public RememberAgentFactTool(MemoryBook book, ISettingsStore settings)
    {
        this.book = book;
        this.settings = settings;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public bool RefusedAfterOutsideContent => true;

    /// <inheritdoc />
    public bool ForEveryAgent => true;

    /// <inheritdoc />
    public JsonElement Schema { get; } = MemoryToolSupport.TextSchema("text", "The fact, in one sentence.");

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return MemoryRecaller.Text("RememberAgent_Description", language);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        throw new ToolException("No agent is answering: there is no agent memory to write.");

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, string? agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agent))
        {
            throw new ToolException("No agent is answering: there is no agent memory to write.");
        }

        if (!settings.Read(MemoryOptions.Section).Enabled)
        {
            throw new ToolException("The memory is turned off in the settings: nothing was kept.");
        }

        return await book.AddForAgentAsync(agent, MemoryToolSupport.Text(arguments, "text"), FactSource.Asked, cancellationToken).ConfigureAwait(false) switch
        {
            FactOutcome.Added => MemoryToolSupport.Result(new { remembered = true }),
            FactOutcome.AlreadyKnown => MemoryToolSupport.Result(new { remembered = true, already_known = true }),
            FactOutcome.Empty => throw new ToolException("The fact is missing."),
            FactOutcome.TooLong => throw new ToolException($"The fact is longer than {MemoryFact.MostCharacters} characters: say it in one short sentence."),
            FactOutcome.LooksLikeSecret => throw new ToolException("This looks like a password, a key or a card number: Jarvis never keeps one."),
            FactOutcome.Full => throw new ToolException($"The memory holds {MemoryBook.MostFacts} facts: the user must erase some on the memory page."),
            _ => throw new ToolException("The memory cannot be written right now."),
        };
    }
}

/// <summary>
/// The tool <c>forget_fact</c> (ADR 0019, section 2): « oublie que… ». It erases the one fact that matches the
/// words best; when several match as well, none is erased and they are returned.
/// </summary>
public sealed class ForgetFactTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "forget_fact";

    private readonly MemoryBook book;

    /// <summary>Creates the tool.</summary>
    /// <param name="book">What is kept.</param>
    public ForgetFactTool(MemoryBook book)
    {
        this.book = book;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public JsonElement Schema { get; } = MemoryToolSupport.TextSchema("text", "A few words of the fact to erase.");

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return MemoryRecaller.Text("Forget_Description", language);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var text = MemoryToolSupport.Text(arguments, "text");
        var words = MemoryToolSupport.Words(text);
        if (words.Count == 0)
        {
            throw new ToolException("A few words of the fact to erase are missing.");
        }

        var facts = await book.FactsAsync(cancellationToken).ConfigureAwait(false);
        if (facts.FirstOrDefault(fact => MemoryBook.Same(fact.Text, MemoryBook.Clean(text))) is { } exact)
        {
            await book.RemoveAsync(exact.Key, cancellationToken).ConfigureAwait(false);
            return MemoryToolSupport.Result(new { forgotten = new[] { exact.Text } });
        }

        // At least half of the words asked must be in the fact.
        var scored = facts
            .Select(fact => (Fact: fact, Score: MemoryToolSupport.Score(fact, words)))
            .Where(entry => entry.Score * 2 >= words.Count)
            .OrderByDescending(entry => entry.Score)
            .ToList();
        if (scored.Count == 0)
        {
            return MemoryToolSupport.Result(new { forgotten = Array.Empty<string>(), reason = "no fact matches" });
        }

        var best = scored.Where(entry => entry.Score == scored[0].Score).Select(entry => entry.Fact).ToList();
        if (best.Count > 1)
        {
            return MemoryToolSupport.Result(new { forgotten = Array.Empty<string>(), reason = "several facts match: ask the user which one", candidates = best.Take(5).Select(fact => fact.Text) });
        }

        await book.RemoveAsync(best[0].Key, cancellationToken).ConfigureAwait(false);
        return MemoryToolSupport.Result(new { forgotten = new[] { best[0].Text } });
    }
}

/// <summary>
/// The tool <c>search_memory</c> (ADR 0019, section 6): a search by words in the text of every fact, for the ones
/// that did not fit in the block sent to the model. Class « Lecture ». No full-text index and no vectors.
/// </summary>
public sealed class SearchMemoryTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "search_memory";

    /// <summary>Most facts returned.</summary>
    public const int MostResults = 10;

    private readonly MemoryBook book;
    private readonly ISettingsStore settings;

    /// <summary>Creates the tool.</summary>
    /// <param name="book">What is kept.</param>
    /// <param name="settings">The settings, for the general switch.</param>
    public SearchMemoryTool(MemoryBook book, ISettingsStore settings)
    {
        this.book = book;
        this.settings = settings;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema { get; } = MemoryToolSupport.TextSchema("query", "The words to look for.");

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return MemoryRecaller.Text("Search_Description", language);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Read;

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!settings.Read(MemoryOptions.Section).Enabled)
        {
            // Off: nothing of the memory goes to the model, by this way either.
            throw new ToolException("The memory is turned off in the settings.");
        }

        var words = MemoryToolSupport.Words(MemoryToolSupport.Text(arguments, "query"));
        if (words.Count == 0)
        {
            throw new ToolException("The words to look for are missing.");
        }

        var found = (await book.FactsAsync(cancellationToken).ConfigureAwait(false))
            .Select(fact => (Fact: fact, Score: MemoryToolSupport.Score(fact, words)))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score).ThenByDescending(entry => entry.Fact.At)
            .Take(MostResults)
            .Select(entry => new { text = entry.Fact.Text, date = MemoryToolSupport.Date(entry.Fact) })
            .ToList();
        return MemoryToolSupport.Result(new { facts = found });
    }
}
