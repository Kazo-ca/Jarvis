using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Kazo.Jarvis.Features.Conversation.Memory;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="ILlmConnector"/> (ADR 0007): no network, no model. Each cause of failure is reached by
/// an address or a model, so the tests and the page can show them all:
/// a host ending in <c>.invalid</c> is unreachable, a host ending in <c>.slow</c> never answers, a cloud provider
/// whose key is the word <c>refused</c> is refused, and only the models in <see cref="Models"/> exist.
/// <para>
/// Tool calls (ADR 0017) are simulated from the words of the request, when the tool is offered: « heure exacte » or
/// "exact time" asks for <c>get_datetime</c>, « calcule … » or "calculate …" for <c>calculate</c> with what follows,
/// « écris une note … » or "write a note …" for the simulated "Sensitive" tool <c>write_note</c>, « efface les notes »
/// or "erase the notes" for the simulated "Critical" tool <c>erase_notes</c> (both ask for a confirmation, ADR 0023),
/// « liste le dossier … » for <c>list_directory</c>, « lis le fichier … » for <c>read_file</c>, « cherche sur le web … »
/// for <c>web_search</c>, « lis la page … » for <c>fetch_url</c>, « cours de l'action … », « envoie le message … » and
/// « lance le dé » for the tools of the simulated tool servers (<c>lookup_stock</c>, <c>send_message</c>, <c>roll_dice</c>),
/// « allume … », « éteins … » (« tout dans la pièce … » for a room) and « lance le script … » for the simulated Home Assistant,
/// « souviens-toi que … », « oublie que … » and « que sais-tu sur … » for the tools of the memory, « question difficile »
/// for <c>hand_off_to_claude</c> (a simulated cloud provider then says back what it received), « que retiens-tu »
/// « où en est Claude sur … » for <c>get_claude_sessions</c> and « silence pendant N heures » for <c>start_quiet_period</c>,
/// to hear back the memory block the model received, « passe la main à X » for <c>hand_to_agent</c> (the Jarvis agent
/// also hands over by itself when the words ask for a tool only another agent sees), « il reste quoi à faire dans X » for the agent of the projects (stage2/19-projects), « pas pour toi » for
/// <c>return_to_jarvis</c>, and « outil inconnu » for a tool that does not exist, which the decision point denies. After a tool result, the
/// answer quotes it.
/// </para>
/// </summary>
public sealed partial class SimulatedLlmConnector : ILlmConnector
{
    private static readonly System.Text.Json.JsonSerializerOptions RelaxedJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private const string Answer = "Bonsoir, Monsieur. Ceci est une réponse simulée : aucun modèle n'a été appelé.";

    private readonly InMemorySecretVault vault;
    private readonly Lock gate = new();
    private readonly HashSet<string> loaded = new(StringComparer.Ordinal);

    /// <summary>Creates the simulation.</summary>
    /// <param name="vault">The simulated vault, for the keys of the cloud providers.</param>
    public SimulatedLlmConnector(InMemorySecretVault vault)
    {
        this.vault = vault;
    }

    /// <summary>The models every simulated server has.</summary>
    public static IReadOnlyList<string> Models { get; } = ["qwen3:14b", "qwen2.5:14b", "default", "claude-opus-5-5", "gpt-5"];

    /// <summary>
    /// When set, every provider fails with this cause, as if the server were stopped (<see cref="LlmFailure.Unreachable"/>)
    /// or had lost its model (<see cref="LlmFailure.ModelMissing"/>). The faults tests use it (stage1/15-faults).
    /// </summary>
    public LlmFailure? ForcedFailure { get; set; }

    /// <summary>How long a simulated load takes.</summary>
    public TimeSpan LoadDuration { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Frees every model, as Ollama does when it loads another one.</summary>
    public void Unload()
    {
        lock (gate)
        {
            loaded.Clear();
        }
    }

    /// <inheritdoc />
    public IChatClient CreateClient(LlmProvider provider) => new Client(this, provider);

    /// <inheritdoc />
    public async Task<LlmConnectionResult> TestConnectionAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckAsync(provider, cancellationToken).ConfigureAwait(false);
            return LlmConnectionResult.Passed(TimeSpan.FromMilliseconds(12), Models);
        }
        catch (LlmException exception)
        {
            return LlmConnectionResult.Failed(exception.Failure, exception.Message, TimeSpan.FromMilliseconds(12),
                exception.Failure == LlmFailure.ModelMissing ? Models : []);
        }
    }

    /// <inheritdoc />
    public Task<bool?> IsLoadedAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        if (provider.Kind != LlmProviderKind.Ollama)
        {
            return Task.FromResult<bool?>(null);
        }

        lock (gate)
        {
            return Task.FromResult<bool?>(loaded.Contains(provider.Model));
        }
    }

    /// <inheritdoc />
    public async Task PreloadAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        if (provider.Kind != LlmProviderKind.Ollama)
        {
            return;
        }

        await CheckAsync(provider, cancellationToken).ConfigureAwait(false);
        await Task.Delay(LoadDuration, cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            loaded.Add(provider.Model);
        }
    }

    private async Task CheckAsync(LlmProvider provider, CancellationToken cancellationToken)
    {
        if (ForcedFailure is { } forced)
        {
            throw new LlmException(forced, $"Simulated: {forced}.");
        }

        if (!provider.Endpoint.IsAbsoluteUri || (provider.Endpoint.Scheme != Uri.UriSchemeHttp && provider.Endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new LlmException(LlmFailure.InvalidAddress, "Simulated: not an http address.");
        }

        if (provider.Endpoint.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new LlmException(LlmFailure.Unreachable, "Simulated: nothing answers.");
        }

        if (provider.Endpoint.Host.EndsWith(".slow", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (provider.Kind.RequiresKey())
        {
            var key = provider.Key is { } reference ? await vault.GetAsync(reference, cancellationToken).ConfigureAwait(false) : null;
            if (key is null)
            {
                throw new LlmException(LlmFailure.KeyMissing, "Simulated: no key.");
            }

            if (key.Reveal() == "refused")
            {
                throw new LlmException(LlmFailure.KeyRejected, "Simulated: key refused.");
            }
        }

        if (!Models.Contains(provider.Model, StringComparer.OrdinalIgnoreCase))
        {
            throw new LlmException(LlmFailure.ModelMissing, "Simulated: unknown model.");
        }
    }

    /// <summary>A chat client that says the same sentence, a word at a time.</summary>
    private sealed class Client(SimulatedLlmConnector owner, LlmProvider provider) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken).ConfigureAwait(false);

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await owner.CheckAsync(provider, cancellationToken).ConfigureAwait(false);
            var last = messages.LastOrDefault();
            if (MemoryWork(last?.Text ?? string.Empty, options) is { } work)
            {
                // The two background requests of the memory (stage2/8-memory): no tool, no personality.
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                yield return new ChatResponseUpdate(ChatRole.Assistant, work) { ModelId = provider.Model, FinishReason = ChatFinishReason.Stop };
                yield break;
            }

            if (options?.Instructions is { } shorten && (shorten.Contains("to be read aloud", StringComparison.Ordinal) || shorten.Contains("à lire à voix haute", StringComparison.Ordinal)))
            {
                // The last message of a session shortened for its announcement (stage2/13-session-announcements).
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                yield return new ChatResponseUpdate(ChatRole.Assistant, $"Résumé simulé de {(last?.Text ?? string.Empty).Length} caractères.") { ModelId = provider.Model, FinishReason = ChatFinishReason.Stop };
                yield break;
            }

            if (last?.Role == ChatRole.User && ToolCall(last.Text, options) is { } call)
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                yield return new ChatResponseUpdate(ChatRole.Assistant, [call]) { ModelId = provider.Model, FinishReason = ChatFinishReason.ToolCalls };
                yield break;
            }

            var result = last?.Role == ChatRole.Tool ? last.Contents.OfType<FunctionResultContent>().LastOrDefault()?.Result?.ToString() : null;
            var answer = result is not null ? "Résultat de l'outil, Monsieur : " + result
                : !provider.Kind.IsLocal() ? CloudAnswer(provider, messages, options)
                : last?.Role == ChatRole.User && (RecallRequest().IsMatch(last.Text) || LeftToDoRequest().IsMatch(last.Text)) ? Recalled(options?.Instructions)
                : Answer;
            foreach (var word in answer.Split(' '))
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ") { ModelId = provider.Model };
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 42, OutputTokenCount = 18, TotalTokenCount = 60 })])
            {
                ModelId = provider.Model,
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType == typeof(LlmProvider) ? provider : null;

        /// <summary>
        /// The answer to a background request of the memory, recognized by its instructions: a fact drawn from
        /// « je m'appelle … » or « j'habite à … » and nothing from anything else, or a summary that counts the
        /// exchanges it was given.
        /// </summary>
        private static string? MemoryWork(string request, ChatOptions? options)
        {
            var instructions = options?.Instructions;
            if (instructions is null)
            {
                return null;
            }

            if (AssistantLanguage.Supported.Any(language => instructions == MemoryPrompts.Extraction(language)))
            {
                var facts = new List<string>();
                if (NameSaid().Match(request) is { Success: true } name)
                {
                    facts.Add($"L'utilisateur s'appelle {name.Groups[1].Value}.");
                }

                if (HomeSaid().Match(request) is { Success: true } home)
                {
                    facts.Add($"L'utilisateur habite à {home.Groups[1].Value}.");
                }

                return System.Text.Json.JsonSerializer.Serialize(facts, RelaxedJson);
            }

            return AssistantLanguage.Supported.Any(language => instructions == MemoryPrompts.Summary(language))
                ? $"Résumé simulé : {ExchangeLine().Matches(request).Count} échange(s) résumé(s)."
                : null;
        }

        /// <summary>
        /// What a simulated cloud model answers (stage2/9-hand-off): it names its model and says back the request, how
        /// many messages of the history it received and whether the memory block came with them, so the tests see
        /// what left for the target.
        /// </summary>
        private static string CloudAnswer(LlmProvider provider, IEnumerable<ChatMessage> messages, ChatOptions? options)
        {
            var all = messages.ToList();
            var memory = options?.Instructions is { } instructions && (instructions.Contains("[Mémoire]", StringComparison.Ordinal) || instructions.Contains("[Memory]", StringComparison.Ordinal));
            return $"Ici {provider.Model}, dans le cloud. Demande reçue : {all.LastOrDefault()?.Text} Historique : {all.Count - 1} message(s). Mémoire : {(memory ? "oui" : "non")}.";
        }

        /// <summary>What the memory block of the system message holds, said back: the tests see what the model received.</summary>
        private static string Recalled(string? instructions)
        {
            // The blocks are written in the language of the turn: the memory of the user, then the one of the
            // agent that answers (stage2/18-agent-memory).
            var user = Between(instructions, ("[Mémoire]", "[Fin de la mémoire]"), ("[Memory]", "[End of memory]"));
            var agent = Between(instructions, ("[Mémoire de l'agent]", "[Fin de la mémoire de l'agent]"), ("[Agent memory]", "[End of the agent memory]"));
            // The project the turn is about (stage2/19-projects), as the agent that answers was told.
            var project = Between(instructions, ("[Projet]", "[Fin du projet]"), ("[Project]", "[End of the project]"));
            var remembered = (user, agent) switch
            {
                (null, null) => "Je ne retiens rien, Monsieur.",
                (_, null) => "Je retiens ceci, Monsieur : " + user,
                (null, _) => "Je retiens ceci pour mon travail, Monsieur : " + agent,
                _ => "Je retiens ceci, Monsieur : " + user + " Et pour mon travail : " + agent,
            };
            return project is null ? remembered : remembered + " Le projet : " + project;
        }

        private static string? Between(string? instructions, params (string Opening, string Closing)[] marks)
        {
            foreach (var (opening, closing) in marks)
            {
                var start = instructions?.LastIndexOf(opening, StringComparison.Ordinal) ?? -1;
                var end = instructions?.LastIndexOf(closing, StringComparison.Ordinal) ?? -1;
                if (start >= 0 && end > start)
                {
                    return instructions![(start + opening.Length)..end].ReplaceLineEndings(" ").Trim();
                }
            }

            return null;
        }

        /// <summary>The tool call the words of <paramref name="request"/> ask for, when that tool is offered.</summary>
        private static FunctionCallContent? ToolCall(string request, ChatOptions? options)
        {
            bool Offered(string name) => options?.Tools?.Any(tool => tool.Name == name) == true;
            string? OfferedAs(string name) =>
                options?.Tools?.FirstOrDefault(tool => tool.Name == name || tool.Name.EndsWith("__" + name, StringComparison.Ordinal))?.Name;

            // The agents (stage2/17-agents): an agent that gets a request made for another one gives it back.
            if (Offered("return_to_jarvis") && NotForMeRequest().IsMatch(request))
            {
                return new FunctionCallContent("call_return", "return_to_jarvis", new Dictionary<string, object?> { ["reason"] = "Cette demande n'est pas pour moi." });
            }

            if (Offered("get_datetime") && TimeRequest().IsMatch(request))
            {
                return new FunctionCallContent("call_time", "get_datetime", new Dictionary<string, object?>());
            }

            if (Offered("calculate") && CalculationRequest().Match(request) is { Success: true } calculation)
            {
                return new FunctionCallContent("call_calculate", "calculate", new Dictionary<string, object?> { ["expression"] = calculation.Groups[1].Value.Trim() });
            }

            if (Offered(SimulatedNoteTool.ToolName) && NoteRequest().Match(request) is { Success: true } note)
            {
                return new FunctionCallContent("call_note", SimulatedNoteTool.ToolName, new Dictionary<string, object?> { ["text"] = note.Groups[1].Value.Trim().TrimEnd('.', '!', '?').Trim() });
            }

            if (Offered(SimulatedEraseTool.ToolName) && EraseRequest().IsMatch(request))
            {
                return new FunctionCallContent("call_erase", SimulatedEraseTool.ToolName, new Dictionary<string, object?>());
            }

            if (Offered("list_directory") && ListRequest().Match(request) is { Success: true } list)
            {
                var arguments = new Dictionary<string, object?>();
                if (list.Groups[1].Value.Trim().TrimEnd('.', '!', '?').Trim() is { Length: > 0 } folder)
                {
                    arguments["path"] = folder;
                }

                return new FunctionCallContent("call_list", "list_directory", arguments);
            }

            if (Offered("read_file") && FileRequest().Match(request) is { Success: true } file)
            {
                return new FunctionCallContent("call_read", "read_file", new Dictionary<string, object?> { ["path"] = file.Groups[1].Value.Trim().TrimEnd('!', '?').Trim() });
            }

            if (Offered("web_search") && SearchRequest().Match(request) is { Success: true } search)
            {
                return new FunctionCallContent("call_search", "web_search", new Dictionary<string, object?> { ["query"] = search.Groups[1].Value.Trim().TrimEnd('.', '!', '?').Trim() });
            }

            if (Offered("fetch_url") && PageRequest().Match(request) is { Success: true } page)
            {
                return new FunctionCallContent("call_fetch", "fetch_url", new Dictionary<string, object?> { ["url"] = page.Groups[1].Value.Trim() });
            }

            // Tools of the simulated tool servers (stage2/6-mcp-client), under their plain name or prefixed by a server.
            if (StockRequest().Match(request) is { Success: true } stock && OfferedAs("lookup_stock") is { } lookup)
            {
                return new FunctionCallContent("call_stock", lookup, new Dictionary<string, object?> { ["symbol"] = stock.Groups[1].Value.Trim() });
            }

            if (MessageRequest().Match(request) is { Success: true } message && OfferedAs("send_message") is { } send)
            {
                return new FunctionCallContent("call_message", send, new Dictionary<string, object?> { ["text"] = message.Groups[1].Value.Trim().TrimEnd('.', '!', '?').Trim() });
            }

            if (DiceRequest().IsMatch(request) && OfferedAs("roll_dice") is { } dice)
            {
                return new FunctionCallContent("call_dice", dice, new Dictionary<string, object?>());
            }

            // « retiens pour le projet X que … » (stage2/21-project-memory-writing): the agent of the projects writes it.
            if (ProjectFactRequest().Match(request) is { Success: true } projectFact && Offered("remember_project_fact"))
            {
                var names = options?.Tools?.OfType<AIFunctionDeclaration>().First(tool => tool.Name == "remember_project_fact").JsonSchema
                    .GetProperty("properties").GetProperty("project").GetProperty("enum").EnumerateArray().Select(name => name.GetString()).ToList() ?? [];
                var said = projectFact.Groups[1].Success ? projectFact.Groups[1].Value : projectFact.Groups[3].Value;
                var fact = projectFact.Groups[2].Success ? projectFact.Groups[2].Value : projectFact.Groups[4].Value;
                return new FunctionCallContent("call_project_fact", "remember_project_fact", new Dictionary<string, object?>
                {
                    ["project"] = names.FirstOrDefault(name => string.Equals(name, said.Trim(), StringComparison.OrdinalIgnoreCase)) ?? said.Trim(),
                    ["text"] = fact.Trim(),
                });
            }

            // Tools of the memory (stage2/8-memory).
            if (RememberRequest().Match(request) is { Success: true } remember && Offered("remember_fact"))
            {
                return new FunctionCallContent("call_remember", "remember_fact", new Dictionary<string, object?> { ["text"] = Sentence(remember.Groups[1].Value) });
            }

            if (ForgetRequest().Match(request) is { Success: true } forget && Offered("forget_fact"))
            {
                return new FunctionCallContent("call_forget", "forget_fact", new Dictionary<string, object?> { ["text"] = Sentence(forget.Groups[1].Value) });
            }

            if (KnownRequest().Match(request) is { Success: true } known && Offered("search_memory"))
            {
                return new FunctionCallContent("call_search_memory", "search_memory", new Dictionary<string, object?> { ["query"] = Sentence(known.Groups[1].Value) });
            }

            // Tools of the simulated Home Assistant (stage2/7-home-assistant).
            if (ScriptRequest().Match(request) is { Success: true } script && OfferedAs("script__" + script.Groups[1].Value) is { } run)
            {
                return new FunctionCallContent("call_script", run, new Dictionary<string, object?>());
            }

            // « déverrouille » holds « verrouille »: turning off is looked for first.
            if (TurnOffRequest().Match(request) is { Success: true } turnOff && OfferedAs("HassTurnOff") is { } off)
            {
                return new FunctionCallContent("call_turn_off", off, HomeTarget(turnOff.Groups[1].Value));
            }

            if (TurnOnRequest().Match(request) is { Success: true } turnOn && OfferedAs("HassTurnOn") is { } on)
            {
                return new FunctionCallContent("call_turn_on", on, HomeTarget(turnOn.Groups[1].Value));
            }

            // The announcements of the sessions (stage2/13-session-announcements).
            if (SessionsRequest().Match(request) is { Success: true } sessions && Offered("get_claude_sessions"))
            {
                var arguments = new Dictionary<string, object?>();
                if (Sentence(sessions.Groups[1].Value) is { Length: > 0 } folder)
                {
                    arguments["folder"] = folder;
                }

                return new FunctionCallContent("call_sessions", "get_claude_sessions", arguments);
            }

            if (QuietRequest().Match(request) is { Success: true } silence && Offered("start_quiet_period"))
            {
                return new FunctionCallContent("call_quiet", "start_quiet_period", new Dictionary<string, object?> { ["hours"] = int.Parse(silence.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) });
            }

            // The hand-off (stage2/9-hand-off): offered only while a target is set.
            if (HardRequest().IsMatch(request) && Offered("hand_off_to_claude"))
            {
                return new FunctionCallContent("call_hand_off", "hand_off_to_claude", new Dictionary<string, object?> { ["kind"] = "question", ["reason"] = "Cette question demande un raisonnement long." });
            }

            // The Jarvis agent (stage2/17-agents): the words ask for a tool it does not see, so it hands the turn to
            // the agent that has it, by the name the hand tool lists; « passe la main à X » names any agent.
            if (Offered("hand_to_agent") && AgentFor(request, options) is { } agent)
            {
                var arguments = new Dictionary<string, object?> { ["agent"] = agent, ["reason"] = "Cette demande est pour cet agent." };
                if (ProjectIn(request, options) is { } project)
                {
                    // The project is named with the agent (ADR 0025, section 10).
                    arguments["project"] = project;
                }

                return new FunctionCallContent("call_hand", "hand_to_agent", arguments);
            }

            // A tool the registry does not know: only when tools are offered, as a real model could invent one.
            return options?.Tools is { Count: > 0 } && request.Contains("outil inconnu", StringComparison.OrdinalIgnoreCase)
                ? new FunctionCallContent("call_unknown", "open_the_pod_bay_doors", new Dictionary<string, object?>())
                : null;
        }

        /// <summary>The agent the words of <paramref name="request"/> are for, among those the hand tool lists.</summary>
        private static string? AgentFor(string request, ChatOptions? options)
        {
            if (HandRequest().Match(request) is { Success: true } named)
            {
                return Sentence(named.Groups[1].Value);
            }

            if (LeftToDoRequest().IsMatch(request) || ProjectFactRequest().IsMatch(request))
            {
                // « il reste quoi à faire dans X » (stage2/19-projects): for the one agent of the projects.
                var hand = options?.Tools?.OfType<AIFunctionDeclaration>().FirstOrDefault(tool => tool.Name == "hand_to_agent")?.JsonSchema.GetRawText() ?? string.Empty;
                return new[] { "Projet", "Project" }.FirstOrDefault(name => hand.Contains("\"" + name + "\"", StringComparison.Ordinal));
            }

            var schema = options?.Tools?.OfType<AIFunctionDeclaration>().FirstOrDefault(tool => tool.Name == "hand_to_agent")?.JsonSchema.GetRawText() ?? string.Empty;
            string? Listed(params string[] names) => names.FirstOrDefault(name => schema.Contains("\"" + name + "\"", StringComparison.Ordinal));

            return ListRequest().IsMatch(request) || FileRequest().IsMatch(request) ? Listed("Fichiers", "Files")
                : SearchRequest().IsMatch(request) || PageRequest().IsMatch(request) ? Listed("Web")
                : RememberRequest().IsMatch(request) || ForgetRequest().IsMatch(request) || KnownRequest().IsMatch(request) ? Listed("Mémoire", "M\\u00E9moire", "Memory")
                : ScriptRequest().IsMatch(request) || TurnOffRequest().IsMatch(request) || TurnOnRequest().IsMatch(request) ? Listed("Domotique", "Home")
                : SessionsRequest().IsMatch(request) ? Listed("Claude Code")
                : null;
        }

        /// <summary>The project of the hand tool the words of <paramref name="request"/> name, if any.</summary>
        private static string? ProjectIn(string request, ChatOptions? options)
        {
            var schema = options?.Tools?.OfType<AIFunctionDeclaration>().FirstOrDefault(tool => tool.Name == "hand_to_agent")?.JsonSchema;
            return schema is { } hand && hand.TryGetProperty("properties", out var properties) && properties.TryGetProperty("project", out var project)
                && project.TryGetProperty("enum", out var names)
                ? names.EnumerateArray().Select(name => name.GetString()).FirstOrDefault(name => name is not null && request.Contains(name, StringComparison.OrdinalIgnoreCase))
                : null;
        }

        private static string Sentence(string words) => words.Trim().TrimEnd('.', '!', '?').Trim();

        /// <summary>« tout dans la pièce X » aims at a room, anything else names a device.</summary>
        private static Dictionary<string, object?> HomeTarget(string words)
        {
            var target = words.Trim().TrimEnd('.', '!', '?').Trim();
            return RoomRequest().Match(target) is { Success: true } room
                ? new Dictionary<string, object?> { ["area"] = room.Groups[1].Value.Trim() }
                : new Dictionary<string, object?> { ["name"] = target };
        }

        public void Dispose()
        {
        }
    }

    [GeneratedRegex(@"(?:retiens|note) pour (?:le projet )?(.+?) que\s+(.+)$|remember for (?:the project )?(.+?) that\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectFactRequest();

    [GeneratedRegex(@"(?:reste (?:quoi )?à faire|left to do)", RegexOptions.IgnoreCase)]
    private static partial Regex LeftToDoRequest();

    [GeneratedRegex(@"(?:pas pour toi|not for you)", RegexOptions.IgnoreCase)]
    private static partial Regex NotForMeRequest();

    [GeneratedRegex(@"(?:passe la main à|hand over to)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex HandRequest();

    [GeneratedRegex(@"(?:où en est claude|where is claude)(?:\s+(?:sur|on)\s+(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex SessionsRequest();

    [GeneratedRegex(@"(?:silence pendant|stay silent for)\s+(\d+)\s+(?:heures?|hours?)", RegexOptions.IgnoreCase)]
    private static partial Regex QuietRequest();

    [GeneratedRegex(@"(?:écris une note|write a note)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteRequest();

    [GeneratedRegex(@"efface les notes|erase the notes", RegexOptions.IgnoreCase)]
    private static partial Regex EraseRequest();

    [GeneratedRegex(@"(?:liste (?:le dossier|les dossiers autorisés)|list the (?:folder|allowed folders))\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ListRequest();

    [GeneratedRegex(@"(?:lis le fichier|read the file)\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileRequest();

    [GeneratedRegex(@"(?:cherche sur le web|search the web for)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SearchRequest();

    [GeneratedRegex(@"(?:lis la page|read the page)\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex PageRequest();

    [GeneratedRegex(@"(?:cours de l'action|price of the stock)\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex StockRequest();

    [GeneratedRegex(@"(?:envoie le message|send the message)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MessageRequest();

    [GeneratedRegex(@"(?:lance le script|run the script)\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRequest();

    [GeneratedRegex(@"(?:allume|ouvre|verrouille|turn on)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TurnOnRequest();

    [GeneratedRegex(@"(?:éteins|ferme|déverrouille|turn off)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TurnOffRequest();

    [GeneratedRegex(@"^(?:tout dans la pièce|everything in the room)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RoomRequest();

    [GeneratedRegex(@"(?:souviens-toi que|retiens que|remember that)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RememberRequest();

    [GeneratedRegex(@"(?:oublie que|forget that)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ForgetRequest();

    [GeneratedRegex(@"(?:que sais-tu sur|what do you know about)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex KnownRequest();

    [GeneratedRegex(@"question difficile|hard question", RegexOptions.IgnoreCase)]
    private static partial Regex HardRequest();

    [GeneratedRegex(@"que retiens-tu|what do you remember", RegexOptions.IgnoreCase)]
    private static partial Regex RecallRequest();

    [GeneratedRegex(@"je m'appelle\s+(\p{L}+)", RegexOptions.IgnoreCase)]
    private static partial Regex NameSaid();

    [GeneratedRegex(@"j'habite à\s+(\p{L}+)", RegexOptions.IgnoreCase)]
    private static partial Regex HomeSaid();

    [GeneratedRegex(@"^(?:Utilisateur :|User:)", RegexOptions.Multiline)]
    private static partial Regex ExchangeLine();

    [GeneratedRegex(@"lance le dé|roll the die", RegexOptions.IgnoreCase)]
    private static partial Regex DiceRequest();

    [GeneratedRegex(@"heure exacte|exact time", RegexOptions.IgnoreCase)]
    private static partial Regex TimeRequest();

    [GeneratedRegex(@"(?:calcule|calculate)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CalculationRequest();
}
