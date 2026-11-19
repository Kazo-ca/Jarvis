using System.Runtime.Versioning;
using Kazo.Jarvis.Adapters.Secrets;
using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

/// <summary>
/// The Windows Credential Manager vault (ADR 0004, section 4; US1.16). Runs on Windows only, on Benoit's PC,
/// under a prefix of its own that is deleted afterwards.
/// </summary>
[TestFixture]
[Category("Windows")]
[SupportedOSPlatform("windows")]
public sealed class CredentialManagerVaultTests
{
    private const string Secret = "sk-ant-WINDOWS-VALUE-456";

    private CredentialManagerVault vault = null!;

    [SetUp]
    public void SetUp()
    {
        Assume.That(OperatingSystem.IsWindows(), "Windows Credential Manager.");
        vault = new CredentialManagerVault($"Kazo.Jarvis.Tests.{Guid.NewGuid():N}/");
    }

    [TearDown]
    public async Task TearDown()
    {
        if (vault is null)
        {
            return;
        }

        foreach (var reference in await vault.ListAsync())
        {
            await vault.DeleteAsync(reference);
        }
    }

    [Test]
    public async Task A_secret_is_stored_read_listed_and_deleted()
    {
        var key = new SecretRef("llm.anthropic.apikey");

        await vault.SetAsync(key, new SecretValue(Secret));
        await vault.SetAsync(new SecretRef("a.first"), new SecretValue("été ✓"));

        Assert.Multiple(async () =>
        {
            Assert.That((await vault.GetAsync(key))?.Reveal(), Is.EqualTo(Secret));
            Assert.That((await vault.GetAsync(new SecretRef("a.first")))?.Reveal(), Is.EqualTo("été ✓"));
            Assert.That(await vault.ListAsync(), Is.EqualTo(new[] { new SecretRef("a.first"), key }));
            Assert.That(await vault.DeleteAsync(key), Is.True);
            Assert.That(await vault.DeleteAsync(key), Is.False);
            Assert.That(await vault.GetAsync(key), Is.Null);
        });
    }

    [Test]
    public async Task An_empty_vault_lists_nothing() =>
        Assert.That(await vault.ListAsync(), Is.Empty);

    [Test]
    public void A_value_too_long_is_refused_without_showing_it()
    {
        var tooLong = "X-SECRET-" + new string('z', CredentialManagerVault.MaximumValueBytes);

        var exception = Assert.ThrowsAsync<SecretVaultException>(async () => await vault.SetAsync(new SecretRef("big"), new SecretValue(tooLong)));

        Assert.That(exception!.Message, Does.Not.Contain("X-SECRET-"));
    }
}
