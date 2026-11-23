using System.Globalization;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.ClaudeCode.Announcements;

/// <summary>
/// The tool <c>start_quiet_period</c> (ADR 0021, section 9, « je vais dormir »): Jarvis stops announcing aloud for
/// a few hours. Class « Effet courant »: the user said yes to the offer of the model before it is called, and the
/// period changes no setting and ends by itself. The description tells the model to make the offer when the user
/// says they go to sleep, only while no quiet hours are set.
/// </summary>
public sealed class QuietPeriodTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "start_quiet_period";

    private readonly QuietTime quiet;
    private readonly TimeProvider time;

    /// <summary>Creates the tool.</summary>
    /// <param name="quiet">The keeper of the silence.</param>
    /// <param name="time">The time zone of the PC, to tell when the period ends.</param>
    public QuietPeriodTool(QuietTime quiet, TimeProvider time)
    {
        this.quiet = quiet;
        this.time = time;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.EverydayEffect;

    /// <summary>
    /// A page or a file the model just read must not be able to make Jarvis silent: after such a content, the tool
    /// is refused (ADR 0019, section 3).
    /// </summary>
    public bool RefusedAfterOutsideContent => true;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            hours = new { type = "number", minimum = 1, maximum = QuietTime.MostHours, description = "Length of the quiet period in hours. 8 when the user gave none." },
        },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return SessionAnnouncer.Text(quiet.HasHours ? "QuietTool_WithHours" : "QuietTool", language);
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.EverydayEffect;

    /// <inheritdoc />
    public string? Summarize(JsonElement arguments, AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return string.Format(CultureInfo.InvariantCulture, SessionAnnouncer.Text("QuietTool_Summary", language), Hours(arguments).ToString("0.#", language.Culture));
    }

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var end = quiet.Start(Hours(arguments));
        return ValueTask.FromResult(JsonSerializer.Serialize(new
        {
            quiet_until = TimeZoneInfo.ConvertTime(end, time.LocalTimeZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            note = "Nothing is announced aloud until then; announcements stay written in the conversation.",
        }));
    }

    private static double Hours(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty("hours", out var hours) || hours.ValueKind == JsonValueKind.Null)
        {
            return QuietTime.DefaultHours;
        }

        // A model may write the number as a text.
        var value = hours.ValueKind switch
        {
            JsonValueKind.Number when hours.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(hours.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
            _ => double.NaN,
        };
        return value is >= 1 and <= QuietTime.MostHours
            ? value
            : throw new ToolException($"hours goes from 1 to {QuietTime.MostHours}.");
    }
}
