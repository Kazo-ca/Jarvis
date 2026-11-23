using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Tools.Servers;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>
/// A tool of a tool server in the registry (ADR 0018, sections 3 and 4): the name, the description and the schema
/// of the server as they are, off until the user turns it on, and a class that the server can only lower to
/// « Lecture » by declaring the tool read-only. Everything else asks for a confirmation at each call.
/// </summary>
internal sealed class McpTool : ITool
{
    private static readonly System.Resources.ResourceManager Texts = new(typeof(McpTexts));

    private readonly McpServerSession session;
    private readonly ISettingsStore settings;
    private readonly IToolServerClassifier? classifier;

    public McpTool(McpServerSession session, ISettingsStore settings, ToolServerTool tool, string name, IToolServerClassifier? classifier = null)
    {
        this.classifier = classifier;
        this.session = session;
        this.settings = settings;
        ServerTool = tool;
        Name = name;
        Fingerprint = McpToolFingerprint.Of(tool);
    }

    /// <summary>The tool as its server describes it.</summary>
    public ToolServerTool ServerTool { get; }

    /// <summary>The fingerprint of <see cref="ServerTool"/>.</summary>
    public string Fingerprint { get; }

    /// <summary>The name of the server in the settings.</summary>
    public string Server => session.Name;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Origin => ToolOrigins.Mcp(session.Name);

    /// <summary>
    /// The tools of a Home Assistant server go to the home automation agent (ADR 0025, section 3); those of any
    /// other server to the agent chosen on the page of the server, the Jarvis agent by default. Read from the
    /// settings at each ask: the choice changes without a new connection.
    /// </summary>
    public string? Agent => session.Options.Kind == McpServerKind.HomeAssistant
        ? Kazo.Jarvis.Kernel.Agents.AgentIds.HomeAutomation
        : settings.Read(McpServersOptions.Section).Find(session.Name)?.Agent is { Length: > 0 } chosen ? chosen : null;

    /// <summary>Never on at its discovery (ADR 0018, section 3).</summary>
    public bool EnabledByDefault => false;

    /// <summary>What it returns comes from outside the conversation (ADR 0019, section 3).</summary>
    public bool ReadsOutsideContent => true;

    /// <summary>The lowest class a call may have, raised by the user's choice if there is one.</summary>
    public ActionClass DeclaredClass => Higher(ServerClass, Raised);

    /// <summary>
    /// The class before any choice of the user. By the general rule, a declared read is a read and the rest is
    /// sensitive. A server whose kind has its own classification gives the lowest class of the tool.
    /// </summary>
    public ActionClass ServerClass => classifier?.LowestClass(ServerTool) ?? GeneralClass;

    private ActionClass GeneralClass => ServerTool.ReadOnly ? ActionClass.Read : ActionClass.Sensitive;

    /// <summary>The class the user raised the tool to, or « Lecture » when there is no such choice.</summary>
    private ActionClass Raised =>
        settings.Read(McpServersOptions.Section).Find(session.Name)?.Classes?.GetValueOrDefault(ServerTool.Name, ActionClass.Read) ?? ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema => ServerTool.Schema;

    /// <summary>The description of the server, in its own language whatever the language of the turn.</summary>
    public string Describe(AssistantLanguage language) => ServerTool.Description;

    /// <inheritdoc />
    /// <summary>Without a question to the server, only the general rule is sure.</summary>
    public ActionClass Classify(JsonElement arguments) => Higher(GeneralClass, Raised);

    /// <summary>
    /// The class of one call: by the classification of the kind of the server when it has one (ADR 0018, section 5),
    /// never under what the user chose.
    /// </summary>
    public async ValueTask<ActionClass> ClassifyAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (classifier is null)
        {
            return Classify(arguments);
        }

        // Classed on the arguments that will be sent (the names matched to the ones of the server), not on what the
        // model wrote: the class and the call always speak of the same devices.
        var call = Call(await ResolvedAsync(arguments, cancellationToken).ConfigureAwait(false));
        return Higher(await classifier.ClassifyAsync(call, cancellationToken).ConfigureAwait(false), Raised);
    }

    private ToolServerCall Call(JsonElement arguments) =>
        new(session.Name, ServerTool, arguments, (tool, asked, cancellation) => session.CallAsync(tool, asked, cancellation));

    private async ValueTask<JsonElement> ResolvedAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        classifier is null ? arguments : await classifier.ResolveAsync(Call(arguments), cancellationToken).ConfigureAwait(false);

    private static ActionClass Higher(ActionClass first, ActionClass second) => first > second ? first : second;

    /// <inheritdoc />
    public string? Discloses(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return session.Options.ByCommand
            ? string.Format(language.Culture, Texts.GetString("Discloses_Command", language.Culture) ?? "{0}", session.Options.Command)
            : string.Format(language.Culture, Texts.GetString("Discloses_Address", language.Culture) ?? "{0}", session.Options.Uri?.GetLeftPart(UriPartial.Path));
    }

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ToolServerResult result;
        try
        {
            arguments = await ResolvedAsync(arguments, cancellationToken).ConfigureAwait(false);
            result = await session.CallAsync(ServerTool.Name, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (ToolServerException exception)
        {
            throw new ToolException(exception.Failure switch
            {
                ToolServerFailure.Rejected => $"The tool server '{session.Name}' refused the token of Jarvis.",
                ToolServerFailure.TimedOut => $"The tool server '{session.Name}' took too long to answer.",
                ToolServerFailure.CommandNotFound => $"The program of the tool server '{session.Name}' could not be started.",
                ToolServerFailure.Closed => $"The tool server '{session.Name}' stopped.",
                ToolServerFailure.Protocol => $"The tool server '{session.Name}' gave an answer that could not be understood.",
                _ => $"The tool server '{session.Name}' cannot be reached.",
            }, exception);
        }

        if (result.IsError)
        {
            var failure = result.Text.Length == 0 ? "The tool reported a failure without a message." : result.Text;
            throw new ToolException(await HintAsync(arguments, cancellationToken).ConfigureAwait(false) is { Length: > 0 } hint ? failure + " " + hint : failure);
        }

        return result.Text;
    }

    /// <summary>What the kind of the server adds to a failure; a hint that cannot be had is no reason to hide the failure.</summary>
    private async ValueTask<string?> HintAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (classifier is null)
        {
            return null;
        }

        try
        {
            return await classifier.HintAsync(Call(arguments), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ToolServerException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>Anchor of the texts of the tools of the tool servers (<c>McpTexts.resx</c>).</summary>
public sealed class McpTexts
{
    private McpTexts()
    {
    }
}
