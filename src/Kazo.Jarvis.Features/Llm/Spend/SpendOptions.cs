using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Llm.Spend;

/// <summary>The price of a provider: US dollars per million tokens (ADR 0020, section 6).</summary>
/// <param name="InputPerMillion">For a million tokens sent.</param>
/// <param name="OutputPerMillion">For a million tokens written.</param>
public sealed record ProviderPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    /// <summary>What an answer costs at this price.</summary>
    /// <param name="inputTokens">Tokens sent.</param>
    /// <param name="outputTokens">Tokens written.</param>
    /// <returns>The cost, in US dollars.</returns>
    public decimal Cost(long inputTokens, long outputTokens) =>
        ((Math.Max(0, inputTokens) * InputPerMillion) + (Math.Max(0, outputTokens) * OutputPerMillion)) / 1_000_000m;
}

/// <summary>The cap and the prices (US2.8), section <c>Llm.Spend</c> of <c>config.json</c>.</summary>
public sealed record SpendOptions
{
    /// <summary>The cap before the user chose (ADR 0020, answer 3).</summary>
    public const decimal DefaultCapUsd = 10m;

    /// <summary>The highest cap and the highest price that can be typed.</summary>
    public const decimal MostUsd = 1_000_000m;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<SpendOptions> Section { get; } = new("Llm.Spend", () => new SpendOptions(), Validate);

    /// <summary>The monthly cap, in US dollars, the billing currency of the APIs. Zero: no cloud call at all.</summary>
    public decimal MonthlyCapUsd { get; init; } = DefaultCapUsd;

    /// <summary>The price of each cloud provider, by provider id. A cloud provider without a price is never called.</summary>
    public IReadOnlyDictionary<string, ProviderPrice> Prices { get; init; } = new Dictionary<string, ProviderPrice>();

    /// <summary>The price typed for the provider <paramref name="providerId"/>.</summary>
    /// <param name="providerId">The id of a provider.</param>
    /// <returns>The price, or <see langword="null"/>.</returns>
    public ProviderPrice? PriceOf(string providerId) =>
        Prices is not null && Prices.TryGetValue(providerId, out var price) ? price : null;

    /// <summary>These options with <paramref name="price"/> for <paramref name="providerId"/>; <see langword="null"/> removes it.</summary>
    /// <param name="providerId">The id of a provider.</param>
    /// <param name="price">The price, or <see langword="null"/>.</param>
    /// <returns>The changed options.</returns>
    public SpendOptions WithPrice(string providerId, ProviderPrice? price)
    {
        var prices = new Dictionary<string, ProviderPrice>(Prices ?? new Dictionary<string, ProviderPrice>(), StringComparer.Ordinal);
        if (price is null)
        {
            prices.Remove(providerId);
        }
        else
        {
            prices[providerId] = price;
        }

        return this with { Prices = prices };
    }

    private static IReadOnlyList<string> Validate(SpendOptions value)
    {
        var problems = new List<string>();
        if (value.MonthlyCapUsd is < 0 or > MostUsd)
        {
            problems.Add($"The monthly cap goes from 0 to {MostUsd} US dollars.");
        }

        foreach (var (id, price) in value.Prices ?? new Dictionary<string, ProviderPrice>())
        {
            if (price is null || price.InputPerMillion is < 0 or > MostUsd || price.OutputPerMillion is < 0 or > MostUsd)
            {
                problems.Add($"The price of provider '{id}' goes from 0 to {MostUsd} US dollars per million tokens.");
            }
        }

        return problems;
    }
}

/// <summary>
/// The count of the month, section <c>Llm.SpendCount</c> of <c>config.json</c>. Written by Jarvis, not by a page:
/// it is kept with the settings so that « Tout effacer », which empties the document store, does not empty the
/// count and lift the cap with it.
/// </summary>
public sealed record SpendCount
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<SpendCount> Section { get; } = new("Llm.SpendCount", () => new SpendCount(), Validate);

    /// <summary>The calendar month counted, <c>yyyy-MM</c>, local time; empty before the first answer.</summary>
    public string Month { get; init; } = string.Empty;

    /// <summary>What the cloud answers of <see cref="Month"/> cost, in US dollars.</summary>
    public decimal SpentUsd { get; init; }

    private static IReadOnlyList<string> Validate(SpendCount value) =>
        value.SpentUsd < 0 ? ["The count of the month cannot be negative."] : [];
}

/// <summary>One counted answer, kept in the collection <c>Llm.SpendEntries</c>: the detail behind the count. Never a word of what was said.</summary>
public sealed record SpendEntry
{
    /// <summary>When the answer ended.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>The id of the provider.</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>The name of the provider, as shown to the user.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>The model that answered.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Tokens sent.</summary>
    public long InputTokens { get; init; }

    /// <summary>Tokens written.</summary>
    public long OutputTokens { get; init; }

    /// <summary>The cost counted, in US dollars, at the price of that moment.</summary>
    public decimal CostUsd { get; init; }
}
