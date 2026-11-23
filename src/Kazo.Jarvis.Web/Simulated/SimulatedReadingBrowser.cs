using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// The browser of Jarvis with fixed pages, in place of WebView2 (ADR 0022, section 5): nothing leaves the test
/// machine. A query that holds « captcha » is answered as a blocked search. It records what it was asked, for the
/// Playwright tests.
/// </summary>
public sealed class SimulatedReadingBrowser : IReadingBrowser
{
    /// <summary>The site of the fixed pages; the name never resolves.</summary>
    public const string Site = "https://pages.jarvis.example";

    private static readonly Dictionary<string, (string Title, string Text)> Pages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/meteo"] = ("Météo de Montréal", "Montréal, ce soir : ciel dégagé, moins deux degrés. Demain : neige légère en après-midi."),
        ["/crepes"] = ("Recette de crêpes", "Mélanger 250 g de farine, 4 œufs et 500 ml de lait. Laisser reposer une heure, puis cuire à feu moyen."),
        ["/long"] = ("Longue page", string.Concat(Enumerable.Range(1, 300).Select(line => $"Paragraphe {line} de la longue page. "))),
    };

    private readonly IWebAddressPolicy policy;
    private readonly Lock gate = new();
    private readonly List<string> searches = [];
    private readonly List<string> shown = [];

    /// <summary>Creates the browser.</summary>
    /// <param name="policy">The addresses that may be read, asked as the real browser asks it.</param>
    public SimulatedReadingBrowser(IWebAddressPolicy policy)
    {
        this.policy = policy;
    }

    /// <summary>Raised after a search, a page shown or the data erased, on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>The queries searched, oldest first, each with its engine: <c>DuckDuckGo: météo</c>.</summary>
    public IReadOnlyList<string> Searches
    {
        get
        {
            lock (gate)
            {
                return [.. searches];
            }
        }
    }

    /// <summary>The pages the user asked to see, oldest first; an empty text for the current page.</summary>
    public IReadOnlyList<string> Shown
    {
        get
        {
            lock (gate)
            {
                return [.. shown];
            }
        }
    }

    /// <summary>How many times the data was erased.</summary>
    public int Cleared { get; private set; }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, WebSearchEngine engine, int most, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            searches.Add($"{engine}: {query}");
        }

        Changed?.Invoke(this, EventArgs.Empty);
        if (query.Contains("captcha", StringComparison.OrdinalIgnoreCase))
        {
            throw new WebReadException(WebReadFailure.Blocked);
        }

        IReadOnlyList<WebSearchResult> results =
        [
            new("Météo de Montréal", new Uri(Site + "/meteo"), $"Résultat simulé pour « {query} » : ciel dégagé ce soir."),
            new("Recette de crêpes", new Uri(Site + "/crepes"), "Farine, œufs, lait : la recette de base."),
            new("Longue page", new Uri(Site + "/long"), "Une page plus longue que ce qu'un appel rend."),
        ];
        return [.. results.Take(most)];
    }

    /// <inheritdoc />
    public async ValueTask<WebPage> ReadAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        if (!policy.Allows(address))
        {
            throw new WebReadException(WebReadFailure.AddressRefused);
        }

        if (!address.AbsoluteUri.StartsWith(Site, StringComparison.OrdinalIgnoreCase) || !Pages.TryGetValue(address.AbsolutePath, out var page))
        {
            throw new WebReadException(WebReadFailure.Unreachable);
        }

        return new WebPage(address, page.Title, page.Text);
    }

    /// <inheritdoc />
    public ValueTask ShowAsync(Uri? address = null, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            shown.Add(address?.AbsoluteUri ?? string.Empty);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearDataAsync(CancellationToken cancellationToken = default)
    {
        Cleared++;
        Changed?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }
}
