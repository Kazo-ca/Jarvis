using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

/// <summary>An in-memory vault that can be told to refuse writes, to test the switch between vaults.</summary>
internal sealed class FakeVault : ISecretVault
{
    public SortedDictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    /// <summary>Number of successful writes before every next one fails; unlimited when null.</summary>
    public int? WritesBeforeFailure { get; set; }

    public ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default)
    {
        if (WritesBeforeFailure is { } left)
        {
            if (left <= 0)
            {
                throw new SecretVaultException($"Fake vault refuses {reference}.");
            }

            WritesBeforeFailure = left - 1;
        }

        Secrets[reference.Value] = value.Reveal();
        return ValueTask.CompletedTask;
    }

    public ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Secrets.TryGetValue(reference.Value, out var value) ? new SecretValue(value) : null);

    public ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Secrets.Remove(reference.Value));

    public ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<SecretRef>>(Secrets.Keys.Select(key => new SecretRef(key)).ToArray());
}
