using System.Runtime.CompilerServices;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Tests.Unit.Features.Conversation;

/// <summary>A default model the test steers: its provider, its client, and whether its loading fails.</summary>
internal sealed class FakeAssistantModel : IAssistantModel
{
    public FakeAssistantModel(ScriptedChatClient? client)
    {
        Client = client;
        Provider = client is null ? null : new LlmProvider("local", "Ollama", LlmProviderKind.Ollama, new Uri("http://127.0.0.1:11435"), "qwen3:14b", new LlmTuning());
    }

    public LlmProvider? Provider { get; set; }

    public IChatClient? Client { get; set; }

    public ModelLoadState LoadState => ModelLoadState.Loaded;

    /// <summary>When set, <see cref="EnsureLoadedAsync"/> throws it.</summary>
    public LlmFailure? LoadFailure { get; set; }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        LoadFailure is { } failure ? Task.FromException(new LlmException(failure, "fake failure")) : Task.CompletedTask;
}

/// <summary>A chat client that streams the pieces the test gives, and keeps what it was sent.</summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Lock gate = new();

    public List<string> Pieces { get; } = [];

    /// <summary>
    /// The rounds of a turn with tools: each request takes the next one, made of pieces of text and of
    /// <see cref="FunctionCallContent"/>. Once they are all taken, <see cref="Pieces"/> are streamed.
    /// </summary>
    public Queue<object[]> Rounds { get; } = new();

    /// <summary>When set, the stream waits for it after the first piece.</summary>
    public TaskCompletionSource? HoldAfterFirstPiece { get; set; }

    /// <summary>When set, the stream throws it after the pieces.</summary>
    public LlmFailure? FailAtEnd { get; set; }

    /// <summary>When set, the client fails before it writes anything.</summary>
    public LlmFailure? FailAtStart { get; set; }

    /// <summary>The name the server gives to the model.</summary>
    public string ModelId { get; set; } = "qwen3:14b";

    /// <summary>Completes when a stream is cancelled.</summary>
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    /// <summary>What a request that is not a stream answers: the summary asked for a Claude Code session. <see langword="null"/>: not supported.</summary>
    public string? Summary { get; set; }

    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> SummaryCalls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Summary is null)
        {
            throw new NotSupportedException();
        }

        SummaryCalls.Add(([.. messages], options));
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Summary)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            Calls.Add(([.. messages], options));
        }

        if (FailAtStart is { } refused)
        {
            throw new LlmException(refused, "fake failure");
        }

        using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
        object[]? round;
        lock (gate)
        {
            Rounds.TryDequeue(out round);
        }

        if (round is not null)
        {
            foreach (var item in round)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return item is FunctionCallContent call
                    ? new ChatResponseUpdate(ChatRole.Assistant, [call]) { ModelId = ModelId }
                    : new ChatResponseUpdate(ChatRole.Assistant, (string)item) { ModelId = ModelId };
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10 })])
            {
                ModelId = ModelId,
                FinishReason = ChatFinishReason.ToolCalls,
            };
            yield break;
        }

        for (var i = 0; i < Pieces.Count; i++)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, Pieces[i]) { ModelId = ModelId };
            if (i == 0 && HoldAfterFirstPiece is { } hold)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }
        }

        if (FailAtEnd is { } failure)
        {
            throw new LlmException(failure, "fake failure");
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 120, OutputTokenCount = 14 })])
        {
            ModelId = ModelId,
            FinishReason = ChatFinishReason.Stop,
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

/// <summary>A memory the test steers: what it recalls, and what it was asked.</summary>
internal sealed class FakeMemory : IConversationMemory
{
    public MemoryRecall Recall { get; set; } = MemoryRecall.Nothing;

    /// <summary>When set, <see cref="RecallAsync"/> throws it.</summary>
    public Exception? Failure { get; set; }

    public List<(AssistantLanguage Language, int ContextLength)> Asked { get; } = [];

    /// <summary>What each agent kept for itself, by its identity.</summary>
    public Dictionary<string, string> OfAgents { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, <see cref="RecallForAgentAsync"/> throws it.</summary>
    public Exception? AgentFailure { get; set; }

    public ValueTask<MemoryRecall> RecallAsync(AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default)
    {
        Asked.Add((language, contextLength));
        return Failure is null ? ValueTask.FromResult(Recall) : ValueTask.FromException<MemoryRecall>(Failure);
    }

    public ValueTask<string?> RecallForAgentAsync(string agent, AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default) =>
        AgentFailure is null ? ValueTask.FromResult(OfAgents.GetValueOrDefault(agent)) : ValueTask.FromException<string?>(AgentFailure);
}

/// <summary>A hand-off whose target is set by the test; the formulas and the arguments are read by the real code of the slice.</summary>
internal sealed class FakeHandOff : IHandOff
{
    public string ToolName => Kazo.Jarvis.Features.Llm.HandOff.HandOffTool.ToolName;

    /// <summary>The target of the questions, or <see langword="null"/> when none is set.</summary>
    public HandOffTarget? Target { get; set; }

    public List<HandOffKind> Opened { get; } = [];

    public ForcedHandOff? Forced(string request) => Kazo.Jarvis.Features.Llm.HandOff.HandOffPhrases.Match(request);

    public HandOffCall? Read(System.Text.Json.JsonElement arguments) => Kazo.Jarvis.Features.Llm.HandOff.HandOffTool.Read(arguments);

    /// <summary>When set, the hand-off of a question is refused with it once <see cref="AllowedOpenings"/> were given.</summary>
    public HandOffRefusal? Refusal { get; set; }

    /// <summary>How many times <see cref="Open"/> gives the target before <see cref="Refusal"/> applies.</summary>
    public int AllowedOpenings { get; set; }

    /// <summary>What <see cref="Notice"/> gives, once each.</summary>
    public Queue<string> Notices { get; } = new();

    public HandOffOpening Open(HandOffKind kind)
    {
        Opened.Add(kind);
        return kind == HandOffKind.Task ? HandOffOpening.Refused(HandOffRefusal.NoTasksTarget)
            : Target is null ? HandOffOpening.Refused(HandOffRefusal.NoQuestionsTarget)
            : Refusal is { } refusal && Opened.Count > AllowedOpenings ? HandOffOpening.Refused(refusal)
            : HandOffOpening.To(Target);
    }

    public string? Notice(Kazo.Jarvis.Kernel.Localization.AssistantLanguage language) => Notices.TryDequeue(out var notice) ? notice : null;
}

/// <summary>The slice of the Claude Code sessions, scripted: it says the folder, then starts or says why not.</summary>
internal sealed class FakeClaudeTasks : Kazo.Jarvis.Kernel.ClaudeCode.IClaudeTasks
{
    public List<(Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskRequest Request, string? Context)> Started { get; } = [];

    /// <summary>What <see cref="StartAsync"/> says before it starts; <see langword="null"/> to start nothing.</summary>
    public string? Announcement { get; set; } = "Je lance Claude Code sur kazo-bar.";

    public string Refusal { get; set; } = "Sur quel dossier ? kazo-bar ou Notes.";

    public Exception? Failure { get; set; }

    /// <summary>The work given back by <see cref="Resume"/> for the request of this name.</summary>
    public Dictionary<string, Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskRequest> Waiting { get; } = [];

    public List<string> Resumed { get; } = [];

    /// <summary>What <see cref="ContinueAsync"/> answers for a task; a task that is not here is not the work of a session.</summary>
    public Dictionary<string, Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskOpening> Sessions { get; } = [];

    public List<(string Task, string Message)> Continued { get; } = [];

    public ValueTask<Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskOpening?> ContinueAsync(
        string taskId, string message, Kazo.Jarvis.Kernel.Localization.AssistantLanguage language, CancellationToken cancellationToken = default)
    {
        Continued.Add((taskId, message));
        return ValueTask.FromResult(Sessions.GetValueOrDefault(taskId));
    }

    public IReadOnlyList<string> Running => [];

    public Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskRequest? Resume(string request)
    {
        Resumed.Add(request);
        return Waiting.Remove(request, out var waiting) ? waiting : null;
    }

    public async ValueTask<Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskOpening> StartAsync(
        Kazo.Jarvis.Kernel.ClaudeCode.ClaudeTaskRequest request, Func<string, ValueTask> announce, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        if (Announcement is null)
        {
            return new(false, Refusal);
        }

        await announce(Announcement);
        cancellationToken.ThrowIfCancellationRequested();
        var context = request.Context is null ? null : await request.Context(cancellationToken);
        Started.Add((request, context));
        return new(true, "Claude Code travaille maintenant sur kazo-bar.", "task-1");
    }

    public ValueTask<bool> StopAsync(string taskId) => ValueTask.FromResult(false);
}
