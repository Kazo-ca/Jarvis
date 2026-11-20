using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>
/// One configured provider (ADR 0007, section 1): what an adapter needs to reach a model. Several providers of
/// the same kind may coexist; they differ by <see cref="Id"/>.
/// </summary>
/// <param name="Id">Stable identifier, used in the settings and in the logs.</param>
/// <param name="Name">Name shown to the user.</param>
/// <param name="Kind">The protocol.</param>
/// <param name="Endpoint">The full address of the server, for instance <c>http://127.0.0.1:11435</c>.</param>
/// <param name="Model">The model asked for, for instance <c>qwen3:14b</c>.</param>
/// <param name="Tuning">Generation settings (ADR 0007, section 5).</param>
/// <param name="Key">Reference of the API key in the vault; <see langword="null"/> when the server needs none.</param>
public sealed record LlmProvider(
    string Id,
    string Name,
    LlmProviderKind Kind,
    Uri Endpoint,
    string Model,
    LlmTuning Tuning,
    SecretRef? Key = null);

/// <summary>Generation settings of a provider (ADR 0007, section 5).</summary>
/// <param name="Think">
/// Whether a model that can reason before answering does so. Off by default: with it, <c>qwen3:14b</c> takes 5.5 s
/// instead of 0.9 s to pick a tool (spike E7). Ignored by models that cannot reason.
/// </param>
/// <param name="ContextLength">Context window in tokens, sent to Ollama (<c>num_ctx</c>). 4096 by default (spike E5).</param>
/// <param name="Temperature">Sampling temperature, 0.3 by default (spike E4). Not sent to Anthropic, which refuses it.</param>
/// <param name="KeepLoaded">Whether a local model is loaded at start and kept in memory while Jarvis runs (Ollama only).</param>
/// <param name="MaxOutputTokens">Largest answer, in tokens. Required by Anthropic; a bound for the others.</param>
public sealed record LlmTuning(
    bool Think = false,
    int ContextLength = 4096,
    double Temperature = 0.3,
    bool KeepLoaded = true,
    int MaxOutputTokens = 16000);
