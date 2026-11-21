using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Calculator;

/// <summary>
/// The tool <c>calculate</c> (ADR 0017, section 3): an arithmetic expression computed by code, with no script
/// execution. Class « Lecture », enabled by default.
/// </summary>
public sealed class CalculatorTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "calculate";

    private static readonly ResourceManager Texts = new(typeof(CalculatorTexts));

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <summary>The schema names its parameter in English; a model reads the names of JSON fields well in any language (spike E13).</summary>
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            expression = new { type = "string", description = Texts.GetString("Expression", CultureInfo.InvariantCulture) },
        },
        required = new[] { "expression" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Description", language.Culture) ?? string.Empty;
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Read;

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("expression", out var expression)
            || expression.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
        {
            throw new ToolException("The parameter 'expression' is missing.");
        }

        var text = expression.ValueKind == JsonValueKind.Number ? expression.GetRawText() : expression.GetString() ?? string.Empty;
        return ValueTask.FromResult(ArithmeticExpression.Format(ArithmeticExpression.Evaluate(text)));
    }
}

/// <summary>Marker of the resources <c>CalculatorTexts.resx</c> and <c>CalculatorTexts.fr.resx</c>.</summary>
public sealed class CalculatorTexts
{
    private CalculatorTexts()
    {
    }
}
