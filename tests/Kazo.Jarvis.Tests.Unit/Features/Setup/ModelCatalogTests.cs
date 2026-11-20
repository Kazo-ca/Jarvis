using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.Models;
using Kazo.Jarvis.Adapters.Speech;
using Kazo.Jarvis.Adapters.Voice;
using Kazo.Jarvis.Features.Setup.Models;
using Kazo.Jarvis.Kernel.Models;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Tests.Unit.Features.Setup;

/// <summary>
/// The catalog of the models page (ADR 0006, section 7) puts every file where the adapters read it, from pinned
/// sources, with a checksum the installer understands.
/// </summary>
[TestFixture]
public sealed class ModelCatalogTests
{
    private static readonly JarvisPaths Paths = new(Path.Combine(Path.GetTempPath(), "catalog"));

    [Test]
    public void Ids_are_unique_and_every_whisper_model_has_one_line()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModelCatalog.All.Select(item => item.Id), Is.Unique);
            foreach (var model in Enum.GetValues<WhisperModel>())
            {
                Assert.That(ModelCatalog.All.Count(item => item.Whisper == model), Is.EqualTo(1), model.ToString());
            }
        });
    }

    [Test]
    public void Whisper_and_silero_go_where_the_transcription_reads_them()
    {
        Assert.Multiple(() =>
        {
            foreach (var model in Enum.GetValues<WhisperModel>())
            {
                Assert.That(Full(ModelCatalog.For(model).Downloads.Single()), Is.EqualTo(VoiceModelFiles.WhisperPath(Paths, model)));
            }

            Assert.That(Full(Item(ModelCatalog.SileroId).Downloads.Single()), Is.EqualTo(VoiceModelFiles.SileroPath(Paths)));
        });
    }

    [Test]
    public void The_wake_word_files_go_where_the_engine_reads_them()
    {
        var expected = new[] { OnnxWakeWordEngine.MelFile, OnnxWakeWordEngine.EmbeddingFile, OnnxWakeWordEngine.ClassifierFile }
            .Select(file => Path.Combine(Paths.Models, OnnxWakeWordEngine.FolderName, file));

        Assert.That(Item(ModelCatalog.WakeWordId).Downloads.Select(Full), Is.EquivalentTo(expected));
    }

    [Test]
    public void The_voices_unpack_to_the_folders_the_engines_read()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Item(ModelCatalog.KokoroId).Downloads.Single().Target, Is.EqualTo(KokoroSpeechEngine.FolderName));
            Assert.That(Item(ModelCatalog.PiperFrenchId).Downloads.Single().Target, Does.StartWith("vits-piper-fr_"));
            Assert.That(Item(ModelCatalog.PiperEnglishId).Downloads.Single().Target, Does.StartWith("vits-piper-en_"));
            Assert.That(ModelCatalog.All.Where(item => item.Group == ModelGroup.Voice).SelectMany(item => item.Downloads),
                Has.All.Matches<ModelDownload>(download => download.Format == ModelDownloadFormat.TarBz2));
        });
    }

    [Test]
    public void Every_download_is_https_sized_and_has_a_valid_checksum()
    {
        var downloads = ModelCatalog.All.SelectMany(item => item.Downloads).ToList();

        Assert.Multiple(() =>
        {
            foreach (var download in downloads)
            {
                Assert.That(download.Source.Scheme, Is.EqualTo(Uri.UriSchemeHttps), download.Target);
                Assert.That(download.Size, Is.GreaterThan(0), download.Target);
                Assert.DoesNotThrow(() => HttpModelInstaller.ParseChecksum(download.Checksum), download.Target);
            }

            Assert.That(downloads.Select(download => download.Target), Is.Unique);
        });
    }

    private static ModelItem Item(string id) => ModelCatalog.All.Single(item => item.Id == id);

    private static string Full(ModelDownload download) =>
        Path.Combine(Paths.Models, download.Target.Replace('/', Path.DirectorySeparatorChar));
}
