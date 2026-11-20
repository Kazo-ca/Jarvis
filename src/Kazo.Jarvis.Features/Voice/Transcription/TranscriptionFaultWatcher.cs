using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>
/// Clears the "Whisper missing" fault as soon as a Whisper model is loaded (US1.17, work item stage1/15-faults):
/// after a download from the models page, a change of model, or the new attempt made before a transcription. The
/// fault itself is raised only by a failed turn (work item 10: no fault at start when Whisper is missing).
/// </summary>
public sealed class TranscriptionFaultWatcher : IHostedService
{
    private readonly RecognizerKeeper keeper;
    private readonly IMessageBus bus;
    private readonly IAssistantStateReader state;

    /// <summary>Creates the watcher.</summary>
    /// <param name="keeper">Says when Whisper is loaded.</param>
    /// <param name="bus">Where the fault is cleared.</param>
    /// <param name="state">Tells whether the current fault is Whisper's.</param>
    public TranscriptionFaultWatcher(RecognizerKeeper keeper, IMessageBus bus, IAssistantStateReader state)
    {
        this.keeper = keeper;
        this.bus = bus;
        this.state = state;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        keeper.StatusChanged += OnStatusChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        keeper.StatusChanged -= OnStatusChanged;
        return Task.CompletedTask;
    }

    private void OnStatusChanged()
    {
        if (keeper.Status.State != RecognizerState.Ready
            || state.Current.Fault is not { Cause: FaultCause.ModelMissing, Origin: FaultOrigin.Transcription or null })
        {
            return;
        }

        // Raised on the loading thread: publish without holding it.
        _ = bus.PublishAsync<AssistantInput>(new FaultResolved(FaultCause.ModelMissing, FaultOrigin.Transcription)).AsTask();
    }
}
