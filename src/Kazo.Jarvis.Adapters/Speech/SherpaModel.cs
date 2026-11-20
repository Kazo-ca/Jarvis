using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Speech;
using SherpaOnnx;

namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>
/// One sherpa-onnx voice model loaded in memory, shared by the voices it holds. sherpa-onnx is not documented as
/// thread-safe for one model: the generations of a model follow each other.
/// </summary>
internal sealed class SherpaModel : IDisposable
{
    private readonly OfflineTts tts;
    private readonly Lock gate = new();
    private bool disposed;

    public SherpaModel(OfflineTtsConfig config)
    {
        tts = new OfflineTts(config);
    }

    /// <summary>Speaks <paramref name="text"/> with speaker <paramref name="speakerId"/>, off the caller's thread.</summary>
    public Task<AudioClip> GenerateAsync(string text, double speed, int speakerId, string voiceName, CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                lock (gate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(disposed, this);
                    var audio = tts.Generate(text, (float)speed, speakerId);
                    try
                    {
                        return audio.NumSamples > 0
                            ? new AudioClip(audio.Samples, audio.SampleRate)
                            : throw new SpeechException(SpeechFailure.Failed, $"{voiceName} produced no sound.");
                    }
                    finally
                    {
                        audio.Dispose();
                    }
                }
            },
            cancellationToken);

    public void Dispose()
    {
        lock (gate)
        {
            if (!disposed)
            {
                disposed = true;
                tts.Dispose();
            }
        }
    }
}

/// <summary>The models of an engine, loaded at first use and kept until the engine is disposed.</summary>
internal sealed class SherpaModelCache : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, SherpaModel> models = new(StringComparer.Ordinal);

    /// <summary>The model named <paramref name="key"/>, created by <paramref name="create"/> the first time.</summary>
    public SherpaModel Get(string key, Func<OfflineTtsConfig> create, string voiceName)
    {
        lock (gate)
        {
            if (models.TryGetValue(key, out var model))
            {
                return model;
            }

            try
            {
                model = new SherpaModel(create());
            }
            catch (Exception exception) when (exception is not SpeechException)
            {
                throw new SpeechException(SpeechFailure.ModelMissing, $"The model of {voiceName} could not be loaded: {exception.Message}", exception);
            }

            models[key] = model;
            return model;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (var model in models.Values)
            {
                model.Dispose();
            }

            models.Clear();
        }
    }
}
