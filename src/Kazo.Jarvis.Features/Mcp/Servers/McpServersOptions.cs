using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>
/// The tool servers the user added (ADR 0018, section 1), section <c>Mcp.Servers</c> of <c>config.json</c>. None is
/// configured by default. A token or the value of a header is never here: only the name of the header, whose value
/// is in the vault.
/// </summary>
public sealed partial record McpServersOptions
{
    /// <summary>The most servers.</summary>
    public const int MostServers = 20;

    /// <summary>The most headers of one server.</summary>
    public const int MostHeaders = 10;

    /// <summary>The most arguments of one command.</summary>
    public const int MostArguments = 40;

    /// <summary>Longest identity of an agent, in characters.</summary>
    public const int MostAgentCharacters = 100;

    /// <summary>The section.</summary>
    public static SettingsSection<McpServersOptions> Section { get; } = new("Mcp.Servers", () => new McpServersOptions(), Validate);

    /// <summary>The servers, in the order they were added.</summary>
    public IReadOnlyList<McpServerOptions> Servers { get; init; } = [];

    /// <summary>The server with this name.</summary>
    /// <param name="name">The name in the settings.</param>
    /// <returns>The server, or <see langword="null"/>.</returns>
    public McpServerOptions? Find(string name) =>
        (Servers ?? []).FirstOrDefault(server => string.Equals(server.Name, name, StringComparison.Ordinal));

    /// <summary>A copy where the server named like <paramref name="server"/> is replaced by it, or added at the end.</summary>
    /// <param name="server">The new value of the server.</param>
    /// <returns>The new settings.</returns>
    public McpServersOptions With(McpServerOptions server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var servers = (Servers ?? []).ToList();
        var index = servers.FindIndex(existing => string.Equals(existing.Name, server.Name, StringComparison.Ordinal));
        if (index < 0)
        {
            servers.Add(server);
        }
        else
        {
            servers[index] = server;
        }

        return this with { Servers = servers };
    }

    /// <summary>A copy without the server <paramref name="name"/>.</summary>
    /// <param name="name">The name in the settings.</param>
    /// <returns>The new settings.</returns>
    public McpServersOptions Without(string name) =>
        this with { Servers = [.. (Servers ?? []).Where(server => !string.Equals(server.Name, name, StringComparison.Ordinal))] };

    /// <summary>Whether <paramref name="name"/> can name a server: it enters the origin of its tools and the references of its secrets.</summary>
    /// <param name="name">The name to check.</param>
    /// <returns><see langword="true"/> for 1 to 30 lower-case letters, digits and hyphens that start with a letter.</returns>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>Whether <paramref name="name"/> can name an HTTP header kept by Jarvis.</summary>
    /// <param name="name">The name to check.</param>
    /// <returns><see langword="true"/> for 1 to 50 letters, digits, hyphens and underscores.</returns>
    public static bool IsValidHeader(string? name) => name is not null && HeaderPattern().IsMatch(name);

    /// <summary>Where the vault keeps the value of a header.</summary>
    /// <param name="server">The name of the server.</param>
    /// <param name="header">The name of the header.</param>
    /// <returns>The reference <c>mcp.&lt;server&gt;.header.&lt;header&gt;</c>.</returns>
    public static SecretRef HeaderSecret(string server, string header) => new($"mcp.{server}.header.{header}");

    private static IReadOnlyList<string> Validate(McpServersOptions value)
    {
        if (value.Servers is null)
        {
            return ["The list of tool servers is missing."];
        }

        var problems = new List<string>();
        if (value.Servers.Count > MostServers)
        {
            problems.Add($"At most {MostServers} tool servers.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var server in value.Servers)
        {
            if (server is null || !IsValidName(server.Name))
            {
                problems.Add("The name of a tool server has 1 to 30 lower-case letters, digits or hyphens and starts with a letter.");
                continue;
            }

            if (!names.Add(server.Name))
            {
                problems.Add($"Two tool servers are named '{server.Name}'.");
            }

            var hasAddress = !string.IsNullOrWhiteSpace(server.Address);
            var hasCommand = !string.IsNullOrWhiteSpace(server.Command);
            if (hasAddress == hasCommand)
            {
                problems.Add($"The tool server '{server.Name}' needs an address or a command, not both.");
            }
            else if (hasAddress && server.Uri is null)
            {
                problems.Add($"The address of the tool server '{server.Name}' is not an http or https address.");
            }

            if (!Enum.IsDefined(server.Kind))
            {
                problems.Add($"The type of the tool server '{server.Name}' is unknown.");
            }

            if (server.Agent is { Length: > MostAgentCharacters })
            {
                problems.Add($"The agent of the tool server '{server.Name}' is longer than {MostAgentCharacters} characters.");
            }

            if (server.Arguments is null || server.Arguments.Count > MostArguments || server.Arguments.Any(argument => argument is null))
            {
                problems.Add($"The arguments of the tool server '{server.Name}' are missing or more than {MostArguments}.");
            }

            if (server.Headers is null || server.Headers.Count > MostHeaders || server.Headers.Any(header => !IsValidHeader(header))
                || server.Headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != server.Headers.Count)
            {
                problems.Add($"The headers of the tool server '{server.Name}' are at most {MostHeaders} distinct names of letters, digits, hyphens and underscores.");
            }

            if (server.Approved is null || server.Classes is null)
            {
                problems.Add($"The tools of the tool server '{server.Name}' are missing.");
            }
            else if (server.Classes.Values.Any(raised => raised is not (ActionClass.Sensitive or ActionClass.Critical)))
            {
                // The user can only raise a class (ADR 0018, section 4): a lower one is never written.
                problems.Add($"A class set by hand on the tool server '{server.Name}' is Sensitive or Critical.");
            }
        }

        return problems;
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,29}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,50}$")]
    private static partial Regex HeaderPattern();
}

/// <summary>One tool server.</summary>
public sealed record McpServerOptions
{
    /// <summary>The name the user gave: lower-case letters, digits and hyphens. It is in the origin of the tools (<c>mcp:&lt;name&gt;</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The type, chosen by the user and never deduced from what the server says of itself (ADR 0018, section 1):
    /// a server cannot give itself lower classes.
    /// </summary>
    public McpServerKind Kind { get; init; } = McpServerKind.Generic;

    /// <summary>The address of a server reached over HTTP; empty for a server started by command.</summary>
    public string? Address { get; init; }

    /// <summary>The command that starts a local server; empty for a server reached by address.</summary>
    public string? Command { get; init; }

    /// <summary>The arguments of <see cref="Command"/>.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>The names of the headers sent to <see cref="Address"/>. Their values are in the vault.</summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>
    /// The agent that sees the tools of this server (ADR 0025, section 3), by its identity; <see langword="null"/>
    /// for the Jarvis agent. A Home Assistant server ignores it: its tools go to the home automation agent.
    /// </summary>
    public string? Agent { get; init; }

    /// <summary>Whether Jarvis connects to the server.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// For each tool the user turned on, by its name on the server: the fingerprint of its name, description and
    /// schema at that moment (ADR 0018, section 3). A tool whose fingerprint changed is turned off until the user
    /// sees it again.
    /// </summary>
    public IReadOnlyDictionary<string, string> Approved { get; init; } = new Dictionary<string, string>();

    /// <summary>The classes the user raised, by the name of the tool on the server: Sensitive or Critical only.</summary>
    public IReadOnlyDictionary<string, ActionClass> Classes { get; init; } = new Dictionary<string, ActionClass>();

    /// <summary>The address as a URI, or <see langword="null"/> when there is none or it is not http or https.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Uri? Uri =>
        System.Uri.TryCreate(Address?.Trim(), UriKind.Absolute, out var address) && (address.Scheme == System.Uri.UriSchemeHttp || address.Scheme == System.Uri.UriSchemeHttps)
            ? address
            : null;

    /// <summary>Whether the server is started by command.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ByCommand => !string.IsNullOrWhiteSpace(Command);

    /// <summary>Whether <paramref name="other"/> is reached the same way: a difference means a new connection.</summary>
    /// <param name="other">Another value of the same server.</param>
    /// <returns><see langword="true"/> when the address, the command, its arguments and the header names are the same.</returns>
    public bool SameConnection(McpServerOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Address ?? string.Empty, other.Address ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(Command ?? string.Empty, other.Command ?? string.Empty, StringComparison.Ordinal)
            && (Arguments ?? []).SequenceEqual(other.Arguments ?? [], StringComparer.Ordinal)
            && (Headers ?? []).SequenceEqual(other.Headers ?? [], StringComparer.Ordinal);
    }
}

/// <summary>The types of tool servers (ADR 0018, section 1).</summary>
public enum McpServerKind
{
    /// <summary>Any server: a tool is a read when the server says so, otherwise it asks for a confirmation.</summary>
    Generic,

    /// <summary>The MCP server of Home Assistant, whose calls are classed from the devices they aim at (ADR 0018, section 5).</summary>
    HomeAssistant,
}
