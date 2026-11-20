using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// Reads the API key of a provider from the vault at each request, so a key saved in the settings applies at
/// once and never stays in memory longer than a request (ADR 0004, section 4).
/// </summary>
internal sealed class LlmKeyReader
{
    private readonly ISecretVault? vault;

    public LlmKeyReader(ISecretVault? vault)
    {
        this.vault = vault;
    }

    /// <summary>
    /// The key of <paramref name="provider"/>, or <see langword="null"/> when it has none and needs none.
    /// A provider that needs a key and has none fails with <see cref="LlmFailure.KeyMissing"/>.
    /// </summary>
    public async Task<string?> ReadAsync(LlmProvider provider, CancellationToken cancellationToken)
    {
        if (provider.Key is { } reference && vault is not null)
        {
            try
            {
                if (await vault.GetAsync(reference, cancellationToken).ConfigureAwait(false) is { } value)
                {
                    return value.Reveal();
                }
            }
            catch (SecretVaultException exception) when (provider.Kind.RequiresKey())
            {
                throw new LlmException(LlmFailure.KeyMissing, $"The vault could not give the key '{reference}'.", exception);
            }
            catch (SecretVaultException)
            {
                // A local server rarely needs a key: without the vault it is called without one.
            }
        }

        return provider.Kind.RequiresKey()
            ? throw new LlmException(LlmFailure.KeyMissing, $"Provider '{provider.Id}' has no key in the vault.")
            : null;
    }
}
