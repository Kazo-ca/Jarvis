using System.Globalization;
using System.Resources;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Permissions.Confirmation;

/// <summary>
/// The confirmation requests (ADR 0023): one open at a time, the others wait their turn. A request sets the
/// condition "confirmation pending", opens the confirmation window, has the question said aloud when the turn was
/// spoken, then waits for a click, a recognized spoken answer, its timeout or an interruption. Only a human answer
/// through a channel the class accepts approves; everything else is a refusal.
/// </summary>
public sealed partial class ConfirmationDesk : IConfirmation, IEventHandler<ConfirmationAnswerHeard>, IDisposable
{
    /// <summary>Key of the confirmation window: one window at a time.</summary>
    public const string DialogKey = "Permissions.Confirmation";

    private static readonly ResourceManager Texts = new(typeof(ConfirmationWindow));

    private readonly IMessageBus bus;
    private readonly IDialogService dialogs;
    private readonly ISettingsStore settings;
    private readonly IAssistantStateReader state;
    private readonly IAssistantLanguageReader language;
    private readonly TimeProvider time;
    private readonly ILogger<ConfirmationDesk> logger;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private readonly Lock gate = new();
    private Pending? current;

    /// <summary>Creates the desk.</summary>
    /// <param name="bus">Where the condition and the question go.</param>
    /// <param name="dialogs">Opens the confirmation window.</param>
    /// <param name="settings">The settings, for the timeout.</param>
    /// <param name="state">The assistant state: a muted microphone hears no answer.</param>
    /// <param name="language">The language of the question and of the recognized words.</param>
    /// <param name="time">The clock of the timeout.</param>
    /// <param name="logger">Logs each request without its arguments (ADR 0004, section 6).</param>
    public ConfirmationDesk(
        IMessageBus bus,
        IDialogService dialogs,
        ISettingsStore settings,
        IAssistantStateReader state,
        IAssistantLanguageReader language,
        TimeProvider time,
        ILogger<ConfirmationDesk> logger)
    {
        this.bus = bus;
        this.dialogs = dialogs;
        this.settings = settings;
        this.state = state;
        this.language = language;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Raised after a request opened or closed, on any thread.</summary>
    public event Action? Changed;

    /// <summary>The open request, or <see langword="null"/>.</summary>
    public OpenConfirmation? Current
    {
        get
        {
            lock (gate)
            {
                return current?.View;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConfirmationOutcome> AskAsync(ConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await oneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new ConfirmationOutcome(ConfirmationAnswer.Interrupted, null);
        }

        var spoken = language.Current;
        var timeout = TimeSpan.FromSeconds(Math.Clamp(
            settings.Read(ConfirmationOptions.Section).TimeoutSeconds,
            ConfirmationOptions.MinimumTimeout,
            ConfirmationOptions.MaximumTimeout));
        var say = request.Context.Spoken && request.Context.Turn != TurnId.None;

        // ADR 0023, section 3: a spoken answer only for "Sensitive", in a spoken turn, the microphone not muted.
        var listen = say && request.Class == ActionClass.Sensitive && !state.Current.MicrophoneMuted;
        var summary = string.IsNullOrWhiteSpace(request.Summary) ? Text("GeneralSummary", spoken, request.Tool) : request.Summary.Trim();
        var pending = new Pending(new OpenConfirmation(Guid.NewGuid(), request, summary, time.GetUtcNow() + timeout, listen), spoken);
        lock (gate)
        {
            current = pending;
        }

        var id = pending.View.Id;
        LogOpened(id, request.Tool, request.Class, say, listen);
        try
        {
            await bus.PublishAsync(new ConfirmationOpened(id, request.Class), CancellationToken.None).ConfigureAwait(false);
            dialogs.Open(new DialogRequest(DialogKey, typeof(ConfirmationWindow), Text("WindowTitle", spoken)));
            Changed?.Invoke();
            if (say)
            {
                var question = summary + " " + Text(
                    request.Class == ActionClass.Critical ? "Question_Critical" : listen ? "Question_Voice" : "Question_Click",
                    spoken);
                await bus.PublishAsync(new ConfirmationQuestionToSay(id, request.Context.Turn, question, listen), CancellationToken.None).ConfigureAwait(false);
            }

            using var limit = new CancellationTokenSource(timeout, time);
            using var late = limit.Token.Register(() => pending.Answer.TrySetResult(new ConfirmationOutcome(ConfirmationAnswer.TimedOut, null)));
            using var interrupted = cancellationToken.Register(() => pending.Answer.TrySetResult(new ConfirmationOutcome(ConfirmationAnswer.Interrupted, null)));
            var outcome = await pending.Answer.Task.ConfigureAwait(false);
            LogClosed(id, outcome.Answer, outcome.Channel);
            return outcome;
        }
        finally
        {
            lock (gate)
            {
                current = null;
            }

            dialogs.Close(DialogKey);
            Changed?.Invoke();
            await bus.PublishAsync(new ConfirmationClosed(id), CancellationToken.None).ConfigureAwait(false);
            oneAtATime.Release();
        }
    }

    /// <summary>A click in the confirmation window: accepted for both classes.</summary>
    /// <param name="id">The request the window showed.</param>
    /// <param name="approved">Approve or refuse.</param>
    public void Click(Guid id, bool approved) => Answer(
        id,
        new ConfirmationOutcome(approved ? ConfirmationAnswer.Approved : ConfirmationAnswer.Refused, ConfirmationChannels.Click));

    /// <inheritdoc />
    public ValueTask HandleAsync(ConfirmationAnswerHeard message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Pending? pending;
        lock (gate)
        {
            pending = current;
        }

        if (pending is null || pending.View.Id != message.Id)
        {
            return ValueTask.CompletedTask;
        }

        if (!pending.View.Listens)
        {
            // ADR 0023, section 4: an answer through a channel the class does not accept is rejected and logged.
            LogChannelRejected(message.Id, pending.View.Request.Class);
            return ValueTask.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(message.Text))
        {
            // Nothing heard: the request stays open until its timeout, the click remains possible.
            return ValueTask.CompletedTask;
        }

        // The text is compared to the list and dropped: it is neither logged nor given to a model.
        var answer = ConfirmationWords.Interpret(message.Text, pending.Language) == SpokenAnswer.Yes
            ? ConfirmationAnswer.Approved
            : ConfirmationAnswer.Refused;
        pending.Answer.TrySetResult(new ConfirmationOutcome(answer, ConfirmationChannels.Voice));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Pending? pending;
        lock (gate)
        {
            pending = current;
        }

        pending?.Answer.TrySetResult(new ConfirmationOutcome(ConfirmationAnswer.Interrupted, null));
        oneAtATime.Dispose();
    }

    /// <summary>A text of the window's resources in the language of the assistant.</summary>
    internal static string Text(string key, AssistantLanguage language, params object[] arguments)
    {
        var text = Texts.GetString(key, language.Culture) ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, arguments);
    }

    private void Answer(Guid id, ConfirmationOutcome outcome)
    {
        Pending? pending;
        lock (gate)
        {
            pending = current;
        }

        if (pending is not null && pending.View.Id == id)
        {
            pending.Answer.TrySetResult(outcome);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation {Id} opened for {Tool} (class {Class}); question said: {Said}, spoken answer accepted: {Listens}.")]
    private partial void LogOpened(Guid id, string tool, ActionClass @class, bool said, bool listens);

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation {Id} closed: {Answer} (channel {Channel}).")]
    private partial void LogClosed(Guid id, ConfirmationAnswer answer, string? channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Confirmation {Id}: a spoken answer was rejected, the class {Class} or the turn does not accept the voice.")]
    private partial void LogChannelRejected(Guid id, ActionClass @class);

    private sealed record Pending(OpenConfirmation View, AssistantLanguage Language)
    {
        public TaskCompletionSource<ConfirmationOutcome> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

/// <summary>The open request, as the confirmation window shows it.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Request">What is asked, and by whom.</param>
/// <param name="Summary">The sentence shown and said.</param>
/// <param name="Deadline">When it is denied without an answer.</param>
/// <param name="Listens">Whether a spoken answer is accepted.</param>
public sealed record OpenConfirmation(Guid Id, ConfirmationRequest Request, string Summary, DateTimeOffset Deadline, bool Listens);
