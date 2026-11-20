using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Features.Shared.Secrets;

/// <summary>What a <see cref="SecretField"/> shows about its secret.</summary>
public enum SecretEntryStatus
{
    /// <summary>Not read yet.</summary>
    Loading,

    /// <summary>No secret is stored under the reference.</summary>
    NotSet,

    /// <summary>A secret is stored; its value is never shown.</summary>
    Stored,

    /// <summary>The secret typed by the user was just saved.</summary>
    Saved,

    /// <summary>The secret was just deleted.</summary>
    Deleted,

    /// <summary>The vault could not be read or refused the change (ADR 0004, section 4).</summary>
    VaultUnavailable,
}

/// <summary>
/// Logic of the secret field (API keys of the providers, cloud voices, connectors): it only learns
/// whether a secret exists, from the list of references, and never reads the stored value back
/// (ADR 0004, section 4). Separate from the component so that it is tested without a browser.
/// </summary>
public sealed class SecretEntry
{
    private readonly ISecretVault vault;

    /// <summary>Creates the logic of one field.</summary>
    /// <param name="vault">The vault.</param>
    /// <param name="reference">The reference of the secret.</param>
    public SecretEntry(ISecretVault vault, SecretRef reference)
    {
        ArgumentNullException.ThrowIfNull(vault);
        this.vault = vault;
        Reference = reference;
    }

    /// <summary>The reference of the secret.</summary>
    public SecretRef Reference { get; }

    /// <summary>What the field shows.</summary>
    public SecretEntryStatus Status { get; private set; } = SecretEntryStatus.Loading;

    /// <summary>Whether a secret is stored under <see cref="Reference"/>.</summary>
    public bool IsStored => Status is SecretEntryStatus.Stored or SecretEntryStatus.Saved;

    /// <summary>Learns whether a secret is stored.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when <see cref="Status"/> is known.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await vault.ListAsync(cancellationToken).ConfigureAwait(false);
            Status = stored.Contains(Reference) ? SecretEntryStatus.Stored : SecretEntryStatus.NotSet;
        }
        catch (SecretVaultException)
        {
            Status = SecretEntryStatus.VaultUnavailable;
        }
    }

    /// <summary>Saves <paramref name="draft"/> as the secret. Blank text is ignored.</summary>
    /// <param name="draft">What the user typed.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns><see langword="true"/> when the secret was saved; the field then empties its input.</returns>
    public async Task<bool> SaveAsync(string? draft, CancellationToken cancellationToken = default)
    {
        // Spaces around a pasted key are a copy mistake, never part of the key.
        var value = draft?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            await vault.SetAsync(Reference, new SecretValue(value), cancellationToken).ConfigureAwait(false);
            Status = SecretEntryStatus.Saved;
            return true;
        }
        catch (SecretVaultException)
        {
            Status = SecretEntryStatus.VaultUnavailable;
            return false;
        }
    }

    /// <summary>Deletes the secret.</summary>
    /// <param name="cancellationToken">Cancels the deletion.</param>
    /// <returns>A task that completes when the secret is deleted.</returns>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await vault.DeleteAsync(Reference, cancellationToken).ConfigureAwait(false);
            Status = SecretEntryStatus.Deleted;
        }
        catch (SecretVaultException)
        {
            Status = SecretEntryStatus.VaultUnavailable;
        }
    }
}
