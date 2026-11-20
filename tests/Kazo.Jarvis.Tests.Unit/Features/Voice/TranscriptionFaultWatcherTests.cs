using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Transcription;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>A Whisper model loaded clears the "Whisper missing" fault (US1.17), and only that one.</summary>
[TestFixture]
public sealed class TranscriptionFaultWatcherTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private FakeSpeechRecognition recognition = null!;
    private RecognizerKeeper keeper = null!;
    private TranscriptionFaultWatcher watcher = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        recognition = new FakeSpeechRecognition { Missing = true };
        keeper = new RecognizerKeeper(recognition, new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System), NullLogger<RecognizerKeeper>.Instance);
        watcher = new TranscriptionFaultWatcher(keeper, Bus, State);
        await watcher.StartAsync(CancellationToken.None);
        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await watcher.StopAsync(CancellationToken.None);
        keeper.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    private IMessageBus Bus => provider.GetRequiredService<IMessageBus>();

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    [Test]
    public async Task Whisper_found_again_clears_its_fault()
    {
        await Bus.PublishAsync<AssistantInput>(new FaultRaised(new AssistantFault(FaultCause.ModelMissing, "ggml-large-v3-turbo.bin", FaultOrigin.Transcription)));
        recognition.Missing = false;

        // The new attempt made before a transcription.
        await keeper.TranscribeAsync(new float[16_000], AssistantLanguage.French, CancellationToken.None);

        await Eventually(() => State.Current.Fault is null);
    }

    [Test]
    public async Task Whisper_loaded_leaves_the_fault_of_a_missing_voice()
    {
        var voice = new AssistantFault(FaultCause.ModelMissing, "fr_FR-tom-medium.onnx", FaultOrigin.Voice);
        await Bus.PublishAsync<AssistantInput>(new FaultRaised(voice));
        recognition.Missing = false;

        await keeper.TranscribeAsync(new float[16_000], AssistantLanguage.French, CancellationToken.None);
        await Task.Delay(50);

        Assert.That(State.Current.Fault, Is.EqualTo(voice));
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }

        Assert.That(condition(), Is.True);
    }
}
