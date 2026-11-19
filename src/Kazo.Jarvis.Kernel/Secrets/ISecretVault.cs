namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>
/// The secret vault (ADR 0004, section 4; US1.16): API keys, then tokens. The settings only keep a
/// <see cref="SecretRef"/>. No value ever goes into a log or an error message.
/// </summary>
public interface ISecretVault
{
    /// <summary>Creates or replaces a secret.</summary>
    /// <param name="reference">Its reference.</param>
    /// <param name="value">The secret.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the secret is stored.</returns>
    /// <exception cref="SecretVaultException">The vault refused it.</exception>
    ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default);

    /// <summary>Reads a secret.</summary>
    /// <param name="reference">Its reference.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The secret, or <see langword="null"/> when there is none for this reference.</returns>
    /// <exception cref="SecretVaultException">The vault cannot be read.</exception>
    ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default);

    /// <summary>Deletes a secret.</summary>
    /// <param name="reference">Its reference.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when there was one.</returns>
    /// <exception cref="SecretVaultException">The vault refused it.</exception>
    ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default);

    /// <summary>Lists the references of the stored secrets, never their values.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The references, sorted.</returns>
    /// <exception cref="SecretVaultException">The vault cannot be read.</exception>
    ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default);
}
