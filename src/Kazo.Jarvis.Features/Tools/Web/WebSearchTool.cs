using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Features.Tools.Web;

/// <summary>
/// The tool <c>web_search</c> (ADR 0017, section 3; ADR 0022): a web search, by Brave Search as soon as the user
/// put a key in the vault, otherwise in the browser of Jarvis, free and without a key. Class « Lecture ». Disabled
/// by default: the query leaves the machine. What it returns is data (ADR 0009, principle 2).
/// </summary>
public sealed class WebSearchTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "web_search";

    /// <summary>The longest query sent to a search service.</summary>
    internal const int MostQueryCharacters = 300;

    /// <summary>The longest snippet kept for one result.</summary>
    private const int MostSnippetCharacters = 220;

    private static readonly ResourceManager Texts = new(typeof(WebToolsTexts));

    private readonly ISettingsStore settings;
    private readonly ISecretVault? vault;
    private readonly IWebSearchApi? api;
    private readonly IReadingBrowser? browser;

    /// <summary>Creates the tool.</summary>
    /// <param name="settings">The settings, for <see cref="WebToolsOptions.Section"/>.</param>
    /// <param name="vault">The vault, to learn whether a Brave Search key exists; its value is never read here.</param>
    /// <param name="api">The search by API; <see langword="null"/> on a host that has none.</param>
    /// <param name="browser">The browser of Jarvis; <see langword="null"/> on a host that has none.</param>
    public WebSearchTool(ISettingsStore settings, ISecretVault? vault = null, IWebSearchApi? api = null, IReadingBrowser? browser = null)
    {
        this.settings = settings;
        this.vault = vault;
        this.api = api;
        this.browser = browser;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => false;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = Texts.GetString("Search_Query", CultureInfo.InvariantCulture) },
        },
        required = new[] { "query" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Search_Description", language.Culture) ?? string.Empty;
    }

    /// <inheritdoc />
    public string? Discloses(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Search_Discloses", language.Culture);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Read;

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (WebToolSupport.Text(arguments, "query") is not { } query)
        {
            throw new ToolException("The parameter 'query' is missing.");
        }

        if (query.Length > MostQueryCharacters)
        {
            query = query[..MostQueryCharacters];
        }

        var options = settings.Read(WebToolsOptions.Section);
        IReadOnlyList<WebSearchResult> results;
        string provider;
        try
        {
            if (api is not null && await HasBraveKeyAsync(cancellationToken).ConfigureAwait(false))
            {
                provider = "brave";
                results = await api.SearchAsync(query, options.Results, cancellationToken).ConfigureAwait(false);
            }
            else if (browser is not null)
            {
                provider = options.Engine.ToString().ToLowerInvariant();
                results = await browser.SearchAsync(query, options.Engine, options.Results, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new WebReadException(WebReadFailure.Unavailable);
            }
        }
        catch (WebReadException exception)
        {
            throw WebToolSupport.Failed(exception);
        }

        // Results are added while the whole fits: a cut in the middle of the JSON would be unreadable.
        var budget = options.ResultCharacters - 80 - query.Length;
        var kept = new List<object>();
        foreach (var result in results.Take(options.Results))
        {
            var title = result.Title.Length > 120 ? result.Title[..120] : result.Title;
            var snippet = result.Snippet.Length > MostSnippetCharacters ? result.Snippet[..MostSnippetCharacters] + "…" : result.Snippet;
            var address = result.Address.AbsoluteUri;
            budget -= title.Length + snippet.Length + address.Length + 40;
            if (budget < 0 && kept.Count > 0)
            {
                break;
            }

            kept.Add(new { title, url = address, snippet = snippet.Length == 0 ? null : snippet });
        }

        return JsonSerializer.Serialize(new { query, provider, results = kept }, WebToolSupport.Json);
    }

    /// <summary>Whether the user put a Brave Search key in the vault. A vault that cannot be read counts as no key.</summary>
    private async ValueTask<bool> HasBraveKeyAsync(CancellationToken cancellationToken)
    {
        if (vault is null)
        {
            return false;
        }

        try
        {
            return (await vault.ListAsync(cancellationToken).ConfigureAwait(false)).Contains(WebSearchApiSecrets.BraveKey);
        }
        catch (SecretVaultException)
        {
            return false;
        }
    }
}
