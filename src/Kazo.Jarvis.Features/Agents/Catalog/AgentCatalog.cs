using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Agents;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Agents.Catalog;

/// <summary>
/// The agents (ADR 0025, sections 1 to 3): read from the Markdown files of the folder of the agents, read again
/// when a file changes. At start it writes the starting files that are missing, and brings up to date those the
/// user never touched. A file that does not read is left out, and a fault says so until every file reads.
/// </summary>
public sealed partial class AgentCatalog : IAgentCatalog, IHostedService, IDisposable
{
    private readonly IAgentFiles files;
    private readonly IToolRegistry tools;
    private readonly ISettingsStore settings;
    private readonly IAssistantLanguageReader language;
    private readonly IMessageBus bus;
    private readonly ILogger<AgentCatalog> logger;
    private readonly Lock gate = new();
    private IReadOnlyList<AgentDefinition>? agents;
    private IReadOnlyList<AgentFileIssue> issues = [];
    private IReadOnlyList<string> fileIds = [];
    private bool faultRaised;
    private bool watching;

    /// <summary>Creates the catalog.</summary>
    /// <param name="files">The folder of the agent files, from the host.</param>
    /// <param name="tools">The registry: an agent only shows tools that are on.</param>
    /// <param name="settings">The settings, for <see cref="AgentsOptions.Section"/>.</param>
    /// <param name="language">The language the starting files are written in.</param>
    /// <param name="bus">The bus, for the fault of an unreadable file.</param>
    /// <param name="logger">Logs the files left out, by name; never their text.</param>
    public AgentCatalog(IAgentFiles files, IToolRegistry tools, ISettingsStore settings, IAssistantLanguageReader language, IMessageBus bus, ILogger<AgentCatalog> logger)
    {
        this.files = files;
        this.tools = tools;
        this.settings = settings;
        this.language = language;
        this.bus = bus;
        this.logger = logger;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> Agents => Loaded();

    /// <inheritdoc />
    public AgentDefinition Main => Loaded().FirstOrDefault(agent => agent.Id == AgentIds.Main) ?? StartingAgents.Main(language.Current);

    /// <inheritdoc />
    public string HandToolName => HandToAgentTool.ToolName;

    /// <inheritdoc />
    public string ReturnToolName => ReturnToJarvisTool.ToolName;

    /// <summary>The files left out, and why; for the agents page.</summary>
    public IReadOnlyList<AgentFileIssue> Issues
    {
        get
        {
            Loaded();
            lock (gate)
            {
                return issues;
            }
        }
    }

    /// <summary>The identities of the agents that come from a file; the Jarvis agent Jarvis ships with is not among them.</summary>
    public IReadOnlyList<string> FileIds
    {
        get
        {
            Loaded();
            lock (gate)
            {
                return fileIds;
            }
        }
    }

    /// <summary>The folder of the files, as shown to the user.</summary>
    public string Folder => files.Folder;

    /// <summary>The agents the Jarvis agent may hand the turn to: every other agent that has a tool of its own now.</summary>
    public IReadOnlyList<AgentDefinition> Others =>
        [.. Loaded().Where(agent => agent.Id != AgentIds.Main && OwnTools(agent, tools.Enabled, Loaded()).Count > 0)];

    /// <inheritdoc />
    public AgentDefinition? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var wanted = Plain(name);
        var known = Loaded();
        return known.FirstOrDefault(agent => Plain(agent.Id) == wanted)
            ?? known.FirstOrDefault(agent => Plain(agent.Name) == wanted)
            ?? (wanted == AgentIds.Main ? Main : null);
    }

    /// <inheritdoc />
    public IReadOnlyList<ITool> ToolsOf(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var known = Loaded();
        var enabled = tools.Enabled;
        var own = OwnTools(agent, enabled, known);
        if (agent.Id != AgentIds.Main)
        {
            // « Ce n'est pas pour moi » (section 8): the way back to the Jarvis agent.
            return [.. own, .. enabled.Where(tool => tool.Name == ReturnToolName)];
        }

        // A tool no agent claims goes to the Jarvis agent (section 3), so a new tool is never lost.
        var claimed = known.Where(other => other.Id != AgentIds.Main).SelectMany(other => OwnTools(other, enabled, known)).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var mine = enabled.Where(tool => !IsHandTool(tool) && (own.Contains(tool) || !claimed.Contains(tool.Name)));

        // The hand is offered only when someone can take it: without it, a request costs one call to the model.
        return [.. mine, .. enabled.Where(tool => tool.Name == HandToolName && Others.Count > 0)];
    }

    /// <inheritdoc />
    public AgentHand ReadHand(JsonElement arguments)
    {
        var asked = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("agent", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

        // Only an agent that has a tool of its own now takes the hand: the others are not in the list the model got.
        var found = Find(asked);
        return new AgentHand(found is not null && Others.Any(agent => agent.Id == found.Id) ? found : null, asked);
    }

    /// <summary>Opens the file of an agent in the editor of the user.</summary>
    /// <param name="id">The identity of the agent.</param>
    /// <returns>Whether something opened.</returns>
    public bool Open(string id) => files.Open(id);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        WriteStartingFiles();
        Watch();
        Reload();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (watching)
        {
            files.Changed -= Reload;
        }
    }

    /// <summary>Reads every file again and tells who listens.</summary>
    public void Reload()
    {
        var known = new List<AgentDefinition>();
        var problems = new List<AgentFileIssue>();
        IReadOnlyList<AgentFile> read;
        try
        {
            read = files.ReadAll();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogFolderUnreadable(exception);
            read = [];
        }

        foreach (var file in read.OrderBy(file => file.Id, StringComparer.OrdinalIgnoreCase))
        {
            var id = file.Id.ToLowerInvariant();
            var problem = AgentFileParser.Parse(id, file.Text, out var agent);
            if (problem is null && known.Any(other => Plain(other.Name) == Plain(agent!.Name)))
            {
                problem = AgentFileProblem.NameTaken;
            }

            if (problem is { } cause)
            {
                problems.Add(new AgentFileIssue(file.Id, cause));
                LogFileLeftOut(file.Id, cause);
            }
            else
            {
                known.Add(agent!);
            }
        }

        // The Jarvis agent first; it always exists, with what Jarvis ships when its file does not read.
        var main = known.FirstOrDefault(agent => agent.Id == AgentIds.Main) ?? StartingAgents.Main(language.Current);
        IReadOnlyList<AgentDefinition> ordered = [main, .. known.Where(agent => agent.Id != AgentIds.Main).OrderBy(agent => agent.Name, StringComparer.CurrentCultureIgnoreCase)];

        bool raise;
        bool resolve;
        lock (gate)
        {
            agents = ordered;
            issues = problems;
            fileIds = [.. known.Select(agent => agent.Id)];
            raise = problems.Count > 0;
            resolve = problems.Count == 0 && faultRaised;
            faultRaised = raise;
        }

        if (raise)
        {
            // The detail is a file name, never its text.
            _ = bus.PublishAsync<AssistantInput>(new FaultRaised(new AssistantFault(FaultCause.AgentUnreadable, problems[0].File, FaultOrigin.Agents))).AsTask();
        }
        else if (resolve)
        {
            _ = bus.PublishAsync<AssistantInput>(new FaultResolved(FaultCause.AgentUnreadable, FaultOrigin.Agents)).AsTask();
        }

        Changed?.Invoke();
    }

    /// <summary>The fingerprint by which a file Jarvis wrote is told from one the user changed.</summary>
    internal static string Fingerprint(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ReplaceLineEndings("\n"))));

    /// <summary>Lower case, without accents nor what is not a letter or a digit: « Mémoire », « memoire » and « MÉMOIRE » are one name.</summary>
    internal static string Plain(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(character) || character == '-'))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (char.IsWhiteSpace(character) || character == '_')
            {
                builder.Append('-');
            }
        }

        return builder.ToString();
    }

    private static bool IsHandTool(ITool tool) => tool.Name is HandToAgentTool.ToolName or ReturnToJarvisTool.ToolName;

    /// <summary>The enabled tools that are <paramref name="agent"/>'s: named by its file, or given to it by their source.</summary>
    private static List<ITool> OwnTools(AgentDefinition agent, IReadOnlyList<ITool> enabled, IReadOnlyList<AgentDefinition> known) =>
        [.. enabled.Where(tool => !IsHandTool(tool)
            && (agent.Tools.Contains(tool.Name, StringComparer.Ordinal) || (tool.Agent is { } given && Owner(given, known) == agent.Id)))];

    /// <summary>The agent a source names, or the Jarvis agent when no such agent exists.</summary>
    private static string Owner(string given, IReadOnlyList<AgentDefinition> known) =>
        known.Any(agent => agent.Id == given) ? given : AgentIds.Main;

    private IReadOnlyList<AgentDefinition> Loaded()
    {
        lock (gate)
        {
            if (agents is not null)
            {
                return agents;
            }
        }

        // Asked before the start: the files are read as they are, nothing is written.
        Reload();
        lock (gate)
        {
            return agents ?? [];
        }
    }

    private void Watch()
    {
        if (!watching)
        {
            watching = true;
            files.Changed += Reload;
        }
    }

    /// <summary>
    /// Writes each starting file that was never written, and rewrites one the user did not touch when what Jarvis
    /// ships changed (a new version, or another language of the assistant). A file the user changed, or deleted,
    /// stays as the user left it.
    /// </summary>
    private void WriteStartingFiles()
    {
        try
        {
            var written = new Dictionary<string, string>(settings.Read(AgentsOptions.Section).Written ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            var present = files.ReadAll().ToDictionary(file => file.Id.ToLowerInvariant(), file => file.Text, StringComparer.Ordinal);
            var changed = false;
            foreach (var agent in StartingAgents.In(language.Current))
            {
                var text = AgentFileParser.Write(agent);
                var fingerprint = Fingerprint(text);
                var known = written.GetValueOrDefault(agent.Id);
                var exists = present.TryGetValue(agent.Id, out var current);
                var untouched = exists && current is not null && known is not null && Fingerprint(current) == known;
                if ((!exists && known is null) || (untouched && known != fingerprint))
                {
                    files.Write(agent.Id, text);
                    written[agent.Id] = fingerprint;
                    changed = true;
                }
            }

            if (changed)
            {
                _ = settings.WriteAsync(AgentsOptions.Section, new AgentsOptions { Written = written }).AsTask();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Without its files Jarvis still answers: the Jarvis agent it ships with takes every tool.
            LogStartingFilesNotWritten(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent file {File} is left out ({Problem}).")]
    private partial void LogFileLeftOut(string file, AgentFileProblem problem);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The folder of the agents could not be read.")]
    private partial void LogFolderUnreadable(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The starting agent files could not be written.")]
    private partial void LogStartingFilesNotWritten(Exception exception);
}

/// <summary>A file of the folder of the agents that is left out.</summary>
/// <param name="File">Its name without the extension.</param>
/// <param name="Problem">Why.</param>
public sealed record AgentFileIssue(string File, AgentFileProblem Problem);
