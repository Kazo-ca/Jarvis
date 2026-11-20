using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Kazo.Jarvis.Adapters.Secrets;
using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

/// <summary>The encrypted secrets file, secrets.json (ADR 0004, section 4; US1.16).</summary>
[TestFixture]
public sealed class EncryptedFileVaultTests
{
    private const string Secret = "sk-ant-THE-VALUE-123";
    private static readonly SecretRef Key = new("llm.anthropic.apikey");

    private TemporaryFolder folder = null!;

    [SetUp]
    public void SetUp() => folder = new TemporaryFolder();

    [TearDown]
    public void TearDown() => folder.Dispose();

    private string SecretsFile => folder.Paths.SecretsFile;

    [Test]
    public async Task A_secret_is_stored_read_listed_and_deleted()
    {
        var vault = Open();

        await vault.SetAsync(Key, new SecretValue(Secret));
        await vault.SetAsync(new SecretRef("a.first"), new SecretValue("other"));
        var reopened = Open();

        Assert.Multiple(async () =>
        {
            Assert.That((await reopened.GetAsync(Key))?.Reveal(), Is.EqualTo(Secret));
            Assert.That(await reopened.ListAsync(), Is.EqualTo(new[] { new SecretRef("a.first"), Key }));
            Assert.That(await reopened.DeleteAsync(Key), Is.True);
            Assert.That(await reopened.DeleteAsync(Key), Is.False);
            Assert.That(await reopened.GetAsync(Key), Is.Null);
        });
    }

    [Test]
    public async Task A_secret_is_replaced()
    {
        var vault = Open();

        await vault.SetAsync(Key, new SecretValue("old"));
        await vault.SetAsync(Key, new SecretValue("new"));

        Assert.That((await vault.GetAsync(Key))?.Reveal(), Is.EqualTo("new"));
    }

    [Test]
    public async Task No_file_means_no_secret()
    {
        var vault = Open();

        Assert.Multiple(async () =>
        {
            Assert.That(await vault.ListAsync(), Is.Empty);
            Assert.That(await vault.GetAsync(Key), Is.Null);
            Assert.That(File.Exists(SecretsFile), Is.False);
        });
    }

    [Test]
    public async Task The_value_is_not_in_clear_in_the_file_but_the_reference_is()
    {
        await Open().SetAsync(Key, new SecretValue(Secret));

        var content = await File.ReadAllTextAsync(SecretsFile);
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Not.Contain(Secret));
            Assert.That(content, Does.Not.Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes(Secret))));
            Assert.That(content, Does.Contain(Key.Value));
            Assert.That(content, Does.Contain("\"schemaVersion\": 1"));
        });
    }

    [TestCase("not json")]
    [TestCase("""{ "secrets": {} }""")]
    [TestCase("""{ "schemaVersion": 1 }""")]
    public async Task An_unreadable_file_fails_and_is_never_overwritten(string content)
    {
        Directory.CreateDirectory(folder.Paths.Root);
        await File.WriteAllTextAsync(SecretsFile, content);
        var vault = Open();

        Assert.Multiple(async () =>
        {
            Assert.That(async () => await vault.SetAsync(Key, new SecretValue(Secret)),
                Throws.TypeOf<SecretVaultException>().With.Message.Contains("cannot be read"));
            Assert.That(async () => await vault.ListAsync(), Throws.TypeOf<SecretVaultException>());
            Assert.That(await File.ReadAllTextAsync(SecretsFile), Is.EqualTo(content));
        });
    }

    [Test]
    public async Task A_file_from_a_newer_version_is_never_overwritten()
    {
        const string content = """{ "schemaVersion": 2, "secrets": {} }""";
        Directory.CreateDirectory(folder.Paths.Root);
        await File.WriteAllTextAsync(SecretsFile, content);

        Assert.Multiple(async () =>
        {
            Assert.That(async () => await Open().SetAsync(Key, new SecretValue(Secret)),
                Throws.TypeOf<SecretVaultException>().With.Message.Contains("newer version"));
            Assert.That(await File.ReadAllTextAsync(SecretsFile), Is.EqualTo(content));
        });
    }

    [Test]
    public async Task A_value_that_cannot_be_decrypted_fails_without_showing_anything()
    {
        await Open().SetAsync(Key, new SecretValue(Secret));
        var otherUser = new EncryptedFileVault(SecretsFile, new FailingProtector());

        var exception = Assert.ThrowsAsync<SecretVaultException>(async () => await otherUser.GetAsync(Key));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(Key.Value).And.Not.Contain(Secret));
            Assert.That(exception.InnerException, Is.Null);
        });
    }

    [Test]
    [Category("Windows")]
    [SupportedOSPlatform("windows")]
    public async Task With_dpapi_the_value_is_read_back_by_the_same_user()
    {
        var vault = new EncryptedFileVault(SecretsFile, new DpapiSecretProtector());

        await vault.SetAsync(Key, new SecretValue(Secret));

        Assert.Multiple(async () =>
        {
            Assert.That((await new EncryptedFileVault(SecretsFile, new DpapiSecretProtector()).GetAsync(Key))?.Reveal(), Is.EqualTo(Secret));
            Assert.That(await File.ReadAllTextAsync(SecretsFile), Does.Not.Contain(Secret));
        });
    }

    private EncryptedFileVault Open() => new(SecretsFile, new ReversingProtector());

    /// <summary>Stands in for DPAPI on Linux: a reversible change, so the value is not in clear.</summary>
    private sealed class ReversingProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plain) => [0x4A, .. plain.Select(value => (byte)(value ^ 0x5C)).Reverse()];

        public byte[] Unprotect(byte[] encrypted) => [.. encrypted.Skip(1).Reverse().Select(value => (byte)(value ^ 0x5C))];
    }

    /// <summary>What DPAPI does for another user or machine.</summary>
    private sealed class FailingProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plain) => throw new CryptographicException("Key not valid for use in specified state.");

        public byte[] Unprotect(byte[] encrypted) => throw new CryptographicException("Key not valid for use in specified state.");
    }
}
