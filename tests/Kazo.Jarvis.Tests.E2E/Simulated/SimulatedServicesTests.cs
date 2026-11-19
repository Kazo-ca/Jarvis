using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Web.Simulated;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.E2E.Simulated;

/// <summary>
/// The simulated settings, store and vault of the Web host keep the contracts of the real adapters
/// (ADR 0003, section 3; ADR 0004), so a flow tested on the Web host behaves the same on Windows.
/// No browser here: these tests sit with the Web host's other tests because the unit test project does not
/// reference the host.
/// </summary>
[TestFixture]
public sealed class SimulatedServicesTests
{
    private static readonly SettingsSection<Sample> Section = new(
        "Test.Sample", () => new Sample(), value => value.Volume > 100 ? ["Too loud."] : []);

    private ServiceProvider provider = null!;

    [SetUp]
    public void SetUp() => provider = new ServiceCollection().AddJarvisSimulatedServices().BuildServiceProvider();

    [TearDown]
    public void TearDown() => provider.Dispose();

    [Test]
    public async Task Settings_start_as_a_first_run_validate_save_and_notify()
    {
        var settings = provider.GetRequiredService<ISettingsStore>();
        var received = new List<int>();
        using var watch = settings.Watch(Section, value => received.Add(value.Volume));

        var invalid = await settings.WriteAsync(Section, new Sample { Volume = 101 });
        var saved = await settings.WriteAsync(Section, new Sample { Volume = 20 });

        Assert.Multiple(() =>
        {
            Assert.That(settings.State.IsFirstRun, Is.True);
            Assert.That(invalid.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(saved.IsSaved, Is.True);
            Assert.That(settings.Read(Section).Volume, Is.EqualTo(20));
            Assert.That(received, Is.EqualTo(new[] { 20 }));
        });
    }

    [Test]
    public async Task Read_only_settings_refuse_to_save()
    {
        var settings = new InMemorySettingsStore(new SettingsLoadState(SettingsLoadOutcome.Unreadable));

        var result = await settings.WriteAsync(Section, new Sample { Volume = 20 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.ReadOnly));
            Assert.That(settings.Read(Section), Is.EqualTo(new Sample()));
        });
    }

    [Test]
    public async Task The_store_reads_periods_in_order_and_erases_everything()
    {
        var store = provider.GetRequiredService<IDocumentStore>();
        var turns = store.Collection<Sample>("Conversation.Turns");
        var start = new DateTimeOffset(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);
        await turns.AddAsync(new Sample { Volume = 2 }, start.AddSeconds(2));
        var first = await turns.AddAsync(new Sample { Volume = 1 }, start);
        await turns.AddAsync(new Sample { Volume = 3 }, start.AddSeconds(10));
        await store.Collection<Sample>("Memory.Facts").AddAsync(new Sample(), start);

        var period = await turns.ReadPeriodAsync(start, start.AddSeconds(10));
        var found = await turns.GetAsync(first.Id);
        var elsewhere = await store.Collection<Sample>("Memory.Facts").GetAsync(first.Id);
        await store.EraseAllAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(period.Select(document => document.Content.Volume), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(found?.Content.Volume, Is.EqualTo(1));
            Assert.That(elsewhere, Is.Null);
            Assert.That(await turns.ReadPeriodAsync(start, start.AddDays(1)), Is.Empty);
            Assert.That(await store.Collection<Sample>("Memory.Facts").ReadPeriodAsync(start, start.AddDays(1)), Is.Empty);
            Assert.That(() => store.Collection<Sample>("bad name"), Throws.ArgumentException);
        });
    }

    [Test]
    public async Task The_vault_keeps_secrets_and_switching_keeps_them()
    {
        var vault = provider.GetRequiredService<ISecretVault>();
        var selector = provider.GetRequiredService<ISecretVaultSelector>();
        await vault.SetAsync(new SecretRef("b"), new SecretValue("2"));
        await vault.SetAsync(new SecretRef("a"), new SecretValue("1"));

        var moved = await selector.SwitchToAsync(VaultKind.EncryptedFile);

        Assert.Multiple(async () =>
        {
            Assert.That(moved, Is.EqualTo(2));
            Assert.That(selector.Current, Is.EqualTo(VaultKind.EncryptedFile));
            Assert.That(await vault.ListAsync(), Is.EqualTo(new[] { new SecretRef("a"), new SecretRef("b") }));
            Assert.That((await vault.GetAsync(new SecretRef("a")))?.Reveal(), Is.EqualTo("1"));
            Assert.That(await vault.DeleteAsync(new SecretRef("a")), Is.True);
        });
    }

    private sealed record Sample
    {
        public int Volume { get; init; } = 50;
    }
}
