using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Maintenance.Updates;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates.FakeReleaseSource;

namespace Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates;

[TestFixture]
public sealed class UpdateServiceTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private ISettingsStore settings = null!;
    private FakeReleaseSource releases = null!;
    private FakeUpdateLauncher launcher = null!;
    private ListLogger log = null!;
    private List<UpdateFound> found = null!;
    private UpdateService service = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        releases = new FakeReleaseSource();
        launcher = new FakeUpdateLauncher();
        log = new ListLogger();
        found = [];
        var bus = provider.GetRequiredService<IMessageBus>();
        bus.Subscribe<UpdateFound>((message, _) =>
        {
            found.Add(message);
            return ValueTask.CompletedTask;
        });
        service = new UpdateService(releases, launcher, settings, bus, new Logger<UpdateService>(log));
    }

    [TearDown]
    public void TearDown()
    {
        log.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public void Reads_the_running_version_without_build_metadata() =>
        Assert.That(service.CurrentVersion?.ToString(), Is.EqualTo("0.1.0"));

    [Test]
    public async Task Up_to_date_when_nothing_newer_is_published()
    {
        releases.Releases.Add(Release("v0.1.0"));

        var status = await service.CheckAsync();

        Assert.That(status.Phase, Is.EqualTo(UpdatePhase.UpToDate));
    }

    [Test]
    public async Task A_newer_version_is_available_and_the_manual_check_announces_nothing()
    {
        releases.Releases.Add(Release("v0.2.0"));
        var changes = new List<UpdatePhase>();
        service.StatusChanged += status => changes.Add(status.Phase);

        var status = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Available));
            Assert.That(status.Update?.Version.ToString(), Is.EqualTo("0.2.0"));
            Assert.That(changes, Is.EqualTo(new[] { UpdatePhase.Checking, UpdatePhase.Available }));
            Assert.That(found, Is.Empty);
        });
    }

    [Test]
    public async Task The_automatic_check_announces_each_version_once()
    {
        releases.Releases.Add(Release("v0.2.0"));

        await service.CheckAsync(automatic: true);
        await service.CheckAsync(automatic: true);
        releases.Releases.Add(Release("v0.3.0"));
        await service.CheckAsync(automatic: true);

        Assert.That(found.Select(f => f.Version), Is.EqualTo(new[] { "0.2.0", "0.3.0" }));
    }

    [Test]
    public async Task Pre_releases_follow_the_setting()
    {
        releases.Releases.Add(Release("v0.2.0-beta.1", preRelease: true));
        await settings.WriteAsync(UpdateOptions.Section, new UpdateOptions { IncludePreReleases = false });

        Assert.That((await service.CheckAsync()).Phase, Is.EqualTo(UpdatePhase.UpToDate));
    }

    [Test]
    public async Task A_failed_manual_check_is_shown_and_logged()
    {
        releases.CheckFailure = new HttpRequestException("offline");

        var status = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Failed));
            Assert.That(status.Failure, Is.EqualTo(UpdateFailure.Unreachable));
            Assert.That(log.Entries.Any(e => e.Level == LogLevel.Warning && e.Exception is HttpRequestException), Is.True);
        });
    }

    [Test]
    public async Task A_failed_automatic_check_keeps_the_previous_status()
    {
        releases.Releases.Add(Release("v0.2.0"));
        await service.CheckAsync();
        releases.CheckFailure = new HttpRequestException("rate limited");

        var status = await service.CheckAsync(automatic: true);

        Assert.Multiple(() =>
        {
            Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Available));
            Assert.That(service.Status.Update?.Version.ToString(), Is.EqualTo("0.2.0"));
        });
    }

    [Test]
    public async Task A_second_check_while_one_runs_does_nothing()
    {
        releases.HoldCheck = new TaskCompletionSource();
        var first = service.CheckAsync();

        var second = await service.CheckAsync();
        releases.HoldCheck.SetResult();
        await first;

        Assert.Multiple(() =>
        {
            Assert.That(second.Phase, Is.EqualTo(UpdatePhase.Checking));
            Assert.That(releases.Checks, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Install_downloads_then_starts_the_updater()
    {
        releases.Releases.Add(Release("v0.2.0"));
        await service.CheckAsync();
        var progress = new List<double?>();
        service.StatusChanged += status => progress.Add(status.Phase == UpdatePhase.Downloading ? status.Progress : null);

        var status = await service.InstallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Installing));
            Assert.That(launcher.Launched, Is.EqualTo(new[] { "prepared/Kazo.Jarvis-0.2.0-win-x64.zip" }));
            Assert.That(progress.Where(p => p is not null), Is.EqualTo(new double?[] { 0, 0.5, 1 }));
        });
    }

    [Test]
    public async Task Install_without_an_available_version_does_nothing()
    {
        var status = await service.InstallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Idle));
            Assert.That(releases.Prepared, Is.Empty);
        });
    }

    [Test]
    public async Task A_development_build_never_downloads()
    {
        releases.Releases.Add(Release("v0.2.0"));
        await service.CheckAsync();
        launcher.CanInstall = false;

        var status = await service.InstallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.Failure, Is.EqualTo(UpdateFailure.NotInstallable));
            Assert.That(releases.Prepared, Is.Empty);
            Assert.That(launcher.Launched, Is.Empty);
        });
    }

    [TestCase(typeof(InvalidDataException), UpdateFailure.Corrupted)]
    [TestCase(typeof(HttpRequestException), UpdateFailure.Unreachable)]
    [TestCase(typeof(IOException), UpdateFailure.Unreachable)]
    public async Task A_failed_download_is_named_and_the_update_stays_available(Type exception, UpdateFailure expected)
    {
        releases.Releases.Add(Release("v0.2.0"));
        await service.CheckAsync();
        releases.PrepareFailure = (Exception)Activator.CreateInstance(exception, "failed")!;

        var status = await service.InstallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Failed));
            Assert.That(status.Failure, Is.EqualTo(expected));
            Assert.That(status.Update?.Version.ToString(), Is.EqualTo("0.2.0"));
            Assert.That(launcher.Launched, Is.Empty);
        });
    }

    [Test]
    public async Task An_updater_that_does_not_start_is_reported()
    {
        releases.Releases.Add(Release("v0.2.0"));
        await service.CheckAsync();
        launcher.Succeeds = false;

        Assert.That((await service.InstallAsync()).Failure, Is.EqualTo(UpdateFailure.LaunchFailed));
    }

    [Test]
    public async Task A_faulty_listener_does_not_break_the_check()
    {
        releases.Releases.Add(Release("v0.2.0"));
        service.StatusChanged += _ => throw new InvalidOperationException("window gone");

        var status = await service.CheckAsync();

        Assert.That(status.Phase, Is.EqualTo(UpdatePhase.Available));
    }
}
