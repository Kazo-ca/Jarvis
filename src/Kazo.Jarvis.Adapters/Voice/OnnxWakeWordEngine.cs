using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.ML.OnnxRuntime;

namespace Kazo.Jarvis.Adapters.Voice;

/// <summary>
/// <see cref="IWakeWordEngine"/> on the three openWakeWord 0.5.1 models run by ONNX Runtime on the processor
/// (ADR 0006, section 2): mel spectrogram, speech embeddings, then the "hey_jarvis_v0.1" classifier. The files
/// are downloaded into <c>models\wake-word</c> (ADR 0004, section 5), never shipped.
/// </summary>
public sealed class OnnxWakeWordEngine : IWakeWordEngine
{
    /// <summary>Name of the folder under the models folder.</summary>
    public const string FolderName = "wake-word";

    /// <summary>The mel spectrogram model, shared by every openWakeWord classifier.</summary>
    public const string MelFile = "melspectrogram.onnx";

    /// <summary>The speech embedding model, shared by every openWakeWord classifier.</summary>
    public const string EmbeddingFile = "embedding_model.onnx";

    /// <summary>The "hey jarvis" classifier.</summary>
    public const string ClassifierFile = "hey_jarvis_v0.1.onnx";

    private readonly string folder;

    /// <summary>Creates the engine on the models folder of <paramref name="paths"/>.</summary>
    /// <param name="paths">Where Jarvis keeps its files.</param>
    public OnnxWakeWordEngine(JarvisPaths paths)
        : this(Path.Combine((paths ?? throw new ArgumentNullException(nameof(paths))).Models, FolderName))
    {
    }

    /// <summary>Creates the engine on a folder that holds the three files (tests).</summary>
    /// <param name="folder">The folder of the models.</param>
    public OnnxWakeWordEngine(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        this.folder = folder;
    }

    /// <summary>The folder where the three files are expected.</summary>
    public string Folder => folder;

    /// <inheritdoc />
    public IWakeWordDetector Load()
    {
        var files = new[] { MelFile, EmbeddingFile, ClassifierFile }.Select(name => Path.Combine(folder, name)).ToArray();
        var missing = files.Where(file => !File.Exists(file)).ToArray();
        if (missing.Length > 0)
        {
            throw new WakeWordModelMissingException(missing);
        }

        // One thread each: the three models are small and run every 80 ms; more threads only add overhead
        // (1.3 to 2 % of one core measured in spike E4).
        using var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        InferenceSession? mel = null, embedding = null, classifier = null;
        try
        {
            mel = new InferenceSession(files[0], options);
            embedding = new InferenceSession(files[1], options);
            classifier = new InferenceSession(files[2], options);
            return new OnnxWakeWordDetector(mel, embedding, classifier);
        }
        catch
        {
            mel?.Dispose();
            embedding?.Dispose();
            classifier?.Dispose();
            throw;
        }
    }
}
