using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Features.Voice.TestVoice;

/// <summary>What went wrong on the voices page.</summary>
public enum TrialProblem
{
    /// <summary>The model files of the voice are missing.</summary>
    ModelMissing,

    /// <summary>The cloud key is missing.</summary>
    KeyMissing,

    /// <summary>The cloud service refused the key.</summary>
    KeyRefused,

    /// <summary>The cloud service cannot be reached.</summary>
    Unreachable,

    /// <summary>No output can play the sound.</summary>
    NoOutput,

    /// <summary>The choice could not be saved.</summary>
    SaveFailed,

    /// <summary>Anything else.</summary>
    Failed,
}

/// <summary>
/// The logic of the voices page (US1.11): the voices of a language, the one tried, its speed, listening to a
/// sentence through the same engines as the conversation (ADR 0006, section 5), and saving the default voice of
/// each language. One per page.
/// </summary>
public sealed class VoiceTrial : IDisposable
{
    private readonly IVoiceSelection selection;
    private readonly IReadOnlyList<ISpeechEngine> engines;
    private readonly IAudioPlayback playback;
    private IReadOnlyList<SpeechVoice> allVoices = [];
    private CancellationTokenSource? listening;

    /// <summary>Creates the trial on the language of the assistant.</summary>
    /// <param name="selection">The voice of each language.</param>
    /// <param name="engines">The engines that speak.</param>
    /// <param name="playback">The playback, on the output chosen in the audio page.</param>
    /// <param name="language">The language of the assistant, tried first.</param>
    public VoiceTrial(IVoiceSelection selection, IEnumerable<ISpeechEngine> engines, IAudioPlayback playback, IAssistantLanguageReader language)
    {
        this.selection = selection;
        this.engines = [.. engines];
        this.playback = playback;
        Language = language.Current;
        Speed = VoiceChoice.NormalSpeed;
    }

    /// <summary>Raised when something shown changed (end of a sentence, failure, choice saved).</summary>
    public event EventHandler? Changed;

    /// <summary>The language tried.</summary>
    public AssistantLanguage Language { get; private set; }

    /// <summary>The voices that speak <see cref="Language"/>, local ones first.</summary>
    public IReadOnlyList<SpeechVoice> Voices => [.. allVoices.Where(voice => voice.Speaks(Language.Code))];

    /// <summary>The voice tried, or <see langword="null"/> when the language has none.</summary>
    public SpeechVoice? Selected { get; private set; }

    /// <summary>The speed tried.</summary>
    public double Speed { get; private set; }

    /// <summary>Whether a sentence is being said.</summary>
    public bool IsPlaying => listening is not null;

    /// <summary>The last problem, cleared by the next action.</summary>
    public TrialProblem? Problem { get; private set; }

    /// <summary>Whether the voice and speed tried are the saved ones of <see cref="Language"/>.</summary>
    public bool IsDefault
    {
        get
        {
            var choice = selection.ChoiceFor(Language);
            return Selected?.Is(choice) == true && Math.Abs(choice.ClampedSpeed - Speed) < 0.001;
        }
    }

    /// <summary>The key of a voice in the list of the page.</summary>
    /// <param name="voice">The voice.</param>
    /// <returns><c>engine:id</c>.</returns>
    public static string KeyOf(SpeechVoice voice) => $"{voice.Engine}:{voice.Id}";

    /// <summary>Reads the voices again (on opening, after a cloud key was saved) and keeps the voice tried if still there.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>A task.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var kept = Selected;
        var keptSpeed = Speed;
        allVoices = await selection.ListVoicesAsync(cancellationToken);
        ShowLanguage(Language);
        if (kept is not null && Voices.FirstOrDefault(voice => KeyOf(voice) == KeyOf(kept)) is { } still)
        {
            Selected = still;
            Speed = keptSpeed;
        }
    }

    /// <summary>Tries another language: its saved voice and speed are selected.</summary>
    /// <param name="language">The language.</param>
    public void ShowLanguage(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        Language = language;
        var choice = selection.ChoiceFor(language);
        var voices = Voices;
        Selected = voices.FirstOrDefault(voice => voice.Is(choice)) ?? voices.FirstOrDefault(voice => !voice.LeavesMachine) ?? voices.FirstOrDefault();
        Speed = choice.ClampedSpeed;
        Problem = null;
    }

    /// <summary>Tries another voice of the language.</summary>
    /// <param name="key">Its <see cref="KeyOf"/>.</param>
    public void Select(string? key)
    {
        if (Voices.FirstOrDefault(voice => KeyOf(voice) == key) is { } voice)
        {
            Selected = voice;
            Problem = null;
        }
    }

    /// <summary>Changes the speed tried.</summary>
    /// <param name="speed">The new speed, clamped to the offered range.</param>
    public void ChangeSpeed(double speed) => Speed = new VoiceChoice(string.Empty, string.Empty, speed).ClampedSpeed;

    /// <summary>Says <paramref name="text"/> with the voice tried; stops a sentence already playing.</summary>
    /// <param name="text">The sentence.</param>
    /// <returns>A task that completes when the sentence was said, stopped, or failed.</returns>
    public async Task ListenAsync(string text)
    {
        Stop();
        if (Selected is not { } voice || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var cancel = new CancellationTokenSource();
        listening = cancel;
        Problem = null;
        try
        {
            var engine = engines.FirstOrDefault(candidate => candidate.Engine == voice.Engine)
                ?? throw new SpeechException(SpeechFailure.UnknownVoice, $"No engine '{voice.Engine}'.");
            var clip = await engine.SynthesizeAsync(voice, text.Trim(), Speed, cancel.Token);
            await playback.PlayAsync(clip, cancel.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Stopped by the user or by another sentence.
        }
        catch (SpeechException exception)
        {
            Problem = exception.Failure switch
            {
                SpeechFailure.ModelMissing => TrialProblem.ModelMissing,
                SpeechFailure.KeyMissing => TrialProblem.KeyMissing,
                SpeechFailure.KeyRefused => TrialProblem.KeyRefused,
                SpeechFailure.Unreachable => TrialProblem.Unreachable,
                _ => TrialProblem.Failed,
            };
        }
        catch (AudioDeviceUnavailableException)
        {
            Problem = TrialProblem.NoOutput;
        }
        finally
        {
            if (listening == cancel)
            {
                listening = null;
            }

            cancel.Dispose();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Stops the sentence playing, if any. Only this page's sound stops, not Jarvis's voice.</summary>
    public void Stop()
    {
        var cancel = listening;
        listening = null;
        try
        {
            cancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already finished.
        }
    }

    /// <summary>Saves the voice and speed tried as those of <see cref="Language"/>.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Whether it was saved.</returns>
    public async Task<bool> MakeDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (Selected is not { } voice)
        {
            return false;
        }

        SettingsWriteResult result = await selection.ChooseAsync(Language, voice.ToChoice(Speed), cancellationToken);
        Problem = result.IsSaved ? null : TrialProblem.SaveFailed;
        Changed?.Invoke(this, EventArgs.Empty);
        return result.IsSaved;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
