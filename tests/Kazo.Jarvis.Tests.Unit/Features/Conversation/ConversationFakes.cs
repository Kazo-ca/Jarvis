using System.Runtime.CompilerServices;
using Kazo.Jarvis.Kernel.Llm;
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
                    ? new ChatResponseUpdate(ChatRole.Assistant, [call]) { ModelId = "qwen3:14b" }
                    : new ChatResponseUpdate(ChatRole.Assistant, (string)item) { ModelId = "qwen3:14b" };
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10 })])
            {
                ModelId = "qwen3:14b",
                FinishReason = ChatFinishReason.ToolCalls,
            };
            yield break;
        }

        for (var i = 0; i < Pieces.Count; i++)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, Pieces[i]) { ModelId = "qwen3:14b" };
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
            ModelId = "qwen3:14b",
            FinishReason = ChatFinishReason.Stop,
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
