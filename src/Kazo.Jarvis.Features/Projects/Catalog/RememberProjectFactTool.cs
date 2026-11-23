using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Projects.Catalog;

/// <summary>
/// The tool <c>remember_project_fact</c> (ADR 0025, section 6; answer of the maintainer of 2026-11-23): « retiens
/// pour ce projet que… ». It writes one line at the end of the memory file of the project, in the folder of the
/// user: class « Sensible », so the user confirms each time (ADR 0023). Refused once the turn holds a content
/// read from outside: its text comes from the model, which may repeat what it read.
/// </summary>
public sealed class RememberProjectFactTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "remember_project_fact";

    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly ProjectCatalog catalog;

    /// <summary>Creates the tool.</summary>
    /// <param name="catalog">The projects.</param>
    public RememberProjectFactTool(ProjectCatalog catalog) => this.catalog = catalog;

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Sensitive;

    /// <inheritdoc />
    public bool RefusedAfterOutsideContent => true;

    /// <inheritdoc />
    public JsonElement Schema => JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["project"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = catalog.Projects.Select(project => project.Name).ToArray(),
                ["description"] = "The project the fact is about.",
            },
            ["text"] = new { type = "string", description = "The fact, in one sentence." },
        },
        ["required"] = new[] { "project", "text" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return ProjectCatalog.Text("Remember_Description", language, Kazo.Jarvis.Kernel.Projects.IProjectCatalog.MemoryFileName);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Sensitive;

    /// <inheritdoc />
    public string? Summarize(JsonElement arguments, AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return catalog.Find(Text(arguments, "project")) is { } project && Text(arguments, "text") is { Length: > 0 } text
            ? ProjectCatalog.Text("Remember_Summary", language, project.Name, text.Trim())
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (catalog.Find(Text(arguments, "project")) is not { } project)
        {
            throw new ToolException("No project has this name: ask the user which project.");
        }

        return await catalog.RememberAsync(project, Text(arguments, "text"), cancellationToken).ConfigureAwait(false) switch
        {
            ProjectFactOutcome.Added => JsonSerializer.Serialize(new { remembered = true, project = project.Name }, Json),
            ProjectFactOutcome.AlreadyKnown => JsonSerializer.Serialize(new { remembered = true, already_known = true, project = project.Name }, Json),
            ProjectFactOutcome.Empty => throw new ToolException("The fact is missing."),
            ProjectFactOutcome.TooLong => throw new ToolException($"The fact is longer than {ProjectCatalog.MostFactCharacters} characters: say it in one short sentence."),
            ProjectFactOutcome.LooksLikeSecret => throw new ToolException("This looks like a password, a key or a card number: Jarvis never keeps one."),
            _ => throw new ToolException("The memory file of the project cannot be written right now."),
        };
    }

    private static string? Text(JsonElement arguments, string property) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
