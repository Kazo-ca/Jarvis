using System.Globalization;
using System.Resources;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Permissions.Confirmation;

/// <summary>
/// The confirmation requests (ADR 0023): one open at a time, the others wait their turn. A request sets the
/// condition "interaction required", opens the confirmation window, has Jarvis say the question (spoken turn) or
/// announce that an interaction is required (typed turn), then waits for a click, a recognized spoken answer or an
/// interruption. It waits as long as it takes: Jarvis never decides in place of the user, so there is no timeout.
/// Only a human answer through a channel the class accepts approves.
/// </summary>
public sealed partial class ConfirmationDesk : IConfirmation, IOpenConfirmations, IEventHandler<ConfirmationAnswerHeard>, IDisposable
{
    /// <summary>Key of the confirmation window: one window at a time.</summary>
    public const string DialogKey = "Permissions.Confirmation";

    private static readonly ResourceManager Texts = new(typeof(ConfirmationWindow));

    private readonly IMessageBus bus;
    private readonly IDialogService dialogs;
    private readonly IAssistantStateReader state;
    private readonly IAssistantLanguageReader language;
    private readonly ILogger<ConfirmationDesk> logger;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private readonly Lock gate = new();
    private Pending? current;

    /// <summary>Creates the desk.</summary>
    /// <param name="bus">Where the condition and the question go.</param>
    /// <param name="dialogs">Opens the confirmation window.</param>
    /// <param name="state">The assistant state: a muted microphone hears no answer.</param>
    /// <param name="language">The language of the question and of the recognized words.</param>
    /// <param name="logger">Logs each request without its arguments (ADR 0004, section 6).</param>
    public ConfirmationDesk(
        IMessageBus bus,
        IDialogService dialogs,
        IAssistantStateReader state,
        IAssistantLanguageReader language,
        ILogger<ConfirmationDesk> logger)
    {
        this.bus = bus;
        this.dialogs = dialogs;
        this.state = state;
        this.language = language;
        this.logger = logger;
        dialogs.Closed += OnDialogClosed;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
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
        var inTurn = request.Context.Turn != TurnId.None;

        // ADR 0023, section 3: a spoken answer only for "Sensitive", in a spoken turn, the microphone not muted.
        var listen = inTurn && request.Context.Spoken && request.Class == ActionClass.Sensitive && !state.Current.MicrophoneMuted;
        var summary = string.IsNullOrWhiteSpace(request.Summary) ? Text("GeneralSummary", spoken, request.Tool) : request.Summary.Trim();
        var pending = new Pending(new OpenConfirmation(Guid.NewGuid(), request, summary, listen), spoken);
        lock (gate)
        {
            current = pending;
        }

        var id = pending.View.Id;
        LogOpened(id, request.Tool, request.Class, inTurn, listen);
        try
        {
            await bus.PublishAsync(new InteractionOpened(id, InteractionKind.Confirmation), CancellationToken.None).ConfigureAwait(false);
            dialogs.Open(new DialogRequest(DialogKey, typeof(ConfirmationWindow), Text("WindowTitle", spoken)));
            Changed?.Invoke();
            if (inTurn)
            {
                // After a spoken request: the summary and the question. After a typed one: only the announcement
                // that an interaction is required; the window says the rest (ADR 0023, section 2).
                var question = request.Context.Spoken
                    ? summary + " " + Text(
                        request.Class == ActionClass.Critical ? "Question_Critical" : listen ? "Question_Voice" : "Question_Click",
                        spoken)
                    : Text("Announcement", spoken);
                await bus.PublishAsync(new ConfirmationQuestionToSay(id, request.Context.Turn, question, listen), CancellationToken.None).ConfigureAwait(false);
            }

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
            await bus.PublishAsync(new InteractionClosed(id), CancellationToken.None).ConfigureAwait(false);
            oneAtATime.Release();
        }
    }

    /// <inheritdoc />
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
            // Nothing heard: the request stays open, the click remains possible.
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
        dialogs.Closed -= OnDialogClosed;
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

    /// <summary>
    /// The window closed while its request is open: the user closed it, and that is a refusal. When the desk closes
    /// the window itself, the request is already over and nothing is open here.
    /// </summary>
    private void OnDialogClosed(string key)
    {
        if (!string.Equals(key, DialogKey, StringComparison.Ordinal))
        {
            return;
        }

        Pending? pending;
        lock (gate)
        {
            pending = current;
        }

        pending?.Answer.TrySetResult(new ConfirmationOutcome(ConfirmationAnswer.Refused, ConfirmationChannels.Click));
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation {Id} opened for {Tool} (class {Class}); said aloud: {Said}, spoken answer accepted: {Listens}.")]
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
