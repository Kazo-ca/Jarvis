using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
/// for <c>web_search</c>, « lis la page … » for <c>fetch_url</c>,
/// and « outil inconnu » for a tool that does not exist, which the decision point denies. After a tool result, the
/// answer quotes it.
/// </para>
/// </summary>
public sealed partial class SimulatedLlmConnector : ILlmConnector
{
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
            if (last?.Role == ChatRole.User && ToolCall(last.Text, options) is { } call)
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                yield return new ChatResponseUpdate(ChatRole.Assistant, [call]) { ModelId = provider.Model, FinishReason = ChatFinishReason.ToolCalls };
                yield break;
            }

            var result = last?.Role == ChatRole.Tool ? last.Contents.OfType<FunctionResultContent>().LastOrDefault()?.Result?.ToString() : null;
            var answer = result is null ? Answer : "Résultat de l'outil, Monsieur : " + result;
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

        /// <summary>The tool call the words of <paramref name="request"/> ask for, when that tool is offered.</summary>
        private static FunctionCallContent? ToolCall(string request, ChatOptions? options)
        {
            bool Offered(string name) => options?.Tools?.Any(tool => tool.Name == name) == true;

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

            // A tool the registry does not know: only when tools are offered, as a real model could invent one.
            return options?.Tools is { Count: > 0 } && request.Contains("outil inconnu", StringComparison.OrdinalIgnoreCase)
                ? new FunctionCallContent("call_unknown", "open_the_pod_bay_doors", new Dictionary<string, object?>())
                : null;
        }

        public void Dispose()
        {
        }
    }

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

    [GeneratedRegex(@"heure exacte|exact time", RegexOptions.IgnoreCase)]
    private static partial Regex TimeRequest();

    [GeneratedRegex(@"(?:calcule|calculate)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CalculationRequest();
}
