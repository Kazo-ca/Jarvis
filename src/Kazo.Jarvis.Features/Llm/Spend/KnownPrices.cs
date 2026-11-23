using Kazo.Jarvis.Kernel.Llm;

namespace Kazo.Jarvis.Features.Llm.Spend;

/// <summary>
/// The prices known when this version of Jarvis was published (ADR 0020, section 6): proposed on the page, never
/// fetched from the network. They age with the application; the user corrects them.
/// </summary>
public static class KnownPrices
{
    /// <summary>The date of the price list below.</summary>
    public static readonly DateOnly AsOf = new(2026, 11, 3);

    // Anthropic's first-party API rates, US dollars per million tokens, by start of the model id. The longest
    // start wins: « claude-opus-5-5 » before « claude-opus-5 ».
    private static readonly (string Start, ProviderPrice Price)[] Anthropic =
    [
        ("claude-fable-5-2", new(8m, 40m)),
        ("claude-fable-5", new(10m, 50m)),
        ("claude-opus-5-5", new(4m, 20m)),
        ("claude-opus-5", new(5m, 25m)),
        ("claude-opus-4-8", new(5m, 25m)),
        ("claude-opus-4-7", new(5m, 25m)),
        ("claude-opus-4-6", new(5m, 25m)),
        ("claude-sonnet-5", new(2m, 10m)),
        ("claude-sonnet-4-6", new(3m, 15m)),
        ("claude-haiku-5-5", new(0.10m, 0.50m)),
        ("claude-haiku-4-5", new(1m, 5m)),
    ];

    /// <summary>The known price of the model of <paramref name="provider"/>.</summary>
    /// <param name="provider">A provider.</param>
    /// <returns>The price, or <see langword="null"/> when this version knows none: the user types it.</returns>
    public static ProviderPrice? For(LlmProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider.Kind != LlmProviderKind.Anthropic)
        {
            return null;
        }

        var model = provider.Model.Trim().ToLowerInvariant();
        return Anthropic
            .Where(known => model.StartsWith(known.Start, StringComparison.Ordinal))
            .OrderByDescending(known => known.Start.Length)
            .Select(known => known.Price)
            .FirstOrDefault();
    }
}
