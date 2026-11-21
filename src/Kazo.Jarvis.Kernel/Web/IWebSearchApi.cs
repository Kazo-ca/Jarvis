using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Kernel.Web;

/// <summary>
/// A web search by an API with a key (ADR 0017, section 3): Brave Search, used as soon as the user put a key in the
/// vault. The key is read from the vault by the adapter, never by a slice.
/// </summary>
public interface IWebSearchApi
{
    /// <summary>Searches the web.</summary>
    /// <param name="query">The words to search.</param>
    /// <param name="most">The most results returned.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The results, best first.</returns>
    /// <exception cref="WebReadException">The search failed; <see cref="WebReadFailure.KeyRejected"/> when the key is missing or refused.</exception>
    ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int most, CancellationToken cancellationToken = default);
}

/// <summary>What the search by API keeps in the vault.</summary>
public static class WebSearchApiSecrets
{
    /// <summary>The reference of the Brave Search key (ADR 0004, section 4).</summary>
    public static SecretRef BraveKey { get; } = new("tools.web.brave.apikey");
}
