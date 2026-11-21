using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Clock;

/// <summary>
/// The tool <c>get_datetime</c> (ADR 0017, section 3): the date, the time and the time zone of the PC. Class
/// « Lecture », enabled by default: nothing leaves the machine.
/// </summary>
public sealed class ClockTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "get_datetime";

    private static readonly ResourceManager Texts = new(typeof(ClockTexts));

    private readonly TimeProvider time;

    /// <summary>Creates the tool.</summary>
    /// <param name="time">The clock and the time zone of the PC.</param>
    public ClockTool(TimeProvider time)
    {
        this.time = time;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { },
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
        // Plain data, in no language: the model words the answer in the language of the turn.
        var now = time.GetLocalNow();
        var offset = now.Offset;
        return ValueTask.FromResult(JsonSerializer.Serialize(new
        {
            date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            time = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            weekday = now.DayOfWeek.ToString(),
            time_zone = time.LocalTimeZone.Id,
            utc_offset = (offset < TimeSpan.Zero ? "-" : "+") + offset.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
        }));
    }
}

/// <summary>Marker of the resources <c>ClockTexts.resx</c> and <c>ClockTexts.fr.resx</c>.</summary>
public sealed class ClockTexts
{
    private ClockTexts()
    {
    }
}
