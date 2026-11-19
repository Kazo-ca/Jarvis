using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Secrets;

/// <summary>
/// The vault the slices see: it works on the vault chosen in the settings (<see cref="VaultSettings"/>) and
/// moves the secrets when the user switches (ADR 0004, section 4). Operations wait while a switch runs.
/// </summary>
public sealed class SelectedSecretVault : ISecretVault, ISecretVaultSelector
{
    private readonly ISettingsStore settings;
    private readonly IReadOnlyDictionary<VaultKind, ISecretVault> vaults;
    private readonly ILogger logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Creates the vault.</summary>
    /// <param name="settings">Where the choice is saved.</param>
    /// <param name="vaults">One vault per <see cref="VaultKind"/>.</param>
    /// <param name="logger">Log; it receives references and counts, never values.</param>
    public SelectedSecretVault(
        ISettingsStore settings,
        IReadOnlyDictionary<VaultKind, ISecretVault> vaults,
        ILogger<SelectedSecretVault>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(vaults);
        foreach (var kind in Enum.GetValues<VaultKind>())
        {
            if (!vaults.ContainsKey(kind))
            {
                throw new ArgumentException($"No vault given for {kind}.", nameof(vaults));
            }
        }

        this.settings = settings;
        this.vaults = vaults;
        this.logger = logger ?? NullLogger<SelectedSecretVault>.Instance;
    }

    /// <inheritdoc />
    public VaultKind Current => settings.Read(VaultSettings.Section).Kind;

    /// <inheritdoc />
    public ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default) =>
        WithActiveAsync(async (vault, token) => { await vault.SetAsync(reference, value, token); return true; }, cancellationToken).Discard();

    /// <inheritdoc />
    public ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default) =>
        WithActiveAsync((vault, token) => vault.GetAsync(reference, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default) =>
        WithActiveAsync((vault, token) => vault.DeleteAsync(reference, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default) =>
        WithActiveAsync((vault, token) => vault.ListAsync(token), cancellationToken);

    /// <inheritdoc />
    public async ValueTask<int> SwitchToAsync(VaultKind target, CancellationToken cancellationToken = default)
    {
        if (!vaults.TryGetValue(target, out var destination))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown vault.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = Current;
            if (current == target)
            {
                return 0;
            }

            var source = vaults[current];
            var references = await source.ListAsync(cancellationToken);
            var alreadyThere = (await destination.ListAsync(cancellationToken)).ToHashSet();
            var copied = new List<SecretRef>();
            try
            {
                foreach (var reference in references)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await source.GetAsync(reference, cancellationToken) is { } value)
                    {
                        await destination.SetAsync(reference, value, cancellationToken);
                        copied.Add(reference);
                    }
                }

                var saved = await settings.WriteAsync(VaultSettings.Section, new VaultSettings { Kind = target }, CancellationToken.None);
                if (!saved.IsSaved)
                {
                    throw new SecretVaultException($"The choice of vault cannot be saved ({saved.Outcome}); the secrets stay in {current}.");
                }
            }
            catch
            {
                await UndoCopyAsync(destination, copied.Where(reference => !alreadyThere.Contains(reference)));
                throw;
            }

            foreach (var reference in copied)
            {
                try
                {
                    await source.DeleteAsync(reference, CancellationToken.None);
                }
                catch (SecretVaultException exception)
                {
                    logger.LogWarning(exception, "Secret {Reference} was moved to {Target} but could not be deleted from {Source}.", reference, target, current);
                }
            }

            logger.LogInformation("Vault switched from {Source} to {Target}: {Count} secrets moved.", current, target, copied.Count);
            return copied.Count;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask UndoCopyAsync(ISecretVault destination, IEnumerable<SecretRef> references)
    {
        foreach (var reference in references)
        {
            try
            {
                await destination.DeleteAsync(reference, CancellationToken.None);
            }
            catch (SecretVaultException exception)
            {
                logger.LogWarning(exception, "Secret {Reference} could not be removed from the target vault after a failed switch.", reference);
            }
        }
    }

    private async ValueTask<TResult> WithActiveAsync<TResult>(
        Func<ISecretVault, CancellationToken, ValueTask<TResult>> operation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation(vaults[Current], cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>Turns a <see cref="ValueTask{TResult}"/> into a <see cref="ValueTask"/>.</summary>
internal static class ValueTaskExtensions
{
    public static async ValueTask Discard<T>(this ValueTask<T> task) => await task;
}
