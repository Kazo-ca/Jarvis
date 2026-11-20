using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Adapters.Voice;

/// <summary>
/// Where the transcription models sit in the models folder (ADR 0004 and 0006, section 7). The first-run wizard
/// (work item stage1/14-first-run) downloads them there; until then they are copied by hand.
/// </summary>
public static class VoiceModelFiles
{
    /// <summary>The Whisper file of a model, as <c>ggerganov/whisper.cpp</c> names it on Hugging Face.</summary>
    /// <param name="model">The model.</param>
    /// <returns>The file name.</returns>
    public static string FileNameOf(WhisperModel model) => model switch
    {
        WhisperModel.LargeV3Turbo => "ggml-large-v3-turbo-q5_0.bin",
        WhisperModel.Small => "ggml-small.bin",
        WhisperModel.Base => "ggml-base.bin",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown Whisper model."),
    };

    /// <summary>The full path of a Whisper model: <c>models\whisper\&lt;file&gt;</c>.</summary>
    /// <param name="paths">The folders of Jarvis.</param>
    /// <param name="model">The model.</param>
    /// <returns>The path.</returns>
    public static string WhisperPath(JarvisPaths paths, WhisperModel model)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.Models, "whisper", FileNameOf(model));
    }

    /// <summary>The Silero model of sherpa-onnx: <c>models\vad\silero_vad.onnx</c>.</summary>
    /// <param name="paths">The folders of Jarvis.</param>
    /// <returns>The path.</returns>
    public static string SileroPath(JarvisPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.Models, "vad", "silero_vad.onnx");
    }
}
