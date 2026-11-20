using System.Globalization;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Speech;
using SherpaOnnx;

namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>
/// Piper voices by sherpa-onnx 1.13.8 (ADR 0006, section 5), the default ones. A voice is usable when its
/// folder, as sherpa-onnx publishes it (<c>vits-piper-fr_FR-tom-medium</c>), is in the models folder with its
/// <c>.onnx</c> file, <c>tokens.txt</c> and <c>espeak-ng-data</c>. Any Piper voice put there is listed.
/// </summary>
public sealed class PiperSpeechEngine : ISpeechEngine, IDisposable
{
    /// <summary>Name of the engine in the settings.</summary>
    public const string Name = "piper";

    private const string FolderPrefix = "vits-piper-";

    private readonly string modelsFolder;
    private readonly SherpaModelCache cache = new();

    /// <summary>Creates the engine.</summary>
    /// <param name="modelsFolder">The models folder (<c>%USERPROFILE%\.kazo\jarvis\models</c>).</param>
    public PiperSpeechEngine(string modelsFolder)
    {
        ArgumentNullException.ThrowIfNull(modelsFolder);
        this.modelsFolder = modelsFolder;
    }

    /// <inheritdoc />
    public string Engine => Name;

    /// <inheritdoc />
    public bool IsCloud => false;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<SpeechVoice>>([.. FindVoices().Select(found => found.Voice)]);

    /// <inheritdoc />
    public async ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var found = FindVoices().FirstOrDefault(candidate => candidate.Voice.Id == voice.Id)
            ?? throw new SpeechException(SpeechFailure.ModelMissing, $"Piper voice '{voice.Id}' is not in the models folder.");
        var model = cache.Get(found.Folder, () => Configure(found), $"piper:{voice.Id}");
        return await model.GenerateAsync(text, speed, 0, $"piper:{voice.Id}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => cache.Dispose();

    /// <summary>
    /// The voice a folder named like sherpa-onnx's holds, from its name alone: <c>fr_FR-tom-medium</c> gives
    /// "Tom (fr_FR, medium)" in French.
    /// </summary>
    /// <param name="voiceId">The name of the <c>.onnx</c> file without its extension.</param>
    /// <returns>The voice, or <see langword="null"/> when the name does not follow Piper's pattern.</returns>
    internal static SpeechVoice? Describe(string voiceId)
    {
        var parts = voiceId.Split('-');
        if (parts.Length < 2 || parts[0].Length < 2)
        {
            return null;
        }

        var locale = parts[0];
        var speaker = string.Join(' ', parts[1].Split('_').Select(Capitalize));
        var quality = parts.Length > 2 ? $", {string.Join('-', parts[2..])}" : string.Empty;
        return new SpeechVoice(Name, voiceId, $"{speaker} ({locale}{quality})", locale[..2].ToLowerInvariant(), LeavesMachine: false);
    }

    private IEnumerable<FoundVoice> FindVoices()
    {
        if (!Directory.Exists(modelsFolder))
        {
            yield break;
        }

        foreach (var folder in Directory.EnumerateDirectories(modelsFolder, FolderPrefix + "*").Order(StringComparer.Ordinal))
        {
            var model = Directory.EnumerateFiles(folder, "*.onnx").FirstOrDefault();
            if (model is null
                || !File.Exists(Path.Combine(folder, "tokens.txt"))
                || !Directory.Exists(Path.Combine(folder, "espeak-ng-data"))
                || Describe(Path.GetFileNameWithoutExtension(model)) is not { } voice)
            {
                continue;
            }

            yield return new FoundVoice(voice, folder, model);
        }
    }

    private static OfflineTtsConfig Configure(FoundVoice found)
    {
        var config = new OfflineTtsConfig();
        config.Model.Vits.Model = found.Model;
        config.Model.Vits.Tokens = Path.Combine(found.Folder, "tokens.txt");
        config.Model.Vits.DataDir = Path.Combine(found.Folder, "espeak-ng-data");

        // Four threads as in spike E4: 0.16 to 0.40 s for a 3.5 s sentence.
        config.Model.NumThreads = 4;
        config.Model.Provider = "cpu";
        return config;
    }

    private static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..];

    private sealed record FoundVoice(SpeechVoice Voice, string Folder, string Model);
}
