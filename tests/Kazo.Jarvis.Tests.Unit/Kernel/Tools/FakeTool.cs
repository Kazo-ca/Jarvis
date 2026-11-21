using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Tools;

/// <summary>A tool the test steers: its class, its switch, what it returns and how its classifier behaves.</summary>
internal sealed class FakeTool(string name) : ITool
{
    public string Name { get; } = name;

    public string Origin { get; init; } = ToolOrigins.BuiltIn;

    public bool EnabledByDefault { get; init; } = true;

    public ActionClass DeclaredClass { get; init; } = ActionClass.Read;

    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new { type = "object", properties = new { text = new { type = "string" } } });

    /// <summary>The class of a call; the declared one when not set.</summary>
    public Func<JsonElement, ActionClass>? Classifier { get; init; }

    /// <summary>What the tool does; it returns <c>ok</c> when not set.</summary>
    public Func<JsonElement, CancellationToken, ValueTask<string>>? Run { get; init; }

    /// <summary>The arguments of the calls that ran.</summary>
    public Func<JsonElement, AssistantLanguage, string?>? Summary { get; init; }

    public List<string> Invoked { get; } = [];

    public string Describe(AssistantLanguage language) => $"{Name} ({language.Code})";

    public string? Summarize(JsonElement arguments, AssistantLanguage language) => Summary?.Invoke(arguments, language);

    public ActionClass Classify(JsonElement arguments) => Classifier?.Invoke(arguments) ?? DeclaredClass;

    /// <summary>The class of a call found after a wait; <see cref="Classify"/> when not set.</summary>
    public Func<JsonElement, CancellationToken, ValueTask<ActionClass>>? AsyncClassifier { get; init; }

    public ValueTask<ActionClass> ClassifyAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        AsyncClassifier?.Invoke(arguments, cancellationToken) ?? new ValueTask<ActionClass>(Classify(arguments));

    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        lock (Invoked)
        {
            Invoked.Add(arguments.GetRawText());
        }

        return Run?.Invoke(arguments, cancellationToken) ?? ValueTask.FromResult("ok");
    }
}
