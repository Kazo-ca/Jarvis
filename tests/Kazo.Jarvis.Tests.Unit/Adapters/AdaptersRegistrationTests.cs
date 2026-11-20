using System.Runtime.Versioning;
using Kazo.Jarvis.Adapters;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.Secrets;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Speech;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.Unit.Adapters;

/// <summary>What the Windows host gets from <c>AddJarvisAdapters</c> (ADR 0002, section 6; ADR 0004).</summary>
[TestFixture]
public sealed class AdaptersRegistrationTests
{
    private TemporaryFolder folder = null!;

    [SetUp]
    public void SetUp() => folder = new TemporaryFolder();

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public void Settings_and_storage_use_the_given_folder()
    {
        using var provider = new ServiceCollection().AddJarvisAdapters(folder.Paths).BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<ISettingsStore>(), Is.TypeOf<JsonFileSettingsStore>());
            Assert.That(provider.GetRequiredService<IDocumentStore>(), Is.TypeOf<SqliteDocumentStore>());
            Assert.That(provider.GetRequiredService<JarvisPaths>(), Is.EqualTo(folder.Paths));
        });
    }

    [Test]
    public void The_four_voice_engines_are_registered_local_ones_first()
    {
        using var provider = new ServiceCollection().AddJarvisAdapters(folder.Paths).BuildServiceProvider();

        var engines = provider.GetServices<ISpeechEngine>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(engines.Select(engine => engine.Engine), Is.EqualTo(new[] { "piper", "kokoro", "elevenlabs", "openai" }));
            Assert.That(engines.Select(engine => engine.IsCloud), Is.EqualTo(new[] { false, false, true, true }));
            Assert.That(engines[0], Is.SameAs(provider.GetRequiredService<PiperSpeechEngine>()));
        });
    }

    [Test]
    public void The_default_root_is_dot_kazo_jarvis_in_the_user_folder()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kazo", "jarvis");

        Assert.That(JarvisPaths.ForCurrentUser().Root, Is.EqualTo(expected));
    }

    [Test]
    public void The_folders_of_adr_0004_are_created()
    {
        folder.Paths.EnsureCreated();

        var created = Directory.GetDirectories(folder.Paths.Root).Select(Path.GetFileName);
        Assert.That(created, Is.EquivalentTo(new[] { "backups", "data", "models", "logs" }));
    }

    [Test]
    [Category("Windows")]
    [SupportedOSPlatform("windows")]
    public void On_windows_the_vault_is_the_selected_one()
    {
        using var provider = new ServiceCollection().AddJarvisAdapters(folder.Paths).BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<ISecretVault>(), Is.TypeOf<SelectedSecretVault>());
            Assert.That(provider.GetRequiredService<ISecretVaultSelector>(), Is.SameAs(provider.GetRequiredService<ISecretVault>()));
            Assert.That(provider.GetRequiredService<ISecretVaultSelector>().Current, Is.EqualTo(VaultKind.CredentialManager));
        });
    }
}
