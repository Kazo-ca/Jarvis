using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>The providers and the default one (US1.8, US1.9), section <c>Llm.Providers</c> of <c>config.json</c>.</summary>
public sealed record LlmProvidersOptions
{
    /// <summary>Id of the provider created with the default settings.</summary>
    public const string LocalId = "local";

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<LlmProvidersOptions> Section { get; } = new("Llm.Providers", () => new LlmProvidersOptions(), Validate);

    /// <summary>
    /// The providers. By default one: Ollama on its usual port with <c>qwen3:14b</c> (ADR 0007, section 5). The
    /// port is Ollama's standard one, 11434; Benoit's Ollama runs in Docker on 11435 and is set once in the
    /// settings (provisional choice: the first-run wizard of work item 14 may look for it).
    /// </summary>
    public IReadOnlyList<LlmProviderSettings> Providers { get; init; } = [Template(LlmProviderKind.Ollama, LocalId, "Ollama")];

    /// <summary>
    /// Id of the provider that answers (US1.9). It must be a local provider at stage 1: cloud providers answer
    /// from stage 2, through the hand-off and under the spending cap (ADR 0007, section 1).
    /// </summary>
    public string? DefaultId { get; init; } = LocalId;

    /// <summary>The default provider, or <see langword="null"/> when there is none.</summary>
    public LlmProviderSettings? Default => Providers.FirstOrDefault(p => p.Id == DefaultId);

    /// <summary>A new provider of <paramref name="kind"/> with the usual address and model of that kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">Its id.</param>
    /// <param name="name">Its name.</param>
    /// <returns>The provider, to adjust.</returns>
    public static LlmProviderSettings Template(LlmProviderKind kind, string id, string name) => kind switch
    {
        LlmProviderKind.Ollama => new() { Id = id, Name = name, Kind = kind, Address = "http://127.0.0.1:11434", Model = "qwen3:14b" },

        // llama-server serves the model it was started with; « Tester la connexion » lists its name.
        LlmProviderKind.OpenAiCompatible => new() { Id = id, Name = name, Kind = kind, Address = "http://127.0.0.1:8080", Model = "default", KeepLoaded = false },
        LlmProviderKind.OpenAi => new() { Id = id, Name = name, Kind = kind, Address = "https://api.openai.com/v1", Model = string.Empty, KeepLoaded = false },
        LlmProviderKind.Anthropic => new() { Id = id, Name = name, Kind = kind, Address = "https://api.anthropic.com", Model = "claude-opus-5-5", KeepLoaded = false },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static IReadOnlyList<string> Validate(LlmProvidersOptions value)
    {
        var problems = new List<string>();
        if (value.Providers is null)
        {
            return ["The list of providers is missing."];
        }

        foreach (var provider in value.Providers)
        {
            problems.AddRange(provider.Problems());
        }

        problems.AddRange(value.Providers
            .GroupBy(p => p.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"Two providers have the id '{group.Key}'."));

        if (value.DefaultId is not null)
        {
            if (value.Default is not { } chosen)
            {
                problems.Add($"The default provider '{value.DefaultId}' is not in the list.");
            }
            else if (!chosen.Kind.IsLocal())
            {
                problems.Add($"The default provider '{chosen.Name}' must be local at stage 1 (Ollama or an OpenAI-compatible server).");
            }
        }

        return problems;
    }
}
