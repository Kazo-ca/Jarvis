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

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

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

    public ValueTask<MemoryRecall> RecallAsync(AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default)
    {
        Asked.Add((language, contextLength));
        return Failure is null ? ValueTask.FromResult(Recall) : ValueTask.FromException<MemoryRecall>(Failure);
    }
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

    public HandOffOpening Open(HandOffKind kind)
    {
        Opened.Add(kind);
        return kind == HandOffKind.Task ? HandOffOpening.Refused(HandOffRefusal.NoTasksTarget)
            : Target is null ? HandOffOpening.Refused(HandOffRefusal.NoQuestionsTarget)
            : HandOffOpening.To(Target);
    }
}
