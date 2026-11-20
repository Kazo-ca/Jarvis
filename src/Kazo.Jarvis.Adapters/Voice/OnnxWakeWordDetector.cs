using Kazo.Jarvis.Kernel.Voice;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Kazo.Jarvis.Adapters.Voice;

/// <summary>
/// Streaming port of the openWakeWord pipeline (behavior of <c>AudioFeatures</c> and <c>Model.predict</c>
/// in openWakeWord 0.5.1, not its code): every 80 ms frame adds mel frames, one 96-value embedding of the
/// last 76 mel frames, then the classifier scores the last 16 embeddings (about 1.3 s of speech).
/// </summary>
internal sealed class OnnxWakeWordDetector : IWakeWordDetector
{
    /// <summary>Samples per frame: 80 ms at 16 kHz.</summary>
    internal const int Frame = 1_280;

    private const int MelBins = 32;
    private const int EmbeddingWindow = 76;
    private const int EmbeddingStride = 8;
    private const int EmbeddingSize = 96;
    private const int ClassifierFrames = 16;

    // The mel model needs three hops (3 × 160 samples) before the frame to produce its frames at the edge.
    private const int MelContext = 480;

    // openWakeWord returns 0 for the first five predictions: the buffers still hold the start-up filler.
    private const int WarmUpFrames = 5;

    private readonly InferenceSession mel;
    private readonly InferenceSession embedding;
    private readonly InferenceSession classifier;
    private readonly string melInput;
    private readonly string embeddingInput;
    private readonly string classifierInput;
    private readonly float[] raw = new float[Frame + MelContext];
    private readonly float[] melFrames = new float[EmbeddingWindow * MelBins];
    private readonly float[] features = new float[ClassifierFrames * EmbeddingSize];
    private readonly float[] startFeatures;
    private int processed;

    public OnnxWakeWordDetector(InferenceSession mel, InferenceSession embedding, InferenceSession classifier)
    {
        this.mel = mel;
        this.embedding = embedding;
        this.classifier = classifier;
        melInput = mel.InputMetadata.Keys.First();
        embeddingInput = embedding.InputMetadata.Keys.First();
        classifierInput = classifier.InputMetadata.Keys.First();
        startFeatures = NoiseFeatures();
        Reset();
    }

    /// <inheritdoc />
    public int FrameLength => Frame;

    /// <inheritdoc />
    public float Process(ReadOnlySpan<float> frame)
    {
        if (frame.Length != Frame)
        {
            throw new ArgumentException($"A frame holds exactly {Frame} samples.", nameof(frame));
        }

        // Keep the context, then add the frame. openWakeWord works on int16 values carried as float.
        raw.AsSpan(Frame).CopyTo(raw);
        var target = raw.AsSpan(MelContext);
        for (var i = 0; i < Frame; i++)
        {
            target[i] = Math.Clamp(frame[i], -1f, 1f) * 32_767f;
        }

        Append(melFrames, Mel(raw));
        Append(features, Embed(melFrames));
        var score = Classify(features);
        processed++;
        return processed <= WarmUpFrames ? 0f : Math.Clamp(score, 0f, 1f);
    }

    /// <inheritdoc />
    public void Reset()
    {
        // Same filler as openWakeWord: mel frames of ones and the embeddings of a few seconds of quiet noise.
        Array.Clear(raw);
        Array.Fill(melFrames, 1f);
        startFeatures.CopyTo(features, 0);
        processed = 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        mel.Dispose();
        embedding.Dispose();
        classifier.Dispose();
    }

    /// <summary>Shifts <paramref name="buffer"/> left and puts <paramref name="values"/> at its end.</summary>
    private static void Append(float[] buffer, float[] values)
    {
        var kept = Math.Max(0, buffer.Length - values.Length);
        buffer.AsSpan(buffer.Length - kept).CopyTo(buffer);
        values.AsSpan(Math.Max(0, values.Length - buffer.Length)).CopyTo(buffer.AsSpan(kept));
    }

    /// <summary>The mel frames of int16-scaled samples, flattened (frames × 32).</summary>
    private float[] Mel(float[] samples)
    {
        var input = new DenseTensor<float>(samples, [1, samples.Length]);
        using var results = mel.Run([NamedOnnxValue.CreateFromTensor(melInput, input)]);
        var output = results[0].AsTensor<float>();
        var values = output.ToArray();

        // Same scaling as openWakeWord, which its embedding model was trained with.
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (values[i] / 10f) + 2f;
        }

        return values;
    }

    /// <summary>The embedding of the 76 mel frames ending at <paramref name="end"/> (in frames).</summary>
    private float[] Embed(float[] frames, int end = -1)
    {
        var stop = end < 0 ? frames.Length / MelBins : end;
        var window = frames.AsSpan((stop - EmbeddingWindow) * MelBins, EmbeddingWindow * MelBins).ToArray();
        var input = new DenseTensor<float>(window, [1, EmbeddingWindow, MelBins, 1]);
        using var results = embedding.Run([NamedOnnxValue.CreateFromTensor(embeddingInput, input)]);
        return results[0].AsTensor<float>().ToArray();
    }

    private float Classify(float[] window)
    {
        var input = new DenseTensor<float>(window.ToArray(), [1, ClassifierFrames, EmbeddingSize]);
        using var results = classifier.Run([NamedOnnxValue.CreateFromTensor(classifierInput, input)]);
        return results[0].AsTensor<float>().ToArray()[0];
    }

    /// <summary>
    /// The last 16 embeddings of 4 s of quiet noise (±1000 in int16), as openWakeWord fills its buffer at
    /// start. A fixed seed keeps the detector deterministic.
    /// </summary>
    private float[] NoiseFeatures()
    {
        var random = new Random(16_000);
        var noise = new float[4 * 16_000];
        for (var i = 0; i < noise.Length; i++)
        {
            noise[i] = random.Next(-1_000, 1_000);
        }

        var frames = Mel(noise);
        var count = frames.Length / MelBins;
        var result = new float[ClassifierFrames * EmbeddingSize];
        var slot = ClassifierFrames - 1;
        for (var end = count - (((count - EmbeddingWindow) % EmbeddingStride)); end >= EmbeddingWindow && slot >= 0; end -= EmbeddingStride, slot--)
        {
            Embed(frames, end).CopyTo(result, slot * EmbeddingSize);
        }

        return result;
    }
}
