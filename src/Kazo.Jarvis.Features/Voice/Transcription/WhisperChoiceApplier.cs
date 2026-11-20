using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>
/// Saves the Whisper model the user installed from the models page or the first-run wizard (work item
/// stage1/14-first-run). This slice owns the setting; <see cref="RecognizerKeeper"/> then loads the model.
/// </summary>
public sealed partial class WhisperChoiceApplier : IEventHandler<WhisperModelChosen>
{
    private readonly ISettingsStore settings;
    private readonly ILogger<WhisperChoiceApplier> logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="settings">The settings, for <see cref="TranscriptionOptions.Section"/>.</param>
    /// <param name="logger">Logs the change.</param>
    public WhisperChoiceApplier(ISettingsStore settings, ILogger<WhisperChoiceApplier> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(WhisperModelChosen message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var current = settings.Read(TranscriptionOptions.Section);
        if (current.Model == message.Model && current.Device == message.Device)
        {
            return;
        }

        var result = await settings.WriteAsync(
            TranscriptionOptions.Section,
            current with { Model = message.Model, Device = message.Device },
            cancellationToken).ConfigureAwait(false);
        if (result.IsSaved)
        {
            LogApplied(message.Model, message.Device);
        }
        else
        {
            LogNotSaved(result.Outcome);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Whisper model chosen in the models page: {Model} on {Device}.")]
    private partial void LogApplied(Kernel.Voice.WhisperModel model, Kernel.Voice.ComputeDevice device);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Whisper model chosen could not be saved ({Outcome}).")]
    private partial void LogNotSaved(SettingsWriteOutcome outcome);
}
