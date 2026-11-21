using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Features.Tools.Web;

/// <summary>
/// The tool <c>fetch_url</c> (ADR 0017, section 3; ADR 0022): the text of a web page, read in the browser of Jarvis.
/// Class « Lecture » ; an address of this machine or of the local network is « Interdite ». Disabled by default:
/// the address leaves the machine. What it returns is data (ADR 0009, principle 2).
/// </summary>
public sealed class FetchUrlTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "fetch_url";

    /// <summary>What the JSON around the text takes: title, positions, field names.</summary>
    private const int Overhead = 200;

    private static readonly ResourceManager Texts = new(typeof(WebToolsTexts));

    private readonly ISettingsStore settings;
    private readonly IWebAddressPolicy policy;
    private readonly IReadingBrowser? browser;

    /// <summary>Creates the tool.</summary>
    /// <param name="settings">The settings, for <see cref="WebToolsOptions.Section"/>.</param>
    /// <param name="policy">The addresses that may be read.</param>
    /// <param name="browser">The browser of Jarvis; <see langword="null"/> on a host that has none.</param>
    public FetchUrlTool(ISettingsStore settings, IWebAddressPolicy policy, IReadingBrowser? browser = null)
    {
        this.settings = settings;
        this.policy = policy;
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
            url = new { type = "string", description = Texts.GetString("Fetch_Url", CultureInfo.InvariantCulture) },
            start = new { type = "integer", description = Texts.GetString("Fetch_Start", CultureInfo.InvariantCulture) },
        },
        required = new[] { "url" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Fetch_Description", language.Culture) ?? string.Empty;
    }

    /// <inheritdoc />
    public string? Discloses(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Fetch_Discloses", language.Culture);
    }

    /// <summary>
    /// An address that cannot be read as one is « Lecture » : the call then fails with a sentence the model can use.
    /// A readable address the policy refuses is « Interdite » : denied and logged.
    /// </summary>
    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) =>
        WebToolSupport.Address(WebToolSupport.Text(arguments, "url")) is { } address && !policy.Allows(address)
            ? ActionClass.Forbidden
            : ActionClass.Read;

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (WebToolSupport.Address(WebToolSupport.Text(arguments, "url")) is not { } address)
        {
            throw new ToolException("The parameter 'url' is missing or is not an address.");
        }

        // Checked again here: the settings may have changed since the decision.
        if (!policy.Allows(address))
        {
            throw WebToolSupport.Failed(new WebReadException(WebReadFailure.AddressRefused));
        }

        if (browser is null)
        {
            throw WebToolSupport.Failed(new WebReadException(WebReadFailure.Unavailable));
        }

        WebPage page;
        try
        {
            page = await browser.ReadAsync(address, cancellationToken).ConfigureAwait(false);
        }
        catch (WebReadException exception)
        {
            throw WebToolSupport.Failed(exception);
        }

        var options = settings.Read(WebToolsOptions.Section);
        var title = page.Title.Length > 120 ? page.Title[..120] : page.Title;
        var finalAddress = page.Address.AbsoluteUri;

        // JSON doubles some characters (quotes, new lines): three quarters of the room is a safe share for the text.
        var most = Math.Max(100, (options.ResultCharacters - Overhead - title.Length - finalAddress.Length) * 3 / 4);
        var start = Math.Clamp(WebToolSupport.Integer(arguments, "start", 0), 0, page.Text.Length);
        var text = page.Text.Substring(start, Math.Min(most, page.Text.Length - start));
        var end = start + text.Length;
        return JsonSerializer.Serialize(new
        {
            url = finalAddress,
            title,
            start,
            total_characters = page.Text.Length,
            next_start = end < page.Text.Length ? end : (int?)null,
            text,
        }, WebToolSupport.Json);
    }
}

/// <summary>Marker of the resources <c>WebToolsTexts.resx</c> and <c>WebToolsTexts.fr.resx</c>.</summary>
public sealed class WebToolsTexts
{
    private WebToolsTexts()
    {
    }
}
