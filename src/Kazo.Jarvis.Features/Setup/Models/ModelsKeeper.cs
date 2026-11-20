using System.Collections.Concurrent;
using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Models;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Setup.Models;

/// <summary>
/// The downloads of the models page and of the first-run wizard (ADR 0006, section 7): the state of each model, one
/// batch at a time, one file after the other. A batch goes on when its page closes, so the wizard can move on while
/// the voices arrive. Once the chosen Whisper model is in place, <see cref="WhisperModelChosen"/> tells the
/// transcription slice to use it.
/// </summary>
public sealed partial class ModelsKeeper : IDisposable
{
    private readonly IModelInstaller installer;
    private readonly IGraphicsCardProbe probe;
    private readonly IMessageBus bus;
    private readonly ILogger<ModelsKeeper> logger;
    private readonly ConcurrentDictionary<string, ModelStatus> running = new(StringComparer.Ordinal);
    private readonly Lock gate = new();
    private readonly CancellationTokenSource disposal = new();
    private Task<ModelAdvice>? advice;
    private CancellationTokenSource? batch;

    /// <summary>Creates the keeper.</summary>
    /// <param name="installer">Puts the models in the models folder.</param>
    /// <param name="probe">Finds the NVIDIA card, for the advice.</param>
    /// <param name="bus">Where the Whisper choice is published.</param>
    /// <param name="logger">Logs the batches.</param>
    public ModelsKeeper(IModelInstaller installer, IGraphicsCardProbe probe, IMessageBus bus, ILogger<ModelsKeeper> logger)
    {
        this.installer = installer;
        this.probe = probe;
        this.bus = bus;
        this.logger = logger;
    }

    /// <summary>Raised when a state changes or a download moves on, on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>Whether a batch is running.</summary>
    public bool IsBusy
    {
        get
        {
            lock (gate)
            {
                return batch is not null;
            }
        }
    }

    /// <summary>The advice for this PC, found once (the card does not change while Jarvis runs).</summary>
    /// <returns>The advice.</returns>
    public Task<ModelAdvice> GetAdviceAsync()
    {
        lock (gate)
        {
            return advice ??= FindAdviceAsync();
        }
    }

    /// <summary>The state of a model now.</summary>
    /// <param name="item">The model.</param>
    /// <returns>Its state.</returns>
    public ModelStatus StatusOf(ModelItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (running.TryGetValue(item.Id, out var status) && status.State != ModelState.Failed)
        {
            return status;
        }

        if (IsInstalled(item))
        {
            return new ModelStatus(ModelState.Installed);
        }

        return status ?? new ModelStatus(ModelState.Missing);
    }

    /// <summary>Whether every file of a model is in place.</summary>
    /// <param name="item">The model.</param>
    /// <returns><see langword="true"/> when it is installed.</returns>
    public bool IsInstalled(ModelItem item) => item.Downloads.All(installer.IsInstalled);

    /// <summary>
    /// The models proposed on this PC: the wake word, Silero, the advised Whisper model and the Piper voices of both
    /// languages (both are required in the first version, ADR 0008). Kokoro, 350 MB, stays a choice.
    /// </summary>
    /// <param name="advice">The advice for this PC.</param>
    /// <returns>The ids of the models proposed.</returns>
    public static IReadOnlySet<string> Proposed(ModelAdvice advice)
    {
        ArgumentNullException.ThrowIfNull(advice);
        return new HashSet<string>(StringComparer.Ordinal)
        {
            ModelCatalog.WakeWordId,
            ModelCatalog.SileroId,
            ModelCatalog.For(advice.Whisper).Id,
            ModelCatalog.PiperFrenchId,
            ModelCatalog.PiperEnglishId,
        };
    }

    /// <summary>
    /// Downloads the models of <paramref name="items"/> that are missing, one after the other, then publishes the Whisper
    /// choice when <paramref name="whisper"/> is installed. Does nothing while another batch runs.
    /// </summary>
    /// <param name="items">The models chosen.</param>
    /// <param name="whisper">The Whisper model chosen, or <see langword="null"/> to leave the transcription as it is.</param>
    /// <returns>Whether every model chosen is installed at the end.</returns>
    public async Task<bool> InstallAsync(IReadOnlyList<ModelItem> items, WhisperModel? whisper)
    {
        ArgumentNullException.ThrowIfNull(items);
        CancellationTokenSource source;
        lock (gate)
        {
            if (batch is not null || disposal.IsCancellationRequested)
            {
                return false;
            }

            source = CancellationTokenSource.CreateLinkedTokenSource(disposal.Token);
            batch = source;
        }

        var missing = items.Where(item => !IsInstalled(item)).ToList();
        foreach (var item in missing)
        {
            running[item.Id] = new ModelStatus(ModelState.Queued);
        }

        RaiseChanged();
        var allInstalled = true;
        try
        {
            LogBatch(missing.Count, missing.Sum(item => item.Size));
            foreach (var item in missing)
            {
                allInstalled &= await InstallOneAsync(item, source.Token).ConfigureAwait(false);
            }

            if (whisper is { } model && IsInstalled(ModelCatalog.For(model)))
            {
                var device = (await GetAdviceAsync().ConfigureAwait(false)).Card is null ? ComputeDevice.Cpu : ComputeDevice.Gpu;
                await bus.PublishAsync(new WhisperModelChosen(model, device), source.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            allInstalled = false;
            LogCancelled();
        }
        finally
        {
            // What was queued and never started goes back to missing.
            foreach (var item in missing)
            {
                if (running.TryGetValue(item.Id, out var status) && status.State is ModelState.Queued or ModelState.Downloading)
                {
                    running.TryRemove(item.Id, out _);
                }
            }

            lock (gate)
            {
                batch = null;
            }

            source.Dispose();
            RaiseChanged();
        }

        return allInstalled && items.All(IsInstalled);
    }

    /// <summary>Stops the running batch. The file being downloaded is deleted; those already in place stay.</summary>
    public void Cancel()
    {
        lock (gate)
        {
            batch?.Cancel();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposal.Cancel();
        disposal.Dispose();
    }

    private async Task<bool> InstallOneAsync(ModelItem item, CancellationToken cancellationToken)
    {
        long done = 0;
        long lastShown = -1;
        var step = Math.Max(1, item.Size / 200);
        try
        {
            foreach (var download in item.Downloads)
            {
                if (installer.IsInstalled(download))
                {
                    done += download.Size;
                    continue;
                }

                var before = done;
                running[item.Id] = new ModelStatus(ModelState.Downloading, before);
                RaiseChanged();
                var progress = new Reporter(received =>
                {
                    var total = before + received;
                    if (total - Interlocked.Read(ref lastShown) < step)
                    {
                        return;
                    }

                    Interlocked.Exchange(ref lastShown, total);
                    running[item.Id] = new ModelStatus(ModelState.Downloading, total);
                    RaiseChanged();
                });
                await installer.InstallAsync(download, progress, cancellationToken).ConfigureAwait(false);
                done += download.Size;
            }

            running.TryRemove(item.Id, out _);
            RaiseChanged();
            return true;
        }
        catch (ModelDownloadException exception)
        {
            LogFailed(item.Id, exception.Failure, exception);
            running[item.Id] = new ModelStatus(ModelState.Failed, Failure: exception.Failure);
            RaiseChanged();
            return false;
        }
    }

    private async Task<ModelAdvice> FindAdviceAsync()
    {
        GraphicsCard? card;
        try
        {
            card = await probe.FindNvidiaCardAsync(disposal.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogProbeFailed(exception);
            card = null;
        }

        return ModelAdvice.For(card);
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            LogHandlerFailed(exception);
        }
    }

    /// <summary>Calls back on the reporting thread: <see cref="Progress{T}"/> would post to a context that may be gone.</summary>
    private sealed class Reporter(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Models: {Count} to download, {Bytes} bytes.")]
    private partial void LogBatch(int count, long bytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Models: downloads stopped by the user.")]
    private partial void LogCancelled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model {Id} not installed ({Failure}).")]
    private partial void LogFailed(string id, ModelDownloadFailure failure, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The graphics card could not be read.")]
    private partial void LogProbeFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "A handler of the models page failed.")]
    private partial void LogHandlerFailed(Exception exception);
}
