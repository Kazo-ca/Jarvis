using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Web;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters.Web;

/// <summary>
/// Brave Search by its API (ADR 0017, section 3): used as soon as the user put a key in the vault. Only the words of
/// the search leave the machine, with the key in a header; neither is ever logged.
/// </summary>
public sealed partial class BraveSearchApi : IWebSearchApi
{
    private static readonly Uri Endpoint = new("https://api.search.brave.com/res/v1/web/search");

    private readonly HttpClient http;
    private readonly ISecretVault? vault;
    private readonly ILogger logger;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The HTTP client; its timeout bounds one search.</param>
    /// <param name="vault">The vault that holds the key.</param>
    /// <param name="logger">The logger.</param>
    public BraveSearchApi(HttpClient http, ISecretVault? vault, ILogger<BraveSearchApi>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        this.vault = vault;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BraveSearchApi>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int most, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        SecretValue? key;
        try
        {
            key = vault is null ? null : await vault.GetAsync(WebSearchApiSecrets.BraveKey, cancellationToken).ConfigureAwait(false);
        }
        catch (SecretVaultException exception)
        {
            throw new WebReadException(WebReadFailure.KeyRejected, exception);
        }

        if (key is null)
        {
            throw new WebReadException(WebReadFailure.KeyRejected);
        }

        var address = new Uri($"{Endpoint}?q={Uri.EscapeDataString(query)}&count={Math.Clamp(most, 1, 20)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("X-Subscription-Token", key.Reveal());

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new WebReadException(WebReadFailure.KeyRejected);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // The quota of the key, or its rate: Jarvis does not retry on its own.
                throw new WebReadException(WebReadFailure.Blocked);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogStatus((int)response.StatusCode);
                throw new WebReadException(WebReadFailure.Unreachable);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Read(document.RootElement, most);
        }
        catch (HttpRequestException exception)
        {
            throw new WebReadException(WebReadFailure.Unreachable, exception);
        }
        catch (JsonException exception)
        {
            throw new WebReadException(WebReadFailure.NothingRead, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WebReadException(WebReadFailure.TimedOut, exception);
        }
    }

    /// <summary>The results of an answer of the API: <c>web.results[]</c> with <c>title</c>, <c>url</c> and <c>description</c>.</summary>
    internal static IReadOnlyList<WebSearchResult> Read(JsonElement root, int most)
    {
        var results = new List<WebSearchResult>();
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("web", out var web) || web.ValueKind != JsonValueKind.Object
            || !web.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (results.Count >= most)
            {
                break;
            }

            if (item.ValueKind == JsonValueKind.Object
                && Text(item, "url") is { } url
                && Uri.TryCreate(url, UriKind.Absolute, out var address)
                && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps))
            {
                results.Add(new WebSearchResult(Plain(Text(item, "title") ?? address.Host), address, Plain(Text(item, "description") ?? string.Empty)));
            }
        }

        return results;
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The API marks the words found with tags and writes some characters as HTML entities.</summary>
    private static string Plain(string text) => WebUtility.HtmlDecode(Tag().Replace(text, string.Empty)).Trim();

    [GeneratedRegex("<[^>]{1,40}>")]
    private static partial Regex Tag();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Brave Search answered with status {Status}.")]
    private partial void LogStatus(int status);
}
