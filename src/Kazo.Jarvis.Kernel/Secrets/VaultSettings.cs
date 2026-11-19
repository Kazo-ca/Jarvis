using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>
/// The choice of vault, saved in <c>config.json</c>. In the kernel because the vault adapter reads it; only
/// <see cref="ISecretVaultSelector"/> changes it, since switching must move the secrets.
/// </summary>
public sealed record VaultSettings
{
    /// <summary>The section, <c>Secrets.Vault</c>.</summary>
    public static SettingsSection<VaultSettings> Section { get; } = new(
        "Secrets.Vault",
        () => new VaultSettings(),
        value => Enum.IsDefined(value.Kind) ? [] : [$"Unknown vault '{value.Kind}'."]);

    /// <summary>The vault in use; Windows Credential Manager by default.</summary>
    public VaultKind Kind { get; init; } = VaultKind.CredentialManager;
}
