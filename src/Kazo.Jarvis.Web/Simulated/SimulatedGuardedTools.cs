using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Two tools that exist only in the Web host, to show the confirmation request (ADR 0023) before any real tool
/// needs one: <c>write_note</c> is "Sensitive", <c>erase_notes</c> is "Critical". They keep their notes in memory
/// and touch nothing else.
/// </summary>
public sealed class SimulatedNotes
{
    private readonly Lock gate = new();
    private readonly List<string> notes = [];

    /// <summary>Raised after the notes changed, on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>The notes written, oldest first.</summary>
    public IReadOnlyList<string> Notes
    {
        get
        {
            lock (gate)
            {
                return [.. notes];
            }
        }
    }

    /// <summary>How many times the notes were erased.</summary>
    public int Erased { get; private set; }

    internal void Add(string note)
    {
        lock (gate)
        {
            notes.Add(note);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Clear()
    {
        lock (gate)
        {
            notes.Clear();
            Erased++;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The simulated "Sensitive" tool: writes a note in memory.</summary>
/// <param name="notes">Where the notes go.</param>
public sealed class SimulatedNoteTool(SimulatedNotes notes) : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "write_note";

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Sensitive;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { text = new { type = "string" } },
        required = new[] { "text" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language) => "Simulated: writes a note. Needs a confirmation.";

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Sensitive;

    /// <inheritdoc />
    public string? Summarize(JsonElement arguments, AssistantLanguage language) =>
        language == AssistantLanguage.French
            ? $"Jarvis veut écrire la note « {TextOf(arguments)} »."
            : $"Jarvis wants to write the note \"{TextOf(arguments)}\".";

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        notes.Add(TextOf(arguments));
        return ValueTask.FromResult("note written");
    }

    private static string TextOf(JsonElement arguments) =>
        arguments.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() ?? string.Empty : string.Empty;
}

/// <summary>The simulated "Critical" tool: erases the notes in memory. Approved by a click only.</summary>
/// <param name="notes">The notes.</param>
public sealed class SimulatedEraseTool(SimulatedNotes notes) : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "erase_notes";

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Critical;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language) => "Simulated: erases every note. Needs a confirmation by a click.";

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Critical;

    /// <inheritdoc />
    public string? Summarize(JsonElement arguments, AssistantLanguage language) =>
        language == AssistantLanguage.French ? "Jarvis veut effacer toutes les notes." : "Jarvis wants to erase every note.";

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        notes.Clear();
        return ValueTask.FromResult("notes erased");
    }
}
