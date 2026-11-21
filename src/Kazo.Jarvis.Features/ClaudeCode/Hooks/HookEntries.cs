using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.LocalServer;

namespace Kazo.Jarvis.Features.ClaudeCode.Hooks;

/// <summary>Whether the hooks of Jarvis are in a settings text.</summary>
public enum HookInstallState
{
    /// <summary>No hook of Jarvis.</summary>
    None,

    /// <summary>The five hooks, with the port and the token of now.</summary>
    Installed,

    /// <summary>Hooks of Jarvis are there, but not the five of now: another port, another token, or one was removed by hand.</summary>
    Outdated,

    /// <summary>The text is not a settings file Jarvis knows how to change.</summary>
    Unreadable,
}

/// <summary>The result of a change of a settings text.</summary>
/// <param name="Text">The new text, or <see langword="null"/> when the text given cannot be changed safely.</param>
/// <param name="Changed">How many hooks of Jarvis were added or removed.</param>
public sealed record HookChange(string? Text, int Changed);

/// <summary>
/// The lines Jarvis adds to the user's Claude Code settings, and how they are added and removed (ADR 0021,
/// section 6). Pure text work: nothing here reads or writes a file.
/// <para>
/// The hooks are <b>commands that run in the background</b> (<c>async</c>), not <c>http</c> hooks: Claude Code
/// does not wait for them, so a Jarvis that is closed neither blocks nor slows a session, and <c>curl</c> fails
/// in silence within two seconds. The ADR asks for this form whenever the other one is not proven harmless.
/// </para>
/// <para>
/// An entry of Jarvis is recognized by its address, <c>http://127.0.0.1:&lt;port&gt;/hook/&lt;route&gt;</c>,
/// whatever the port: a removal also finds the entries written before the port was changed. Everything else in
/// the file is kept as it is; only the layout of the JSON is rewritten.
/// </para>
/// </summary>
public static partial class HookEntries
{
    /// <summary>The events of Claude Code and the route each one calls, in the order of ADR 0021, section 5.</summary>
    public static IReadOnlyList<(string Event, string Path)> Events { get; } =
    [
        ("SessionStart", HookPaths.Context),
        ("UserPromptSubmit", HookPaths.Start),
        ("PermissionRequest", HookPaths.Approval),
        ("SubagentStop", HookPaths.SubtaskIdle),
        ("Stop", HookPaths.Idle),
    ];

    private static readonly JsonSerializerOptions Layout = new()
    {
        WriteIndented = true,

        // Quotes and accents stay readable: the user must be able to read what was written in their file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The command of one hook: the body of the hook, received on the standard input, is posted as it is.</summary>
    /// <param name="port">The port of the local server.</param>
    /// <param name="token">The token of the user's hooks.</param>
    /// <param name="path">The route.</param>
    /// <returns>The command line, valid in Git Bash and in PowerShell.</returns>
    public static string Command(int port, string token, string path)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(path);

        // curl.exe by its full name: in Windows PowerShell, « curl » is another command.
        return $"curl.exe -s -m 2 --noproxy 127.0.0.1 -X POST -H \"Authorization: Bearer {token}\" -H \"Content-Type: application/json\" --data-binary @- http://127.0.0.1:{port}{path}";
    }

    /// <summary>The exact lines Jarvis adds, as the <c>hooks</c> part of a settings file.</summary>
    /// <param name="port">The port of the local server.</param>
    /// <param name="token">The token of the user's hooks.</param>
    /// <returns>A JSON text, to show before the installation or to paste by hand.</returns>
    public static string Preview(int port, string token)
    {
        var hooks = new JsonObject();
        foreach (var (name, path) in Events)
        {
            hooks[name] = new JsonArray(Group(port, token, path));
        }

        return new JsonObject { ["hooks"] = hooks }.ToJsonString(Layout);
    }

    /// <summary>Whether the hooks of Jarvis are in a settings text.</summary>
    /// <param name="text">The settings, or <see langword="null"/> when there is no file.</param>
    /// <param name="port">The port of now.</param>
    /// <param name="token">The token of now.</param>
    /// <returns>The state.</returns>
    public static HookInstallState StateOf(string? text, int port, string token)
    {
        if (Parse(text) is not { } root)
        {
            return HookInstallState.Unreadable;
        }

        if (!TryHooks(root, out var hooks))
        {
            return HookInstallState.Unreadable;
        }

        var commands = Ours(hooks).Select(hook => hook.Command).ToList();
        if (commands.Count == 0)
        {
            return HookInstallState.None;
        }

        var wanted = Events.Select(entry => Command(port, token, entry.Path)).ToList();
        return commands.Count == wanted.Count && wanted.All(commands.Contains) ? HookInstallState.Installed : HookInstallState.Outdated;
    }

    /// <summary>Adds the five hooks, after removing those of Jarvis already there.</summary>
    /// <param name="text">The settings, or <see langword="null"/> when there is no file.</param>
    /// <param name="port">The port of the local server.</param>
    /// <param name="token">The token of the user's hooks.</param>
    /// <returns>The new text; no text when the settings cannot be changed safely.</returns>
    public static HookChange Install(string? text, int port, string token)
    {
        if (Parse(text) is not { } root || !TryHooks(root, out var hooks))
        {
            return new HookChange(null, 0);
        }

        // An event that is not a list is something Jarvis does not know: nothing is written.
        if (hooks is not null && Events.Any(entry => hooks[entry.Event] is not (null or JsonArray)))
        {
            return new HookChange(null, 0);
        }

        RemoveOurs(root, hooks);
        if (root["hooks"] is not JsonObject target)
        {
            root["hooks"] = target = [];
        }

        foreach (var (name, path) in Events)
        {
            if (target[name] is not JsonArray groups)
            {
                target[name] = groups = [];
            }

            groups.Add(Group(port, token, path));
        }

        return new HookChange(root.ToJsonString(Layout), Events.Count);
    }

    /// <summary>Removes the hooks of Jarvis and nothing else.</summary>
    /// <param name="text">The settings.</param>
    /// <returns>The new text and how many hooks left; no text when the settings cannot be changed safely.</returns>
    public static HookChange Remove(string? text)
    {
        if (Parse(text) is not { } root || !TryHooks(root, out var hooks))
        {
            return new HookChange(null, 0);
        }

        var removed = RemoveOurs(root, hooks);
        return new HookChange(root.ToJsonString(Layout), removed);
    }

    private static JsonObject Group(int port, string token, string path) => new()
    {
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            ["command"] = Command(port, token, path),
            ["async"] = true,
            ["timeout"] = 5,
        }),
    };

    private static JsonObject? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            // Strict JSON: a file with comments would lose them when rewritten, so it is left to the user.
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryHooks(JsonObject root, out JsonObject? hooks)
    {
        hooks = root["hooks"] as JsonObject;
        return root["hooks"] is null or JsonObject;
    }

    private static IEnumerable<(JsonArray Groups, JsonObject Group, JsonArray List, JsonObject Hook, string Command)> Ours(JsonObject? hooks)
    {
        if (hooks is null)
        {
            yield break;
        }

        foreach (var (_, value) in hooks)
        {
            if (value is not JsonArray groups)
            {
                continue;
            }

            foreach (var group in groups.OfType<JsonObject>())
            {
                if (group["hooks"] is not JsonArray list)
                {
                    continue;
                }

                foreach (var hook in list.OfType<JsonObject>())
                {
                    var address = Text(hook["command"]) ?? Text(hook["url"]);
                    if (address is not null && OurAddress().IsMatch(address))
                    {
                        yield return (groups, group, list, hook, address);
                    }
                }
            }
        }
    }

    private static int RemoveOurs(JsonObject root, JsonObject? hooks)
    {
        var ours = Ours(hooks).ToList();
        foreach (var (groups, group, list, hook, _) in ours)
        {
            list.Remove(hook);

            // What Jarvis emptied goes with it; what was empty before stays as the user wrote it.
            if (list.Count == 0)
            {
                groups.Remove(group);
            }
        }

        if (ours.Count > 0 && hooks is not null)
        {
            foreach (var name in hooks.Where(pair => pair.Value is JsonArray { Count: 0 }).Select(pair => pair.Key).ToList())
            {
                hooks.Remove(name);
            }

            if (hooks.Count == 0)
            {
                root.Remove("hooks");
            }
        }

        return ours.Count;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    [GeneratedRegex(@"http://127\.0\.0\.1:\d{1,5}/hook/(task\.context|task\.start|approval\.requested|task\.idle|subtask\.idle)(?![\w.])")]
    private static partial Regex OurAddress();
}
