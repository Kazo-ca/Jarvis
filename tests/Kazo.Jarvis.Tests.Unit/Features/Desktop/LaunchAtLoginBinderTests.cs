using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Desktop.LaunchAtLogin;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Features.Desktop;

/// <summary>Start with Windows, on by default (design assumption no. 3).</summary>
[TestFixture]
public sealed class LaunchAtLoginBinderTests
{
    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private FakeLaunchAtLogin launch = null!;
    private ListLogger log = null!;
    private LaunchAtLoginBinder binder = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        log = new ListLogger();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        launch = new FakeLaunchAtLogin();
        binder = new LaunchAtLoginBinder(launch, settings, new Logger<LaunchAtLoginBinder>(log));
    }

    [TearDown]
    public void TearDown()
    {
        binder.Dispose();
        log.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task It_is_registered_at_the_first_start()
    {
        await binder.StartAsync(CancellationToken.None);

        Assert.That(launch.Requests, Is.EqualTo(new[] { true }));
    }

    [Test]
    public async Task Turning_the_setting_off_removes_it_at_once()
    {
        await binder.StartAsync(CancellationToken.None);

        await settings.WriteAsync(LaunchAtLoginOptions.Section, new LaunchAtLoginOptions { Enabled = false });

        Assert.Multiple(() =>
        {
            Assert.That(launch.Requests, Is.EqualTo(new[] { true, false }));
            Assert.That(launch.IsEnabled, Is.False);
        });
    }

    [Test]
    public async Task A_refused_change_is_logged_and_does_not_stop_the_start()
    {
        launch.Throws = true;

        Assert.That(async () => await binder.StartAsync(CancellationToken.None), Throws.Nothing);
        Assert.That(log.Entries.Select(entry => entry.Level), Has.Some.EqualTo(LogLevel.Error));
    }
}
