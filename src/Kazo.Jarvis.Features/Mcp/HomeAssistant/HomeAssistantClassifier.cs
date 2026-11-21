using System.Text.Json;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools.Servers;

namespace Kazo.Jarvis.Features.Mcp.HomeAssistant;

/// <summary>
/// The class of a call to the MCP server of Home Assistant (ADR 0018, section 5), found by code after looking for
/// the devices the call aims at. The name of a tool says nothing: <c>HassTurnOff</c> unlocks a lock, and the
/// server marks everything that is not a read as destructive (spike E14).
/// <list type="number">
/// <item>A read is a read.</item>
/// <item>A script is an everyday effect, or critical when the user marked it « sécurité ».</item>
/// <item>
/// A tool that aims at entities is critical when one of the entities it designates is a security one, an everyday
/// effect otherwise. When the target cannot be found, it takes the highest class among every exposed entity its
/// <c>domain</c> and <c>device_class</c> filters do not exclude.
/// </item>
/// <item>Any other tool is an everyday effect when it is in the known list, sensitive otherwise.</item>
/// </list>
/// </summary>
public sealed class HomeAssistantClassifier : IToolServerClassifier
{
    private static readonly string[] Reads = ["GetLiveContext", "GetDateTime", "get_items"];
    private static readonly string[] EverydayTools = ["HassBroadcast", "HassCancelAllTimers", "HassListAddItem", "HassListCompleteItem", "HassListRemoveItem"];
    private static readonly string[] TargetParameters = ["name", "area", "floor", "domain", "device_class"];
    private static readonly string[] SecurityDomains = ["lock", "alarm_control_panel", "valve", "siren"];
    private static readonly string[] SecurityCovers = ["garage", "door", "gate"];
    private static readonly JsonElement NoArguments = JsonSerializer.SerializeToElement(new { });

    private readonly ISettingsStore settings;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private readonly Dictionary<string, (DateTimeOffset At, IReadOnlyList<HomeEntity> Entities)> kept = new(StringComparer.Ordinal);

    /// <summary>Creates the classifier.</summary>
    /// <param name="settings">The settings, for <see cref="HomeAssistantOptions.Section"/>.</param>
    /// <param name="time">The clock of the kept entities.</param>
    public HomeAssistantClassifier(ISettingsStore settings, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        this.time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => ToolServerKinds.HomeAssistant;

    /// <summary>Whether the tool is a script of Home Assistant: <c>script__…</c> or <c>intent_script__…</c>.</summary>
    /// <param name="tool">The name of the tool, on the server or in the registry.</param>
    /// <returns><see langword="true"/> for a script.</returns>
    public static bool IsScript(string tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.StartsWith("script__", StringComparison.Ordinal) || tool.StartsWith("intent_script__", StringComparison.Ordinal)
            || tool.Contains("__script__", StringComparison.Ordinal) || tool.Contains("__intent_script__", StringComparison.Ordinal);
    }

    /// <summary>Whether an entity is a security one by what it is: a lock, an alarm, a valve, a siren, a garage door, a door or a gate.</summary>
    /// <param name="entity">An exposed entity.</param>
    /// <returns><see langword="true"/> for a security entity.</returns>
    public static bool IsSecurityByNature(HomeEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return SecurityDomains.Contains(entity.Domain, StringComparer.Ordinal)
            || (entity.Domain == "cover" && entity.DeviceClass is { } deviceClass && SecurityCovers.Contains(deviceClass, StringComparer.Ordinal));
    }

    /// <inheritdoc />
    public ActionClass LowestClass(ToolServerTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return IsRead(tool) ? ActionClass.Read : IsScript(tool.Name) || AimsAtEntities(tool) || IsKnownEveryday(tool) ? ActionClass.EverydayEffect : ActionClass.Sensitive;
    }

    /// <inheritdoc />
    public async ValueTask<ActionClass> ClassifyAsync(ToolServerCall call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        var tool = call.Tool;
        if (IsRead(tool))
        {
            return ActionClass.Read;
        }

        var options = settings.Read(HomeAssistantOptions.Section);
        if (IsScript(tool.Name))
        {
            // A script can actuate anything, and its name does not say what: only the mark of the user raises it.
            return IsMarked(options.SecurityScripts, tool.Name) ? ActionClass.Critical : ActionClass.EverydayEffect;
        }

        if (!AimsAtEntities(tool))
        {
            return IsKnownEveryday(tool) ? ActionClass.EverydayEffect : ActionClass.Sensitive;
        }

        var entities = await EntitiesAsync(call, options, cancellationToken).ConfigureAwait(false);
        var aimedAt = Designated(entities, call.Arguments);
        return aimedAt.Any(entity => IsSecurity(entity, options)) ? ActionClass.Critical : ActionClass.EverydayEffect;
    }

    /// <summary>
    /// The entities a call designates. When its name, its room or its floor finds nothing sure (an alias the list
    /// does not show, an approximate name, a floor, which the list does not give), every exposed entity its
    /// <c>domain</c> and <c>device_class</c> filters do not exclude.
    /// </summary>
    /// <param name="entities">The exposed entities.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The entities whose class decides.</returns>
    internal static IReadOnlyList<HomeEntity> Designated(IReadOnlyList<HomeEntity> entities, JsonElement arguments)
    {
        var name = Text(arguments, "name");
        var area = Text(arguments, "area");
        var hasFloor = Text(arguments, "floor") is not null;
        var domains = Texts(arguments, "domain");
        var deviceClasses = Texts(arguments, "device_class");

        var notExcluded = entities
            .Where(entity => domains.Count == 0 || domains.Contains(entity.Domain, StringComparer.OrdinalIgnoreCase))
            .Where(entity => deviceClasses.Count == 0 || (entity.DeviceClass is { } own && deviceClasses.Contains(own, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        if (hasFloor || (name is null && area is null))
        {
            return notExcluded;
        }

        var found = notExcluded
            .Where(entity => name is null || entity.IsNamed(name))
            .Where(entity => area is null || entity.Areas.Any(own => string.Equals(own, area, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return found.Count > 0 ? found : notExcluded;
    }

    private static bool IsRead(ToolServerTool tool) => tool.ReadOnly || Reads.Any(read => IsNamed(tool.Name, read));

    private static bool IsKnownEveryday(ToolServerTool tool) => EverydayTools.Any(known => IsNamed(tool.Name, known));

    /// <summary>The tools of Home Assistant are named <c>&lt;platform&gt;__&lt;intent&gt;</c>: the intent decides.</summary>
    private static bool IsNamed(string tool, string intent) =>
        string.Equals(tool, intent, StringComparison.Ordinal) || tool.EndsWith("__" + intent, StringComparison.Ordinal);

    private static bool AimsAtEntities(ToolServerTool tool) =>
        tool.Schema.ValueKind == JsonValueKind.Object
        && tool.Schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object
        && TargetParameters.Any(parameter => properties.TryGetProperty(parameter, out _));

    private static bool IsMarked(IReadOnlyList<string>? marked, string tool) =>
        (marked ?? []).Any(entry => string.Equals(entry, tool, StringComparison.Ordinal) || entry.EndsWith("__" + tool, StringComparison.Ordinal));

    private static bool IsSecurity(HomeEntity entity, HomeAssistantOptions options) =>
        IsSecurityByNature(entity) || (options.SecurityEntities ?? []).Any(entity.IsNamed);

    private static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    /// <summary>A filter of Home Assistant is a list, but a model may write a single text.</summary>
    private static IReadOnlyList<string> Texts(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => [value.GetString()!.Trim()],
            JsonValueKind.Array => [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())).Select(item => item.GetString()!.Trim())],
            _ => [],
        };
    }

    private async ValueTask<IReadOnlyList<HomeEntity>> EntitiesAsync(ToolServerCall call, HomeAssistantOptions options, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        lock (gate)
        {
            if (options.ContextSeconds > 0 && kept.TryGetValue(call.Server, out var entry) && now - entry.At < TimeSpan.FromSeconds(options.ContextSeconds))
            {
                return entry.Entities;
            }
        }

        // The tool is named « GetLiveContext » or « homeassistant__GetLiveContext » depending on the version.
        ToolServerResult? answer = null;
        foreach (var name in new[] { "homeassistant__GetLiveContext", "GetLiveContext" })
        {
            var attempt = await call.Ask(name, NoArguments, cancellationToken).ConfigureAwait(false);
            if (!attempt.IsError)
            {
                answer = attempt;
                break;
            }
        }

        if (answer is null)
        {
            // Without the exposed entities nothing can be classed: a denial, never a guess.
            throw new InvalidOperationException("Home Assistant did not give the exposed entities.");
        }

        var entities = LiveContext.Parse(answer.Text);
        if (entities.Count == 0)
        {
            // An answer nothing can be read from (the format changed, nothing is exposed) would class every call
            // as an everyday effect, a lock included: a denial instead.
            throw new InvalidOperationException("No exposed entity could be read from the answer of Home Assistant.");
        }

        lock (gate)
        {
            kept[call.Server] = (now, entities);
        }

        return entities;
    }
}
