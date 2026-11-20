using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>A connector the test steers: which models are loaded, which loads fail, and how long tests take.</summary>
internal sealed class FakeConnector : ILlmConnector
{
    public HashSet<string> Loaded { get; } = [];

    public Queue<LlmFailure?> PreloadOutcomes { get; } = new();

    public List<LlmProvider> Preloaded { get; } = [];

    public List<LlmProvider> Tested { get; } = [];

    public List<FakeClient> Clients { get; } = [];

    public TaskCompletionSource? HoldTest { get; set; }

    public LlmConnectionResult TestResult { get; set; } = LlmConnectionResult.Passed(TimeSpan.FromMilliseconds(5), ["qwen3:14b"]);

    /// <summary>When set, answers each test by provider instead of <see cref="TestResult"/>.</summary>
    public Func<LlmProvider, LlmConnectionResult>? Answer { get; set; }

    public IChatClient CreateClient(LlmProvider provider)
    {
        var client = new FakeClient(provider);
        Clients.Add(client);
        return client;
    }

    public async Task<LlmConnectionResult> TestConnectionAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        Tested.Add(provider);
        if (HoldTest is not null)
        {
            await HoldTest.Task.WaitAsync(cancellationToken);
        }

        return Answer?.Invoke(provider) ?? TestResult;
    }

    public Task<bool?> IsLoadedAsync(LlmProvider provider, CancellationToken cancellationToken = default) =>
        Task.FromResult<bool?>(provider.Kind == LlmProviderKind.Ollama ? Loaded.Contains(provider.Model) : null);

    public Task PreloadAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        Preloaded.Add(provider);
        if (PreloadOutcomes.TryDequeue(out var failure) && failure is { } cause)
        {
            return Task.FromException(new LlmException(cause, "fake failure"));
        }

        Loaded.Add(provider.Model);
        return Task.CompletedTask;
    }

    internal sealed class FakeClient(LlmProvider provider) : IChatClient
    {
        public LlmProvider Provider { get; } = provider;

        public bool Disposed { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() => Disposed = true;
    }
}
