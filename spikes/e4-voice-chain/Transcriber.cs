using System.Diagnostics;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Spikes.VoiceChain;

/// <summary>Whisper.net transcription; the device is chosen once per process (CUDA first, then CPU).</summary>
public sealed class Transcriber : IDisposable
{
    readonly WhisperFactory _factory;
    readonly WhisperProcessor _processor;
    public string Model { get; }

    public static void SelectRuntime(bool gpu) =>
        RuntimeOptions.RuntimeLibraryOrder = gpu ? [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu] : [RuntimeLibrary.Cpu];

    public static string LoadedRuntime => RuntimeOptions.LoadedLibrary?.ToString() ?? "not loaded";

    public Transcriber(string modelPath, string language, bool gpu, int threads = 8)
    {
        Model = Path.GetFileNameWithoutExtension(modelPath);
        _factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = gpu });
        _processor = _factory.CreateBuilder().WithLanguage(language).WithThreads(threads).Build();
    }

    public async Task<(string Text, long Ms)> TranscribeAsync(float[] samples16k)
    {
        var sw = Stopwatch.StartNew();
        var parts = new List<string>();
        await foreach (var seg in _processor.ProcessAsync(samples16k)) parts.Add(seg.Text);
        return (string.Join(" ", parts).Trim(), sw.ElapsedMilliseconds);
    }

    public void Dispose() { _processor.Dispose(); _factory.Dispose(); }
}
