namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>The four kinds of language model providers (ADR 0007, section 1).</summary>
public enum LlmProviderKind
{
    /// <summary>Ollama's own API (<c>/api/chat</c>), local. For instance Ollama in Docker on port 11435.</summary>
    Ollama,

    /// <summary>A local server that speaks <c>/v1/chat/completions</c>: <c>llama-server</c>, LM Studio...</summary>
    OpenAiCompatible,

    /// <summary>OpenAI's cloud API (ChatGPT).</summary>
    OpenAi,

    /// <summary>Anthropic's Messages API (Claude), cloud.</summary>
    Anthropic,
}

/// <summary>Helpers on <see cref="LlmProviderKind"/>.</summary>
public static class LlmProviderKinds
{
    /// <summary>Every kind, in the order the settings page lists them.</summary>
    public static IReadOnlyList<LlmProviderKind> All { get; } =
        [LlmProviderKind.Ollama, LlmProviderKind.OpenAiCompatible, LlmProviderKind.OpenAi, LlmProviderKind.Anthropic];

    /// <summary>Whether the model runs on the user's own machine or network. Only a local provider answers at stage 1.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for <see cref="LlmProviderKind.Ollama"/> and <see cref="LlmProviderKind.OpenAiCompatible"/>.</returns>
    public static bool IsLocal(this LlmProviderKind kind) => kind is LlmProviderKind.Ollama or LlmProviderKind.OpenAiCompatible;

    /// <summary>Whether a key from the vault is required to call the provider.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for the cloud kinds.</returns>
    public static bool RequiresKey(this LlmProviderKind kind) => !kind.IsLocal();
}
