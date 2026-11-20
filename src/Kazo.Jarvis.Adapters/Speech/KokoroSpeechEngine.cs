using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Speech;
using SherpaOnnx;

namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>
/// Kokoro voices by sherpa-onnx 1.13.8, the "quality" option (ADR 0006, section 5): 1.6 to 1.7 s per sentence
/// on the processor in spike E4. Usable when <c>kokoro-multi-lang-v1_0</c> is in the models folder. One model
/// file holds every speaker; it is loaded once per language.
/// </summary>
public sealed class KokoroSpeechEngine : ISpeechEngine, IDisposable
{
    /// <summary>Name of the engine in the settings.</summary>
    public const string Name = "kokoro";

    /// <summary>The folder as sherpa-onnx publishes it.</summary>
    public const string FolderName = "kokoro-multi-lang-v1_0";

    /// <summary>
    /// The speakers offered, with their number in <c>kokoro-multi-lang-v1_0</c>. <c>ff_siwis</c> (30) and
    /// <c>bm_george</c> (26) are those of spike E4; the others are a few English ones of the same model.
    /// </summary>
    private static readonly (string Id, int Speaker, string Language, string Name)[] Speakers =
    [
        ("ff_siwis", 30, "fr", "Siwis (fr)"),
        ("bm_george", 26, "en", "George (en-GB)"),
        ("bm_lewis", 27, "en", "Lewis (en-GB)"),
        ("bm_daniel", 24, "en", "Daniel (en-GB)"),
        ("bf_emma", 21, "en", "Emma (en-GB)"),
        ("am_michael", 16, "en", "Michael (en-US)"),
        ("af_heart", 3, "en", "Heart (en-US)"),
    ];

    private static readonly string[] RequiredFiles = ["model.onnx", "voices.bin", "tokens.txt"];

    private readonly string folder;
    private readonly SherpaModelCache cache = new();

    /// <summary>Creates the engine.</summary>
    /// <param name="modelsFolder">The models folder (<c>%USERPROFILE%\.kazo\jarvis\models</c>).</param>
    public KokoroSpeechEngine(string modelsFolder)
    {
        ArgumentNullException.ThrowIfNull(modelsFolder);
        folder = Path.Combine(modelsFolder, FolderName);
    }

    /// <inheritdoc />
    public string Engine => Name;

    /// <inheritdoc />
    public bool IsCloud => false;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<SpeechVoice>>(IsInstalled()
            ? [.. Speakers.Select(speaker => new SpeechVoice(Name, speaker.Id, speaker.Name, speaker.Language, LeavesMachine: false))]
            : []);

    /// <inheritdoc />
    public async ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var speaker = Speakers.FirstOrDefault(candidate => candidate.Id == voice.Id);
        if (speaker.Id is null)
        {
            throw new SpeechException(SpeechFailure.UnknownVoice, $"Kokoro has no speaker '{voice.Id}'.");
        }

        if (!IsInstalled())
        {
            throw new SpeechException(SpeechFailure.ModelMissing, $"{FolderName} is not in the models folder.");
        }

        var model = cache.Get(speaker.Language, () => Configure(speaker.Language), $"kokoro:{voice.Id}");
        return await model.GenerateAsync(text, speed, speaker.Speaker, $"kokoro:{voice.Id}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => cache.Dispose();

    private bool IsInstalled() =>
        RequiredFiles.All(file => File.Exists(Path.Combine(folder, file))) && Directory.Exists(Path.Combine(folder, "espeak-ng-data"));

    private OfflineTtsConfig Configure(string language)
    {
        var config = new OfflineTtsConfig();
        config.Model.Kokoro.Model = Path.Combine(folder, "model.onnx");
        config.Model.Kokoro.Voices = Path.Combine(folder, "voices.bin");
        config.Model.Kokoro.Tokens = Path.Combine(folder, "tokens.txt");
        config.Model.Kokoro.DataDir = Path.Combine(folder, "espeak-ng-data");

        // As in spike E4: English goes through the US lexicon, French through espeak-ng alone.
        var english = language == "en";
        var lexicon = Path.Combine(folder, "lexicon-us-en.txt");
        config.Model.Kokoro.Lexicon = english && File.Exists(lexicon) ? lexicon : string.Empty;
        config.Model.Kokoro.Lang = english ? "en-us" : language;
        config.Model.NumThreads = 4;
        config.Model.Provider = "cpu";
        return config;
    }
}
