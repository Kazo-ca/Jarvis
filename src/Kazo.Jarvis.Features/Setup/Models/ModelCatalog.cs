using Kazo.Jarvis.Kernel.Models;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Features.Setup.Models;

/// <summary>What a model is for, which orders the models page.</summary>
public enum ModelGroup
{
    /// <summary>The three openWakeWord models (ADR 0006, section 2).</summary>
    WakeWord,

    /// <summary>Silero, the end of speech (ADR 0006, section 3).</summary>
    EndOfSpeech,

    /// <summary>A Whisper model (ADR 0006, section 4). One is enough.</summary>
    Recognition,

    /// <summary>A local voice (ADR 0006, section 5).</summary>
    Voice,
}

/// <summary>One line of the models page: a model and the files it takes.</summary>
/// <param name="Id">Stable identifier, also the key of its texts (<c>Model_&lt;Id&gt;</c>).</param>
/// <param name="Group">What it is for.</param>
/// <param name="Downloads">Its files. Installed when every one is in place.</param>
/// <param name="Whisper">The Whisper model, for the <see cref="ModelGroup.Recognition"/> group.</param>
/// <param name="Language">The language of a voice, <c>fr</c> or <c>en</c>; <see langword="null"/> for a multilingual one.</param>
public sealed record ModelItem(string Id, ModelGroup Group, IReadOnlyList<ModelDownload> Downloads, WhisperModel? Whisper = null, string? Language = null)
{
    /// <summary>Size of all its downloads, in bytes.</summary>
    public long Size => Downloads.Sum(download => download.Size);
}

/// <summary>
/// The models Jarvis downloads (ADR 0006, section 7), with their sources, sizes and checksums. Versions are pinned:
/// changing a line changes what every user gets, so a new checksum is taken from the publisher, never from a download
/// that just worked. The targets follow the layout the adapters read (<c>VoiceModelFiles</c>, <c>OnnxWakeWordEngine</c>,
/// <c>PiperSpeechEngine</c>, <c>KokoroSpeechEngine</c>), checked by the unit tests.
/// </summary>
public static class ModelCatalog
{
    private const string OpenWakeWord = "https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/";
    private const string SherpaTts = "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/";
    private const string SherpaAsr = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/";
    private const string WhisperCpp = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    /// <summary>Id of the wake word models.</summary>
    public const string WakeWordId = "wake-word";

    /// <summary>Id of Silero.</summary>
    public const string SileroId = "silero-vad";

    /// <summary>Id of the French Piper voice.</summary>
    public const string PiperFrenchId = "piper-fr";

    /// <summary>Id of the English Piper voice.</summary>
    public const string PiperEnglishId = "piper-en";

    /// <summary>Id of Kokoro.</summary>
    public const string KokoroId = "kokoro";

    /// <summary>Every model, in the order of the page.</summary>
    public static IReadOnlyList<ModelItem> All { get; } =
    [
        new(WakeWordId, ModelGroup.WakeWord,
        [
            File(OpenWakeWord + "melspectrogram.onnx", "wake-word/melspectrogram.onnx", 1_087_958, "sha256:ba2b0e0f8b7b875369a2c89cb13360ff53bac436f2895cced9f479fa65eb176f"),
            File(OpenWakeWord + "embedding_model.onnx", "wake-word/embedding_model.onnx", 1_326_578, "sha256:70d164290c1d095d1d4ee149bc5e00543250a7316b59f31d056cff7bd3075c1f"),
            File(OpenWakeWord + "hey_jarvis_v0.1.onnx", "wake-word/hey_jarvis_v0.1.onnx", 1_271_370, "sha256:94a13cfe60075b132f6a472e7e462e8123ee70861bc3fb58434a73712ee0d2cb"),
        ]),
        new(SileroId, ModelGroup.EndOfSpeech,
        [
            File(SherpaAsr + "silero_vad.onnx", "vad/silero_vad.onnx", 643_854, "sha256:9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6"),
        ]),

        // whisper.cpp publishes SHA-1 checksums only (models/README.md of ggml-org/whisper.cpp); sizes from Hugging Face.
        new("whisper-large-v3-turbo", ModelGroup.Recognition,
        [
            File(WhisperCpp + "ggml-large-v3-turbo-q5_0.bin", "whisper/ggml-large-v3-turbo-q5_0.bin", 574_041_195, "sha1:e050f7970618a659205450ad97eb95a18d69c9ee"),
        ], WhisperModel.LargeV3Turbo),
        new("whisper-small", ModelGroup.Recognition,
        [
            File(WhisperCpp + "ggml-small.bin", "whisper/ggml-small.bin", 487_601_967, "sha1:55356645c2b361a969dfd0ef2c5a50d530afd8d5"),
        ], WhisperModel.Small),
        new("whisper-base", ModelGroup.Recognition,
        [
            File(WhisperCpp + "ggml-base.bin", "whisper/ggml-base.bin", 147_951_465, "sha1:465707469ff3a37a2b9b8d8f89f2f99de7299dac"),
        ], WhisperModel.Base),

        new(PiperFrenchId, ModelGroup.Voice,
        [
            Archive(SherpaTts + "vits-piper-fr_FR-tom-medium.tar.bz2", "vits-piper-fr_FR-tom-medium", 67_202_955, "sha256:3d8258ef8466b5d5f2cc46c06e1415d3d01c9cbaeed8b6d5b3d152073672472c"),
        ], Language: "fr"),
        new(PiperEnglishId, ModelGroup.Voice,
        [
            Archive(SherpaTts + "vits-piper-en_GB-alan-medium.tar.bz2", "vits-piper-en_GB-alan-medium", 67_220_121, "sha256:a48d4017da0f77668b27bed63fe6e04dd64c6397e1fadad4f460efb0ef7c9012"),
        ], Language: "en"),
        new(KokoroId, ModelGroup.Voice,
        [
            Archive(SherpaTts + "kokoro-multi-lang-v1_0.tar.bz2", "kokoro-multi-lang-v1_0", 349_886_476, "sha256:60703707bc05ccd48d60e1eb723f38b07d6e474795a0b6a588d7220400e0f7a4"),
        ]),
    ];

    /// <summary>The line of a Whisper model.</summary>
    /// <param name="model">The model.</param>
    /// <returns>Its line.</returns>
    public static ModelItem For(WhisperModel model) => All.First(item => item.Whisper == model);

    private static ModelDownload File(string source, string target, long size, string checksum) =>
        new(new Uri(source), target, size, checksum);

    private static ModelDownload Archive(string source, string target, long size, string checksum) =>
        new(new Uri(source), target, size, checksum, ModelDownloadFormat.TarBz2);
}
