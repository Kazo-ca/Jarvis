using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated vault for the Web host (ADR 0004, section 4): secrets in memory, lost when the host stops.
/// Switching vaults only changes <see cref="Current"/>: both simulated vaults are the same memory.
/// </summary>
public sealed class InMemorySecretVault : ISecretVault, ISecretVaultSelector
{
    private readonly Lock gate = new();
    private readonly SortedDictionary<string, SecretValue> secrets = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public VaultKind Current { get; private set; } = VaultKind.CredentialManager;

    /// <inheritdoc />
    public ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            secrets[reference.Value] = value;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return ValueTask.FromResult(secrets.GetValueOrDefault(reference.Value));
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return ValueTask.FromResult(secrets.Remove(reference.Value));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return ValueTask.FromResult<IReadOnlyList<SecretRef>>(secrets.Keys.Select(key => new SecretRef(key)).ToArray());
        }
    }

    /// <inheritdoc />
    public ValueTask<int> SwitchToAsync(VaultKind target, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (target == Current)
            {
                return ValueTask.FromResult(0);
            }

            Current = target;
            return ValueTask.FromResult(secrets.Count);
        }
    }
}
