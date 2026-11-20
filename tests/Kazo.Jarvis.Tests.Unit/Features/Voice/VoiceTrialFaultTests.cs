using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Features.Voice.TestVoice;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>A voice heard on the voices page clears the voice fault (US1.17), where the faults window sends the user.</summary>
[TestFixture]
public sealed class VoiceTrialFaultTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private FakeSpeechEngine piper = null!;
    private VoiceSelection selection = null!;
    private VoiceTrial trial = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        piper = new FakeSpeechEngine("piper");
        piper.Add("fr_FR-tom-medium", "fr");
        selection = new VoiceSelection([piper], new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System));
        trial = new VoiceTrial(selection, [piper], new FakePlayback(), new ManualLanguage { Current = AssistantLanguage.French }, Bus, State);
        await trial.LoadAsync();
    }

    [TearDown]
    public void TearDown()
    {
        trial.Dispose();
        selection.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    private IMessageBus Bus => provider.GetRequiredService<IMessageBus>();

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    [TestCase(FaultCause.VoiceMissing)]
    [TestCase(FaultCause.ModelMissing)]
    public async Task A_voice_heard_clears_the_voice_fault(FaultCause cause)
    {
        await Bus.PublishAsync<AssistantInput>(new FaultRaised(new AssistantFault(cause, "fake", FaultOrigin.Voice)));

        await trial.ListenAsync("Bonsoir.");

        Assert.That(State.Current.Fault, Is.Null);
    }

    [Test]
    public async Task A_voice_that_fails_again_keeps_the_fault()
    {
        var fault = new AssistantFault(FaultCause.ModelMissing, "fake", FaultOrigin.Voice);
        await Bus.PublishAsync<AssistantInput>(new FaultRaised(fault));
        piper.FailOn = "Bonsoir";

        await trial.ListenAsync("Bonsoir.");

        Assert.That(State.Current.Fault, Is.EqualTo(fault));
    }

    [Test]
    public async Task A_voice_heard_leaves_the_faults_of_other_parts()
    {
        var whisper = new AssistantFault(FaultCause.ModelMissing, "ggml-large-v3-turbo.bin", FaultOrigin.Transcription);
        await Bus.PublishAsync<AssistantInput>(new FaultRaised(whisper));

        await trial.ListenAsync("Bonsoir.");

        Assert.That(State.Current.Fault, Is.EqualTo(whisper));
    }
}
