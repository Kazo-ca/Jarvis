using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Setup.FirstRun;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Setup;

/// <summary>When the first-run wizard opens (US1.4): no settings file, a wizard left unfinished, or on request.</summary>
[TestFixture]
public sealed class FirstRunLauncherTests
{
    private TemporaryFolder folder = null!;
    private RecordingDialogs dialogs = null!;
    private FirstRunWizardState state = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        dialogs = new RecordingDialogs();
        state = new FirstRunWizardState();
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task Without_a_settings_file_the_wizard_opens_and_stays_pending()
    {
        var settings = Store();
        Assert.That(settings.State.IsFirstRun, Is.True);

        await Launcher(settings).StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.Opened.Select(request => request.Key), Is.EqualTo(new[] { FirstRunLauncher.DialogKey }));
            Assert.That(dialogs.Opened[0].Component, Is.EqualTo(typeof(FirstRunWizard)));
            Assert.That(File.Exists(folder.Paths.ConfigFile), Is.True);
            Assert.That(Store().Read(FirstRunOptions.Section).Pending, Is.True);
        });
    }

    [Test]
    public async Task A_wizard_left_unfinished_opens_again_at_the_next_start()
    {
        await Store().WriteAsync(FirstRunOptions.Section, new FirstRunOptions { Pending = true });
        var settings = Store();

        await Launcher(settings).StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(settings.State.IsFirstRun, Is.False);
            Assert.That(dialogs.Opened, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task An_existing_profile_or_a_finished_wizard_does_not_open_it()
    {
        await Store().WriteAsync(FirstRunOptions.Section, new FirstRunOptions { Pending = false });

        await Launcher(Store()).StartAsync(CancellationToken.None);

        Assert.That(dialogs.Opened, Is.Empty);
    }

    [Test]
    public async Task The_tray_entry_opens_it_from_the_first_step_without_making_it_pending()
    {
        await Store().WriteAsync(FirstRunOptions.Section, new FirstRunOptions());
        var settings = Store();
        state.Step = 3;

        await Launcher(settings).HandleAsync(new FirstRunWizardRequested(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.Opened, Has.Count.EqualTo(1));
            Assert.That(state.Step, Is.Zero);
            Assert.That(settings.Read(FirstRunOptions.Section).Pending, Is.False);
        });
    }

    [Test]
    public async Task Asking_again_while_it_is_open_keeps_the_step()
    {
        var launcher = Launcher(Store());
        await launcher.HandleAsync(new FirstRunWizardRequested(), CancellationToken.None);
        state.Step = 2;

        await launcher.HandleAsync(new FirstRunWizardRequested(), CancellationToken.None);

        Assert.That(state.Step, Is.EqualTo(2));
    }

    private JsonFileSettingsStore Store() => new(folder.Paths, [], TimeProvider.System);

    private FirstRunLauncher Launcher(ISettingsStore settings) =>
        new(settings, dialogs, state, new KeyLocalizer<FirstRunWizard>(), NullLogger<FirstRunLauncher>.Instance);
}
