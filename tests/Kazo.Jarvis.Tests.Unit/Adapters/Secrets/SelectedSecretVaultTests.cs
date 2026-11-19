using Kazo.Jarvis.Adapters.Secrets;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

/// <summary>Choosing the vault and switching moves the secrets (ADR 0004, section 4).</summary>
[TestFixture]
public sealed class SelectedSecretVaultTests
{
    private TemporaryFolder folder = null!;
    private FakeVault credentialManager = null!;
    private FakeVault encryptedFile = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        credentialManager = new FakeVault();
        encryptedFile = new FakeVault();
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task By_default_the_credential_manager_is_used()
    {
        var vault = Open(Settings());

        await vault.SetAsync(new SecretRef("a"), new SecretValue("1"));

        Assert.Multiple(() =>
        {
            Assert.That(vault.Current, Is.EqualTo(VaultKind.CredentialManager));
            Assert.That(credentialManager.Secrets.Keys, Is.EqualTo(new[] { "a" }));
            Assert.That(encryptedFile.Secrets, Is.Empty);
        });
    }

    [Test]
    public async Task Switching_moves_every_secret_and_saves_the_choice()
    {
        var settings = Settings();
        var vault = Open(settings);
        await vault.SetAsync(new SecretRef("a"), new SecretValue("1"));
        await vault.SetAsync(new SecretRef("b"), new SecretValue("2"));

        var moved = await vault.SwitchToAsync(VaultKind.EncryptedFile);

        Assert.Multiple(async () =>
        {
            Assert.That(moved, Is.EqualTo(2));
            Assert.That(vault.Current, Is.EqualTo(VaultKind.EncryptedFile));
            Assert.That(Settings().Read(VaultSettings.Section).Kind, Is.EqualTo(VaultKind.EncryptedFile));
            Assert.That(encryptedFile.Secrets, Is.EqualTo(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }));
            Assert.That(credentialManager.Secrets, Is.Empty);
            Assert.That((await vault.GetAsync(new SecretRef("b")))?.Reveal(), Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task Switching_to_the_current_vault_does_nothing()
    {
        var vault = Open(Settings());
        await vault.SetAsync(new SecretRef("a"), new SecretValue("1"));

        Assert.Multiple(async () =>
        {
            Assert.That(await vault.SwitchToAsync(VaultKind.CredentialManager), Is.Zero);
            Assert.That(credentialManager.Secrets.Keys, Is.EqualTo(new[] { "a" }));
        });
    }

    [Test]
    public async Task A_failed_copy_changes_nothing()
    {
        var vault = Open(Settings());
        await vault.SetAsync(new SecretRef("a"), new SecretValue("1"));
        await vault.SetAsync(new SecretRef("b"), new SecretValue("2"));
        encryptedFile.WritesBeforeFailure = 1;

        Assert.That(async () => await vault.SwitchToAsync(VaultKind.EncryptedFile), Throws.TypeOf<SecretVaultException>());
        Assert.Multiple(() =>
        {
            Assert.That(vault.Current, Is.EqualTo(VaultKind.CredentialManager));
            Assert.That(credentialManager.Secrets.Keys, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(encryptedFile.Secrets, Is.Empty);
        });
    }

    [Test]
    public async Task When_the_choice_cannot_be_saved_the_secrets_stay_where_they_were()
    {
        await credentialManager.SetAsync(new SecretRef("a"), new SecretValue("1"));
        Directory.CreateDirectory(folder.Paths.Root);
        await File.WriteAllTextAsync(folder.Paths.ConfigFile, "unreadable");
        var vault = Open(Settings());

        Assert.That(async () => await vault.SwitchToAsync(VaultKind.EncryptedFile),
            Throws.TypeOf<SecretVaultException>().With.Message.Contains("cannot be saved"));
        Assert.Multiple(() =>
        {
            Assert.That(credentialManager.Secrets.Keys, Is.EqualTo(new[] { "a" }));
            Assert.That(encryptedFile.Secrets, Is.Empty);
        });
    }

    [Test]
    public async Task A_secret_already_in_the_target_is_kept_when_the_switch_fails()
    {
        await credentialManager.SetAsync(new SecretRef("a"), new SecretValue("1"));
        await credentialManager.SetAsync(new SecretRef("b"), new SecretValue("2"));
        await encryptedFile.SetAsync(new SecretRef("a"), new SecretValue("older"));
        encryptedFile.WritesBeforeFailure = 1;
        var vault = Open(Settings());

        Assert.That(async () => await vault.SwitchToAsync(VaultKind.EncryptedFile), Throws.TypeOf<SecretVaultException>());
        Assert.That(encryptedFile.Secrets.Keys, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void Every_vault_kind_needs_a_vault() =>
        Assert.That(
            () => new SelectedSecretVault(Settings(), new Dictionary<VaultKind, ISecretVault> { [VaultKind.CredentialManager] = credentialManager }),
            Throws.ArgumentException);

    private JsonFileSettingsStore Settings() => new(folder.Paths, [], new FakeTimeProvider());

    private SelectedSecretVault Open(ISettingsStore settings) => new(
        settings,
        new Dictionary<VaultKind, ISecretVault>
        {
            [VaultKind.CredentialManager] = credentialManager,
            [VaultKind.EncryptedFile] = encryptedFile,
        });
}
