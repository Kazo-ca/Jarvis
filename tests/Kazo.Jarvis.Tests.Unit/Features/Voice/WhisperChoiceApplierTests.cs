using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Transcription;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Voice;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>The Whisper model installed from the models page becomes the one in use (work item 14).</summary>
[TestFixture]
public sealed class WhisperChoiceApplierTests
{
    [Test]
    public async Task The_choice_is_saved_and_the_other_settings_stay()
    {
        using var folder = new TemporaryFolder();
        var settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        await settings.WriteAsync(TranscriptionOptions.Section, new TranscriptionOptions { SilenceMilliseconds = 700 });
        var applier = new WhisperChoiceApplier(settings, NullLogger<WhisperChoiceApplier>.Instance);

        await applier.HandleAsync(new WhisperModelChosen(WhisperModel.Base, ComputeDevice.Cpu), CancellationToken.None);

        var saved = settings.Read(TranscriptionOptions.Section);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Model, Is.EqualTo(WhisperModel.Base));
            Assert.That(saved.Device, Is.EqualTo(ComputeDevice.Cpu));
            Assert.That(saved.SilenceMilliseconds, Is.EqualTo(700));
        });
    }
}
