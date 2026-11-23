namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>
/// The monthly spending cap on the cloud providers (US2.8, ADR 0020, section 6). The spending slice keeps the
/// prices, the cap and the count; whoever is about to call a cloud provider asks it first, and the pages that
/// show the count read it here. Without an implementation nothing is counted and nothing is capped.
/// </summary>
public interface ISpendCap
{
    /// <summary>Raised after the count, the cap, a price or the month changed; on any thread.</summary>
    event Action? Changed;

    /// <summary>The count of the current month and the cap.</summary>
    SpendStatus Status { get; }

    /// <summary>Whether a call to <paramref name="provider"/> may be made now. Asked by code before each call.</summary>
    /// <param name="provider">The provider about to be called.</param>
    /// <returns><see cref="SpendVerdict.Allowed"/> for a local provider, and for a cloud one that has a price while the cap is not reached.</returns>
    SpendVerdict Check(LlmProvider provider);

    /// <summary>
    /// What must be told to the user about the cap, once: 80 % of it was passed, or it was reached. Taking it
    /// removes it.
    /// </summary>
    /// <returns>The notice, or <see langword="null"/>.</returns>
    SpendNotice? TakeNotice();

    /// <summary>
    /// What a Claude Code session may cost at most (ADR 0021, section 1): <paramref name="wanted"/> while
    /// « Claude Code est payé par abonnement » is checked; otherwise never more than what is left of the cap.
    /// </summary>
    /// <param name="wanted">The cap of a session chosen by the user, in US dollars.</param>
    /// <returns>The budget of the session; zero when the cap is reached and Claude Code counts.</returns>
    decimal SessionBudget(decimal wanted) => wanted;
}

/// <summary>The answer of <see cref="ISpendCap.Check"/>.</summary>
public enum SpendVerdict
{
    /// <summary>The call may be made.</summary>
    Allowed,

    /// <summary>The cloud provider has no price: its cost cannot be counted, so it is not called.</summary>
    NoPrice,

    /// <summary>The cap of the month is reached: no call until next month or until the user raises it.</summary>
    CapReached,
}

/// <summary>What the user is told once about the cap.</summary>
public enum SpendNotice
{
    /// <summary>80 % of the cap was passed this month.</summary>
    NearCap,

    /// <summary>The cap was reached: Jarvis stays local.</summary>
    CapReached,
}

/// <summary>The count of a month. An estimate from the tokens and the prices typed: the bill of the provider is what counts.</summary>
/// <param name="Month">The first day of the calendar month, local time.</param>
/// <param name="SpentUsd">What the answers of the cloud providers cost this month, in US dollars.</param>
/// <param name="CapUsd">The monthly cap, in US dollars.</param>
public sealed record SpendStatus(DateOnly Month, decimal SpentUsd, decimal CapUsd)
{
    /// <summary>The share of the cap at which the user is warned.</summary>
    public const decimal WarningShare = 0.8m;

    /// <summary>Whether the cap is reached.</summary>
    public bool Reached => SpentUsd >= CapUsd;
}
