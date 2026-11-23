using System.Text.Json.Serialization;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>Where a fact comes from (ADR 0019, section 2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactSource>))]
public enum FactSource
{
    /// <summary>The user asked Jarvis to remember it: the tool <c>remember_fact</c>.</summary>
    Asked,

    /// <summary>Drawn by the local model from what the user said in a turn.</summary>
    Auto,

    /// <summary>Typed by the user on the « Mémoire » page.</summary>
    Manual,
}

/// <summary>A short and lasting sentence about the user (ADR 0019, section 1), as the collection keeps it.</summary>
public sealed record MemoryFact
{
    /// <summary>Longest fact, in characters.</summary>
    public const int MostCharacters = 200;

    /// <summary>Identity of the fact in this slice.</summary>
    public Guid Key { get; init; } = Guid.NewGuid();

    /// <summary>The sentence.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Where it comes from.</summary>
    public FactSource Source { get; init; }

    /// <summary>When it was kept.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>The stored turn it comes from, when there is one.</summary>
    public Guid? Turn { get; init; }

    /// <summary>
    /// The agent this fact serves alone (ADR 0025, section 5), by its identity; <see langword="null"/> for a fact
    /// about the user, sent whoever answers.
    /// </summary>
    public string? Agent { get; init; }

    /// <summary>A pinned fact goes to the model before every other.</summary>
    public bool Pinned { get; init; }
}

/// <summary>The one summary of the exchanges that left the history (ADR 0019, section 5).</summary>
public sealed record MemorySummary
{
    /// <summary>Longest summary, in characters.</summary>
    public const int MostCharacters = 1200;

    /// <summary>The summary.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The moment of the last exchange the summary holds: an exchange after it is not summarized yet.</summary>
    public DateTimeOffset Through { get; init; }

    /// <summary>When the summary was written.</summary>
    public DateTimeOffset At { get; init; }
}

/// <summary>What happened to a fact given to the memory.</summary>
public enum FactOutcome
{
    /// <summary>Kept.</summary>
    Added,

    /// <summary>The same fact is already there, whatever the case and the spaces.</summary>
    AlreadyKnown,

    /// <summary>Nothing to keep.</summary>
    Empty,

    /// <summary>Longer than <see cref="MemoryFact.MostCharacters"/>.</summary>
    TooLong,

    /// <summary>It has the shape of a key, a password or a card number.</summary>
    LooksLikeSecret,

    /// <summary>The memory holds <see cref="MemoryBook.MostFacts"/> facts.</summary>
    Full,

    /// <summary>The store could not be used.</summary>
    Unavailable,
}
