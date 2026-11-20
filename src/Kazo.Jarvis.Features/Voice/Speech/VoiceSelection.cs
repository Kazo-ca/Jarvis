using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.Voice.Speech;

/// <summary>
/// The <see cref="IVoiceSelection"/> of this slice: keeps the choice in <see cref="SpeechOptions"/> and lists the
/// voices of every registered engine.
/// </summary>
public sealed class VoiceSelection : IVoiceSelection, IDisposable
{
    private readonly IReadOnlyList<ISpeechEngine> engines;
    private readonly ISettingsStore settings;
    private readonly ILogger logger;
    private readonly IDisposable watch;

    /// <summary>Creates the selection.</summary>
    /// <param name="engines">Every engine the host registered.</param>
    /// <param name="settings">The settings, for <see cref="SpeechOptions.Section"/>.</param>
    /// <param name="logger">Log of the engines that fail to list.</param>
    public VoiceSelection(IEnumerable<ISpeechEngine> engines, ISettingsStore settings, ILogger<VoiceSelection>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(settings);

        // Local engines first: they are listed first and preferred as fallback.
        this.engines = [.. engines.OrderBy(engine => engine.IsCloud)];
        this.settings = settings;
        this.logger = logger ?? NullLogger<VoiceSelection>.Instance;
        watch = settings.Watch(SpeechOptions.Section, _ => Changed?.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public VoiceChoice ChoiceFor(AssistantLanguage language) => settings.Read(SpeechOptions.Section).ChoiceFor(language);

    /// <inheritdoc />
    public ValueTask<SettingsWriteResult> ChooseAsync(AssistantLanguage language, VoiceChoice choice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(choice);
        var current = settings.Read(SpeechOptions.Section);
        return settings.WriteAsync(SpeechOptions.Section, current.With(language, choice), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        var lists = await Task.WhenAll(engines.Select(engine => ListAsync(engine, cancellationToken))).ConfigureAwait(false);
        return [.. lists.SelectMany(list => list)];
    }

    /// <inheritdoc />
    public async ValueTask<(SpeechVoice Voice, double Speed)?> ResolveAsync(AssistantLanguage language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(language);
        var choice = ChoiceFor(language);
        var voices = await ListVoicesAsync(cancellationToken).ConfigureAwait(false);
        if (voices.FirstOrDefault(voice => voice.Is(choice) && voice.Speaks(language.Code)) is { } chosen)
        {
            return (chosen, choice.ClampedSpeed);
        }

        // The chosen voice is gone (model deleted, cloud key removed): a local voice of the language takes over,
        // never a cloud one the user did not choose (ADR 0009, section 9).
        var fallback = voices.FirstOrDefault(voice => voice.Is(SpeechDefaults.For(language)))
            ?? voices.FirstOrDefault(voice => !voice.LeavesMachine && voice.Language == language.Code);
        if (fallback is null)
        {
            return null;
        }

        logger.LogWarning("Voice {Engine}:{Voice} is not usable; {Fallback} speaks {Language} instead.",
            choice.Engine, choice.Voice, $"{fallback.Engine}:{fallback.Id}", language.Code);
        return (fallback, choice.ClampedSpeed);
    }

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();

    private async Task<IReadOnlyList<SpeechVoice>> ListAsync(ISpeechEngine engine, CancellationToken cancellationToken)
    {
        try
        {
            return await engine.ListVoicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The contract says an engine never throws while listing; one that does must not hide the others.
            logger.LogWarning(exception, "Engine {Engine} could not list its voices.", engine.Engine);
            return [];
        }
    }
}
