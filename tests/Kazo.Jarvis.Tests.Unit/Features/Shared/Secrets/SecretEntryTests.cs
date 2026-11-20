using Kazo.Jarvis.Features.Shared.Secrets;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Features.Shared.Secrets;

/// <summary>The secret field (ADR 0004, section 4): it learns whether a secret exists, never its value.</summary>
[TestFixture]
public sealed class SecretEntryTests
{
    private static readonly SecretRef Reference = new("llm.anthropic.apikey");

    [Test]
    public async Task A_missing_secret_shows_as_not_set()
    {
        var entry = new SecretEntry(new FakeVault(), Reference);

        await entry.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Status, Is.EqualTo(SecretEntryStatus.NotSet));
            Assert.That(entry.IsStored, Is.False);
        });
    }

    [Test]
    public async Task A_stored_secret_shows_as_stored_without_being_read()
    {
        var vault = new CountingVault();
        vault.Secrets[Reference.Value] = "sk-test";
        var entry = new SecretEntry(vault, Reference);

        await entry.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Status, Is.EqualTo(SecretEntryStatus.Stored));
            Assert.That(vault.Reads, Is.Zero, "The field never reads the stored value back.");
        });
    }

    [Test]
    public async Task Saving_trims_the_pasted_key_and_stores_it()
    {
        var vault = new FakeVault();
        var entry = new SecretEntry(vault, Reference);
        await entry.LoadAsync();

        var saved = await entry.SaveAsync("  sk-new \n");

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(entry.Status, Is.EqualTo(SecretEntryStatus.Saved));
            Assert.That(vault.Secrets[Reference.Value], Is.EqualTo("sk-new"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Blank_text_is_not_saved(string? draft)
    {
        var vault = new FakeVault();
        var entry = new SecretEntry(vault, Reference);
        await entry.LoadAsync();

        var saved = await entry.SaveAsync(draft);

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.False);
            Assert.That(entry.Status, Is.EqualTo(SecretEntryStatus.NotSet));
            Assert.That(vault.Secrets, Is.Empty);
        });
    }

    [Test]
    public async Task A_refused_save_says_the_vault_is_unavailable()
    {
        var entry = new SecretEntry(new FakeVault { WritesBeforeFailure = 0 }, Reference);

        var saved = await entry.SaveAsync("sk-new");

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.False);
            Assert.That(entry.Status, Is.EqualTo(SecretEntryStatus.VaultUnavailable));
        });
    }

    [Test]
    public async Task Deleting_removes_the_secret()
    {
        var vault = new FakeVault();
        vault.Secrets[Reference.Value] = "sk-old";
        var entry = new SecretEntry(vault, Reference);
        await entry.LoadAsync();

        await entry.DeleteAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Status, Is.EqualTo(SecretEntryStatus.Deleted));
            Assert.That(entry.IsStored, Is.False);
            Assert.That(vault.Secrets, Is.Empty);
        });
    }

    private sealed class CountingVault : ISecretVault
    {
        private readonly FakeVault inner = new();

        public SortedDictionary<string, string> Secrets => inner.Secrets;

        public int Reads { get; private set; }

        public ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default) =>
            inner.SetAsync(reference, value, cancellationToken);

        public ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.GetAsync(reference, cancellationToken);
        }

        public ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(reference, cancellationToken);

        public ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default) =>
            inner.ListAsync(cancellationToken);
    }
}
