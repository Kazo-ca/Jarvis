namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>
/// The user's choice of vault (ADR 0004, section 4). The <see cref="ISecretVault"/> of the container always
/// works on the chosen one; switching moves the secrets.
/// </summary>
public interface ISecretVaultSelector
{
    /// <summary>The vault in use.</summary>
    VaultKind Current { get; }

    /// <summary>
    /// Uses <paramref name="target"/> from now on: copies every secret to it, saves the choice, then deletes
    /// them from the previous vault. Nothing changes when a step before the save fails.
    /// </summary>
    /// <param name="target">The vault to use.</param>
    /// <param name="cancellationToken">Cancels the switch before the choice is saved.</param>
    /// <returns>The number of secrets moved.</returns>
    /// <exception cref="SecretVaultException">A vault refused an operation, or the choice cannot be saved.</exception>
    ValueTask<int> SwitchToAsync(VaultKind target, CancellationToken cancellationToken = default);
}

/// <summary>The two vaults the user can choose from.</summary>
public enum VaultKind
{
    /// <summary>Windows Credential Manager, the default.</summary>
    CredentialManager,

    /// <summary>A <c>secrets.json</c> file encrypted with DPAPI for the Windows user.</summary>
    EncryptedFile,
}
