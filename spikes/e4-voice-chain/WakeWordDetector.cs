using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Spikes.VoiceChain;

/// <summary>
/// "hey jarvis" detection with the three openWakeWord ONNX models, ported from the Python
/// pipeline: audio (16 kHz) -> mel spectrogram -> 96-dim embeddings (76-frame windows, 8-frame hop)
/// -> classifier over the last 16 embeddings. Runs on CPU; no Python.
/// </summary>
public sealed class WakeWordDetector : IDisposable
{
    const int MelBins = 32, EmbeddingWindow = 76, EmbeddingHop = 8, ClassifierFrames = 16;
    readonly InferenceSession _mel, _embedding, _classifier;

    public WakeWordDetector(string modelDir, string classifierFile = "hey_jarvis_v0.1.onnx")
    {
        var opts = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        _mel = new InferenceSession(Path.Combine(modelDir, "melspectrogram.onnx"), opts);
        _embedding = new InferenceSession(Path.Combine(modelDir, "embedding_model.onnx"), opts);
        _classifier = new InferenceSession(Path.Combine(modelDir, classifierFile), opts);
    }

    /// <summary>Scores every 80 ms step of a 16 kHz clip; returns the score curve (0..1).</summary>
    public float[] Score(float[] samples16k)
    {
        var mel = Mel(samples16k);                 // [frames][32]
        var embeddings = new List<float[]>();
        for (var start = 0; start + EmbeddingWindow <= mel.Length; start += EmbeddingHop)
            embeddings.Add(Embed(mel, start));
        var scores = new List<float>();
        for (var end = ClassifierFrames; end <= embeddings.Count; end++)
            scores.Add(Classify(embeddings, end - ClassifierFrames));
        return scores.ToArray();
    }

    float[][] Mel(float[] samples)
    {
        // openWakeWord feeds int16-range values as float32.
        var input = new DenseTensor<float>(samples.Select(s => s * 32767f).ToArray(), [1, samples.Length]);
        using var r = _mel.Run([NamedOnnxValue.CreateFromTensor(_mel.InputMetadata.Keys.First(), input)]);
        var t = r.First().AsTensor<float>();      // [1,1,frames,32]
        var frames = t.Dimensions[2];
        var mel = new float[frames][];
        for (var f = 0; f < frames; f++)
        {
            mel[f] = new float[MelBins];
            for (var b = 0; b < MelBins; b++) mel[f][b] = t[0, 0, f, b] / 10f + 2f; // same transform as openWakeWord
        }
        return mel;
    }

    float[] Embed(float[][] mel, int start)
    {
        var buf = new float[EmbeddingWindow * MelBins];
        for (var f = 0; f < EmbeddingWindow; f++) Array.Copy(mel[start + f], 0, buf, f * MelBins, MelBins);
        var input = new DenseTensor<float>(buf, [1, EmbeddingWindow, MelBins, 1]);
        using var r = _embedding.Run([NamedOnnxValue.CreateFromTensor(_embedding.InputMetadata.Keys.First(), input)]);
        return r.First().AsTensor<float>().ToArray();
    }

    float Classify(List<float[]> embeddings, int start)
    {
        var buf = new float[ClassifierFrames * 96];
        for (var i = 0; i < ClassifierFrames; i++) Array.Copy(embeddings[start + i], 0, buf, i * 96, 96);
        var input = new DenseTensor<float>(buf, [1, ClassifierFrames, 96]);
        using var r = _classifier.Run([NamedOnnxValue.CreateFromTensor(_classifier.InputMetadata.Keys.First(), input)]);
        return r.First().AsTensor<float>().ToArray()[0];
    }

    public void Dispose() { _mel.Dispose(); _embedding.Dispose(); _classifier.Dispose(); }
}
