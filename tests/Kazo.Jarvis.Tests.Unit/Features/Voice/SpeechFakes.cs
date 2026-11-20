using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>An engine whose voices the test sets, and which keeps what it was asked to say.</summary>
internal sealed class FakeSpeechEngine(string engine, bool cloud = false) : ISpeechEngine
{
    public List<SpeechVoice> Voices { get; } = [];

    public List<(SpeechVoice Voice, string Text, double Speed)> Said { get; } = [];

    /// <summary>When set, synthesis of a sentence containing this text fails with <see cref="Failure"/>.</summary>
    public string? FailOn { get; set; }

    public SpeechFailure Failure { get; set; } = SpeechFailure.ModelMissing;

    public bool ThrowWhileListing { get; set; }

    public string Engine => engine;

    public bool IsCloud => cloud;

    public SpeechVoice Add(string id, string? language)
    {
        var voice = new SpeechVoice(engine, id, id, language, cloud);
        Voices.Add(voice);
        return voice;
    }

    public ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        ThrowWhileListing ? throw new InvalidOperationException("broken engine") : ValueTask.FromResult<IReadOnlyList<SpeechVoice>>([.. Voices]);

    public ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailOn is { } failOn && text.Contains(failOn, StringComparison.Ordinal))
        {
            throw new SpeechException(Failure, "fake failure");
        }

        lock (Said)
        {
            Said.Add((voice, text, speed));
        }

        // A tenth of a second of a loud tone, so the envelope is not silent.
        var samples = Enumerable.Range(0, 1600).Select(i => (float)(0.5 * Math.Sin(i * 0.3))).ToArray();
        return ValueTask.FromResult(new AudioClip(samples, 16_000));
    }
}

/// <summary>A playback that keeps each clip; when <see cref="Hold"/> is set, a clip plays until stopped.</summary>
internal sealed class FakePlayback : IAudioPlayback
{
    private readonly Lock gate = new();
    private CancellationTokenSource stopAll = new();

    public List<AudioClip> Played { get; } = [];

    public int StopAllCount { get; private set; }

    public bool Hold { get; set; }

    public bool Unavailable { get; set; }

    /// <summary>Completes when a clip starts playing.</summary>
    public TaskCompletionSource Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string? SelectedOutput => null;

    public IReadOnlyList<AudioDevice> ListOutputs() => [];

    public void SelectOutput(string? deviceId)
    {
    }

    public async ValueTask PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            throw new AudioDeviceUnavailableException("no output");
        }

        CancellationToken stop;
        lock (gate)
        {
            Played.Add(clip);
            stop = stopAll.Token;
        }

        Started.TrySetResult();
        if (Hold)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stop);
            try
            {
                await Task.Delay(Timeout.Infinite, linked.Token);
            }
            catch (OperationCanceledException)
            {
                // Stopped, as the real playback.
            }
        }
    }

    public void StopAll()
    {
        lock (gate)
        {
            StopAllCount++;
            stopAll.Cancel();
            stopAll = new CancellationTokenSource();
        }
    }

    public void ResetStarted() => Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
