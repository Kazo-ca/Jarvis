using Kazo.Jarvis.Features.Setup.Models;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Models;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Setup;

/// <summary>The downloads of the models page (work item 14): missing models only, one at a time, the Whisper choice published.</summary>
[TestFixture]
public sealed class ModelsKeeperTests
{
    private static readonly GraphicsCard Rtx3060 = new("NVIDIA GeForce RTX 3060", 12_288);

    private ServiceProvider provider = null!;
    private FakeModelInstaller installer = null!;
    private List<WhisperModelChosen> chosen = null!;
    private IDisposable subscription = null!;

    [SetUp]
    public void SetUp()
    {
        provider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddJarvisKernel()
            .BuildServiceProvider();
        installer = new FakeModelInstaller();
        chosen = [];
        subscription = provider.GetRequiredService<IMessageBus>().Subscribe<WhisperModelChosen>((message, _) =>
        {
            chosen.Add(message);
            return ValueTask.CompletedTask;
        });
    }

    [TearDown]
    public void TearDown()
    {
        subscription.Dispose();
        provider.Dispose();
    }

    [Test]
    public async Task The_proposed_models_follow_the_card()
    {
        using var keeper = Keeper(Rtx3060);
        var advice = await keeper.GetAdviceAsync();

        Assert.That(ModelsKeeper.Proposed(advice), Is.EquivalentTo(new[]
        {
            ModelCatalog.WakeWordId, ModelCatalog.SileroId, "whisper-large-v3-turbo", ModelCatalog.PiperFrenchId, ModelCatalog.PiperEnglishId,
        }));
    }

    [Test]
    public async Task Only_missing_files_are_downloaded_then_whisper_is_chosen_on_the_card()
    {
        using var keeper = Keeper(Rtx3060);
        var wakeWord = Item(ModelCatalog.WakeWordId);
        installer.Installed.Add(wakeWord.Downloads[0].Target);

        var done = await keeper.InstallAsync([wakeWord, ModelCatalog.For(WhisperModel.LargeV3Turbo)], WhisperModel.LargeV3Turbo);

        Assert.Multiple(() =>
        {
            Assert.That(done, Is.True);
            Assert.That(installer.Downloaded, Has.Count.EqualTo(3));
            Assert.That(installer.Downloaded, Does.Not.Contain(wakeWord.Downloads[0].Target));
            Assert.That(keeper.StatusOf(wakeWord).State, Is.EqualTo(ModelState.Installed));
            Assert.That(chosen, Is.EqualTo(new[] { new WhisperModelChosen(WhisperModel.LargeV3Turbo, ComputeDevice.Gpu) }));
            Assert.That(keeper.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task Without_an_nvidia_card_whisper_runs_on_the_processor()
    {
        using var keeper = Keeper(null);

        await keeper.InstallAsync([ModelCatalog.For(WhisperModel.Base)], WhisperModel.Base);

        Assert.That(chosen, Is.EqualTo(new[] { new WhisperModelChosen(WhisperModel.Base, ComputeDevice.Cpu) }));
    }

    [Test]
    public async Task A_failure_is_shown_on_its_line_and_the_others_go_on()
    {
        using var keeper = Keeper(Rtx3060);
        var silero = Item(ModelCatalog.SileroId);
        var french = Item(ModelCatalog.PiperFrenchId);
        installer.Failures[silero.Downloads[0].Target] = ModelDownloadFailure.ChecksumMismatch;
        var whisper = ModelCatalog.For(WhisperModel.Small);

        var done = await keeper.InstallAsync([silero, french, whisper], WhisperModel.Small);

        Assert.Multiple(() =>
        {
            Assert.That(done, Is.False);
            Assert.That(keeper.StatusOf(silero), Is.EqualTo(new ModelStatus(ModelState.Failed, Failure: ModelDownloadFailure.ChecksumMismatch)));
            Assert.That(keeper.StatusOf(french).State, Is.EqualTo(ModelState.Installed));
            Assert.That(chosen.Select(message => message.Model), Is.EqualTo(new[] { WhisperModel.Small }));
        });
    }

    [Test]
    public async Task A_whisper_model_not_installed_is_not_chosen()
    {
        using var keeper = Keeper(Rtx3060);
        var whisper = ModelCatalog.For(WhisperModel.LargeV3Turbo);
        installer.Failures[whisper.Downloads[0].Target] = ModelDownloadFailure.Unreachable;

        await keeper.InstallAsync([whisper], WhisperModel.LargeV3Turbo);

        Assert.That(chosen, Is.Empty);
    }

    [Test]
    public async Task One_batch_at_a_time_and_cancel_stops_it()
    {
        using var keeper = Keeper(Rtx3060);
        installer.Hold = new TaskCompletionSource();
        var french = Item(ModelCatalog.PiperFrenchId);
        var english = Item(ModelCatalog.PiperEnglishId);

        var first = keeper.InstallAsync([french, english], null);
        Assert.Multiple(() =>
        {
            Assert.That(keeper.IsBusy, Is.True);
            Assert.That(keeper.StatusOf(french).State, Is.EqualTo(ModelState.Downloading));
            Assert.That(keeper.StatusOf(english).State, Is.EqualTo(ModelState.Queued));
        });

        Assert.That(await keeper.InstallAsync([english], null), Is.False);
        keeper.Cancel();

        Assert.Multiple(async () =>
        {
            Assert.That(await first, Is.False);
            Assert.That(keeper.IsBusy, Is.False);
            Assert.That(keeper.StatusOf(french).State, Is.EqualTo(ModelState.Missing));
            Assert.That(keeper.StatusOf(english).State, Is.EqualTo(ModelState.Missing));
            Assert.That(installer.Downloaded, Has.Count.EqualTo(1));
        });
    }

    private ModelsKeeper Keeper(GraphicsCard? card) => new(
        installer,
        new FakeGraphicsCardProbe(card),
        provider.GetRequiredService<IMessageBus>(),
        NullLogger<ModelsKeeper>.Instance);

    private static ModelItem Item(string id) => ModelCatalog.All.Single(item => item.Id == id);
}
