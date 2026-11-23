using System.Text.Json;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Llm.HandOff;

/// <summary>
/// The tool <c>hand_off_to_claude</c> (US2.5, ADR 0020, section 2), by which the local model hands a question
/// over. Its name is fixed, whatever the target; its description names it. Class « Lecture »: choosing the target
/// is the consent, there is no confirmation at each hand-off. It is in the registry only while a target is set.
/// <para>
/// It is a terminal tool: the conversation recognizes it by its name and runs the answer of the target itself,
/// said straight to the user. <see cref="InvokeAsync"/> is therefore never the way it runs.
/// </para>
/// </summary>
public sealed class HandOffTool : ITool
{
    /// <summary>The name given to the model (ADR 0007, spike E7).</summary>
    public const string ToolName = "hand_off_to_claude";

    private readonly Func<string?> target;

    /// <summary>Creates the tool.</summary>
    /// <param name="target">The name said for the target of the questions now, or <see langword="null"/>.</param>
    public HandOffTool(Func<string?> target)
    {
        this.target = target;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.HandOff;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            // « task » joins the list with the Claude Code sessions (ADR 0021).
            ["kind"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "question" } },
            ["reason"] = new { type = "string", description = "One short sentence told to the user: why you hand over." },
        },
        ["required"] = new[] { "kind", "reason" },
    });

    /// <summary>The kind and the reason of a call. A call without a kind is a question: it is the only use offered.</summary>
    /// <param name="arguments">The arguments, a JSON object.</param>
    /// <returns>The call, or <see langword="null"/> for an unknown kind or arguments that are not an object.</returns>
    public static HandOffCall? Read(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var kind = Text(arguments, "kind")?.Trim().ToLowerInvariant();
        var reason = Text(arguments, "reason")?.Trim();
        reason = string.IsNullOrEmpty(reason) ? null : reason.Length > 200 ? reason[..200] : reason;
        return kind switch
        {
            null or "" or "question" => new HandOffCall(HandOffKind.Question, reason),
            "task" => new HandOffCall(HandOffKind.Task, reason),
            _ => null,
        };
    }

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return HandOffTargets.Text("Tool_Description", language, target() ?? "Claude");
    }

    /// <inheritdoc />
    public string? Discloses(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return HandOffTargets.Text("Tool_Discloses", language, target() ?? "Claude");
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) =>
        Read(arguments) is null ? throw new ArgumentException("The kind of the hand-off is unknown.", nameof(arguments)) : ActionClass.Read;

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        throw new ToolException("A hand-off only happens inside a turn of the conversation.");

    private static string? Text(JsonElement arguments, string property) =>
        arguments.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
