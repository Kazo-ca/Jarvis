using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>One provider as saved in <c>config.json</c> (US1.8, ADR 0007, section 1). Never holds a key.</summary>
public sealed partial record LlmProviderSettings
{
    /// <summary>Stable identifier: lowercase letters, digits and dashes. Names the key in the vault.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Name shown to the user.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The protocol.</summary>
    public LlmProviderKind Kind { get; init; }

    /// <summary>The full address of the server, for instance <c>http://127.0.0.1:11435</c>.</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>The model, for instance <c>qwen3:14b</c>.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Whether a model that can reason does so before answering. Off by default (ADR 0007, section 5).</summary>
    public bool Think { get; init; }

    /// <summary>Context window in tokens (Ollama's <c>num_ctx</c>); 4096 by default, at most 8192 advised on a 12 GB card.</summary>
    public int ContextLength { get; init; } = 4096;

    /// <summary>Sampling temperature, 0.3 by default. Anthropic ignores it.</summary>
    public double Temperature { get; init; } = 0.3;

    /// <summary>Whether a local model is loaded at start and kept in memory while Jarvis runs (Ollama).</summary>
    public bool KeepLoaded { get; init; } = true;

    /// <summary>Largest answer, in tokens.</summary>
    public int MaxOutputTokens { get; init; } = 16000;

    /// <summary>
    /// Reference of the API key of a provider in the vault: <c>llm.&lt;id&gt;.apikey</c>. Derived from the id, so
    /// the settings never name a secret and the key follows the provider.
    /// </summary>
    /// <param name="id">The id of the provider.</param>
    /// <returns>The reference.</returns>
    public static SecretRef KeyReference(string id) => new($"llm.{id}.apikey");

    /// <summary>Whether <paramref name="id"/> is a valid identifier.</summary>
    /// <param name="id">The candidate.</param>
    /// <returns><see langword="true"/> for 1 to 40 lowercase letters, digits and dashes.</returns>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>The kernel description of this provider, or <see langword="null"/> when its address is not absolute.</summary>
    /// <returns>The provider for the connector.</returns>
    public LlmProvider? ToProvider() =>
        IsValidId(Id) && Uri.TryCreate(Address.Trim(), UriKind.Absolute, out var endpoint)
            ? new LlmProvider(
                Id,
                Name.Trim(),
                Kind,
                endpoint,
                Model.Trim(),
                new LlmTuning(Think, ContextLength, Temperature, KeepLoaded, MaxOutputTokens),
                KeyReference(Id))
            : null;

    /// <summary>The problems of this provider, empty when it can be saved.</summary>
    /// <returns>One sentence per problem, in English (the settings store logs them).</returns>
    public IEnumerable<string> Problems()
    {
        var label = string.IsNullOrWhiteSpace(Name) ? Id : Name;
        if (!IsValidId(Id))
        {
            yield return $"'{Id}' is not a valid provider id (1 to 40 lowercase letters, digits or dashes).";
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 60)
        {
            yield return $"Provider '{Id}' needs a name of 1 to 60 characters.";
        }

        if (!Enum.IsDefined(Kind))
        {
            yield return $"Provider '{label}' has an unknown kind.";
        }

        if (!Uri.TryCreate(Address.Trim(), UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            yield return $"Provider '{label}' needs an http or https address.";
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            yield return $"Provider '{label}' needs a model.";
        }

        if (ContextLength is < 512 or > 262_144)
        {
            yield return $"Provider '{label}': the context is between 512 and 262144 tokens.";
        }

        if (double.IsNaN(Temperature) || Temperature is < 0 or > 2)
        {
            yield return $"Provider '{label}': the temperature is between 0 and 2.";
        }

        if (MaxOutputTokens is < 1 or > 128_000)
        {
            yield return $"Provider '{label}': the longest answer is between 1 and 128000 tokens.";
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,38}[a-z0-9])?$")]
    private static partial Regex IdPattern();
}
