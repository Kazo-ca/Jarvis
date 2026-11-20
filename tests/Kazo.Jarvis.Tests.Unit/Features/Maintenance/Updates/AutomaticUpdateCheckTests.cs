using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Maintenance.Updates;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates;

[TestFixture]
public sealed class AutomaticUpdateCheckTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private ISettingsStore settings = null!;
    private FakeReleaseSource releases = null!;
    private FakeTimeProvider time = null!;
    private AutomaticUpdateCheck check = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        releases = new FakeReleaseSource();
        time = new FakeTimeProvider();
        var service = new UpdateService(releases, new FakeUpdateLauncher(), settings, provider.GetRequiredService<IMessageBus>(), NullLogger<UpdateService>.Instance);
        check = new AutomaticUpdateCheck(service, settings, time);
    }

    [TearDown]
    public async Task TearDown()
    {
        await check.StopAsync(CancellationToken.None);
        check.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task Off_by_default_nothing_leaves_the_pc()
    {
        Assert.That(settings.Read(UpdateOptions.Section).Automatic, Is.False);
        await check.StartAsync(CancellationToken.None);
        await Settle();

        await Advance(AutomaticUpdateCheck.FirstDelay + (AutomaticUpdateCheck.Interval * 3));

        Assert.That(releases.Checks, Is.Zero);
    }

    [Test]
    public async Task When_on_checks_a_minute_after_start_then_every_six_hours()
    {
        await settings.WriteAsync(UpdateOptions.Section, new UpdateOptions { Automatic = true });
        await check.StartAsync(CancellationToken.None);
        await Settle();

        await Advance(AutomaticUpdateCheck.FirstDelay - TimeSpan.FromSeconds(1));
        var beforeFirst = releases.Checks;
        await Advance(TimeSpan.FromSeconds(1));
        var afterFirst = releases.Checks;
        await Advance(AutomaticUpdateCheck.Interval);

        Assert.Multiple(() =>
        {
            Assert.That(beforeFirst, Is.Zero);
            Assert.That(afterFirst, Is.EqualTo(1));
            Assert.That(releases.Checks, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Turning_it_on_needs_no_restart()
    {
        await check.StartAsync(CancellationToken.None);
        await Settle();
        await Advance(AutomaticUpdateCheck.FirstDelay);
        await settings.WriteAsync(UpdateOptions.Section, new UpdateOptions { Automatic = true });

        await Advance(AutomaticUpdateCheck.Interval);

        Assert.That(releases.Checks, Is.EqualTo(1));
    }

    /// <summary>Moves the fake clock, then lets the background loop run its continuations.</summary>
    private async Task Advance(TimeSpan by)
    {
        time.Advance(by);
        await Settle();
    }

    /// <summary>
    /// Lets the background loop reach its next wait: since .NET 10, ExecuteAsync starts on a pool thread,
    /// so its first timer may not exist yet when StartAsync returns.
    /// </summary>
    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10);
        }
    }
}
