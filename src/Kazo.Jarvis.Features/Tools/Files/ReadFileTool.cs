using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Files;

/// <summary>
/// The tool <c>read_file</c> (ADR 0017, section 3): the text of a file of an allowed folder, by parts. Class
/// « Lecture » inside an allowed folder, « Interdite » outside and for the session keys and IDE locks of Claude
/// Code. What it returns is data (ADR 0009, principle 2).
/// </summary>
public sealed class ReadFileTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "read_file";

    /// <summary>What the JSON around the text takes: path, positions, field names.</summary>
    private const int Overhead = 160;

    private static readonly ResourceManager Texts = new(typeof(FileToolsTexts));

    private readonly AllowedFolders allowed;
    private readonly ILocalFiles files;

    /// <summary>Creates the tool.</summary>
    /// <param name="allowed">The allowed folders.</param>
    /// <param name="files">Reads the files.</param>
    public ReadFileTool(AllowedFolders allowed, ILocalFiles files)
    {
        this.allowed = allowed;
        this.files = files;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <summary>What it returns comes from outside the conversation (ADR 0019, section 3).</summary>
    public bool ReadsOutsideContent => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = Texts.GetString("Read_Path", CultureInfo.InvariantCulture) },
            start = new { type = "integer", description = Texts.GetString("Read_Start", CultureInfo.InvariantCulture) },
        },
        required = new[] { "path" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Read_Description", language.Culture) ?? string.Empty;
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) =>
        FileToolSupport.ClassOf(allowed.Check(FileToolSupport.Text(arguments, "path")));

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        // Checked again here: the settings may have changed since the decision, and nothing outside is ever read.
        var target = allowed.Check(FileToolSupport.Text(arguments, "path"));
        if (target is not { Verdict: PathVerdict.Allowed, Resolved: { } file })
        {
            throw new ToolException(FileToolSupport.Text(arguments, "path") is null
                ? "The parameter 'path' is missing."
                : "This path is outside the allowed folders.");
        }

        var start = Math.Max(0, FileToolSupport.Integer(arguments, "start", 0));

        // JSON doubles some characters (quotes, new lines): three quarters of the room is a safe share for the text.
        var most = Math.Max(100, (allowed.ResultCharacters - Overhead - file.Length) * 3 / 4);
        FileExcerpt excerpt;
        try
        {
            excerpt = await files.ReadTextAsync(file, start, most, cancellationToken).ConfigureAwait(false);
        }
        catch (LocalFileException exception)
        {
            throw FileToolSupport.Failed(exception);
        }

        var end = excerpt.Start + excerpt.Text.Length;
        return JsonSerializer.Serialize(new
        {
            path = file,
            start = excerpt.Start,
            total_characters = excerpt.TotalCharacters,
            next_start = end < excerpt.TotalCharacters ? end : (int?)null,
            text = excerpt.Text,
        }, FileToolSupport.Json);
    }
}
