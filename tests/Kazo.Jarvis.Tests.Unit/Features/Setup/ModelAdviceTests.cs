using Kazo.Jarvis.Features.Setup.Models;
using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Tests.Unit.Features.Setup;

/// <summary>The fallbacks proposed when the graphics memory runs short or there is no NVIDIA card (work item 14).</summary>
[TestFixture]
public sealed class ModelAdviceTests
{
    [Test]
    public void A_16_gb_card_gets_the_default_chain()
    {
        var advice = ModelAdvice.For(new GraphicsCard("NVIDIA GeForce RTX 4060 Ti", 16_380));

        Assert.That(advice, Is.EqualTo(advice with { Whisper = WhisperModel.LargeV3Turbo, Device = ComputeDevice.Gpu, LanguageModel = LanguageModelAdvice.Default }));
    }

    [Test]
    public void A_12_gb_card_keeps_whisper_turbo_with_a_smaller_language_model()
    {
        // The acceptance of stage 2: qwen3:14b next to Whisper turbo did not fit the 12 GB card once the tools were in the prompt.
        var advice = ModelAdvice.For(new GraphicsCard("NVIDIA GeForce RTX 3060", 12_288));

        Assert.That(advice, Is.EqualTo(advice with { Whisper = WhisperModel.LargeV3Turbo, Device = ComputeDevice.Gpu, LanguageModel = LanguageModelAdvice.Smaller }));
    }

    [Test]
    public void A_smaller_card_gets_whisper_small_and_a_smaller_language_model()
    {
        var advice = ModelAdvice.For(new GraphicsCard("NVIDIA GeForce RTX 3050", 8_192));

        Assert.Multiple(() =>
        {
            Assert.That(advice.Whisper, Is.EqualTo(WhisperModel.Small));
            Assert.That(advice.Device, Is.EqualTo(ComputeDevice.Gpu));
            Assert.That(advice.LanguageModel, Is.EqualTo(LanguageModelAdvice.Smaller));
        });
    }

    [Test]
    public void No_nvidia_card_gets_whisper_base_on_the_processor()
    {
        var advice = ModelAdvice.For(null);

        Assert.Multiple(() =>
        {
            Assert.That(advice.Whisper, Is.EqualTo(WhisperModel.Base));
            Assert.That(advice.Device, Is.EqualTo(ComputeDevice.Cpu));
            Assert.That(advice.LanguageModel, Is.EqualTo(LanguageModelAdvice.Smaller));
        });
    }
}
