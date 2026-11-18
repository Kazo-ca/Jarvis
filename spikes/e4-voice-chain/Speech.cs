using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SherpaOnnx;

namespace Spikes.VoiceChain;

/// <summary>Local text-to-speech through sherpa-onnx: Piper (VITS) and Kokoro voices, no Python, no espeak install.</summary>
public sealed class Speaker : IDisposable
{
    readonly OfflineTts _tts;
    public string Name { get; }
    public int SpeakerId { get; }
    public int SampleRate => _tts.SampleRate;

    Speaker(OfflineTts tts, string name, int sid) { _tts = tts; Name = name; SpeakerId = sid; }

    public static Speaker Piper(string dir, string onnx, string provider = "cpu")
    {
        var c = new OfflineTtsConfig();
        c.Model.Vits.Model = Path.Combine(dir, onnx);
        c.Model.Vits.Tokens = Path.Combine(dir, "tokens.txt");
        c.Model.Vits.DataDir = Path.Combine(dir, "espeak-ng-data");
        c.Model.NumThreads = 4; c.Model.Provider = provider;
        return new Speaker(new OfflineTts(c), "piper:" + Path.GetFileNameWithoutExtension(onnx), 0);
    }

    public static Speaker Kokoro(string dir, int sid, string lang, string provider = "cpu")
    {
        var c = new OfflineTtsConfig();
        c.Model.Kokoro.Model = Path.Combine(dir, "model.onnx");
        c.Model.Kokoro.Voices = Path.Combine(dir, "voices.bin");
        c.Model.Kokoro.Tokens = Path.Combine(dir, "tokens.txt");
        c.Model.Kokoro.DataDir = Path.Combine(dir, "espeak-ng-data");
        c.Model.Kokoro.Lexicon = lang.StartsWith("en") ? Path.Combine(dir, "lexicon-us-en.txt") : "";
        c.Model.Kokoro.Lang = lang;
        c.Model.NumThreads = 4; c.Model.Provider = provider;
        return new Speaker(new OfflineTts(c), $"kokoro:{sid}:{lang}", sid);
    }

    public float[] Say(string text, float speed = 1f)
    {
        var audio = _tts.Generate(text, speed, SpeakerId);
        try { return audio.NumSamples > 0 ? audio.Samples : throw new InvalidOperationException($"{Name} produced no audio for: {text}"); } finally { audio.Dispose(); }
    }

    public void Dispose() => _tts.Dispose();
}

public static class Audio
{
    public static float[] Resample(float[] samples, int from, int to)
    {
        if (from == to) return samples;
        var src = new RawFloatProvider(samples, from);
        var rs = new WdlResamplingSampleProvider(src, to);
        var outBuf = new List<float>(); var buf = new float[4096]; int n;
        while ((n = rs.Read(buf.AsSpan())) > 0) outBuf.AddRange(buf.AsSpan(0, n).ToArray());
        return outBuf.ToArray();
    }

    public static float[] Silence(double seconds, int rate = 16000) => new float[(int)(seconds * rate)];

    public static void Save(string path, float[] samples, int rate)
    {
        using var w = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(rate, 1));
        w.WriteSamples(samples, 0, samples.Length);
    }

    sealed class RawFloatProvider(float[] data, int rate) : ISampleProvider
    {
        int _pos;
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(rate, 1);
        public int Read(Span<float> buffer)
        {
            var n = Math.Min(buffer.Length, data.Length - _pos);
            data.AsSpan(_pos, n).CopyTo(buffer); _pos += n; return n;
        }
    }
}
