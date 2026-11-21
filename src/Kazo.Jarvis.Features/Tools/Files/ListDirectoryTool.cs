using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Files;

/// <summary>
/// The tool <c>list_directory</c> (ADR 0017, section 3): what an allowed folder holds. Without a path it lists the
/// allowed folders, so a model learns where it may look. Class « Lecture » inside an allowed folder, « Interdite »
/// outside. Enabled by default: with no allowed folder it sees nothing.
/// </summary>
public sealed class ListDirectoryTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "list_directory";

    /// <summary>The most entries read from one folder.</summary>
    internal const int MostEntries = 200;

    private static readonly ResourceManager Texts = new(typeof(FileToolsTexts));

    private readonly AllowedFolders allowed;
    private readonly ILocalFiles files;

    /// <summary>Creates the tool.</summary>
    /// <param name="allowed">The allowed folders.</param>
    /// <param name="files">Reads the folders.</param>
    public ListDirectoryTool(AllowedFolders allowed, ILocalFiles files)
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
            path = new { type = "string", description = Texts.GetString("List_Path", CultureInfo.InvariantCulture) },
        },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("List_Description", language.Culture) ?? string.Empty;
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) =>
        FileToolSupport.Text(arguments, "path") is { } path ? FileToolSupport.ClassOf(allowed.Check(path)) : ActionClass.Read;

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (FileToolSupport.Text(arguments, "path") is not { } path)
        {
            return JsonSerializer.Serialize(new { allowed_folders = allowed.Roots }, FileToolSupport.Json);
        }

        // Checked again here: the settings may have changed since the decision, and nothing outside is ever read.
        var target = allowed.Check(path);
        if (target is not { Verdict: PathVerdict.Allowed, Resolved: { } folder })
        {
            throw new ToolException("This path is outside the allowed folders.");
        }

        FolderListing listing;
        try
        {
            listing = await files.ListAsync(folder, MostEntries, cancellationToken).ConfigureAwait(false);
        }
        catch (LocalFileException exception)
        {
            throw FileToolSupport.Failed(exception);
        }

        // Names are added while the result fits: a cut in the middle of the JSON would be unreadable.
        var budget = allowed.ResultCharacters - 120 - folder.Length;
        var folders = new List<string>();
        var fileEntries = new List<object>();
        var shown = 0;
        foreach (var entry in listing.Entries)
        {
            budget -= entry.Name.Length + (entry.IsFolder ? 4 : 30);
            if (budget < 0)
            {
                break;
            }

            if (entry.IsFolder)
            {
                folders.Add(entry.Name);
            }
            else
            {
                fileEntries.Add(new { name = entry.Name, bytes = entry.Size });
            }

            shown++;
        }

        return JsonSerializer.Serialize(new
        {
            path = folder,
            total = listing.Total,
            shown,
            folders,
            files = fileEntries,
        }, FileToolSupport.Json);
    }
}

/// <summary>Marker of the resources <c>FileToolsTexts.resx</c> and <c>FileToolsTexts.fr.resx</c>.</summary>
public sealed class FileToolsTexts
{
    private FileToolsTexts()
    {
    }
}
