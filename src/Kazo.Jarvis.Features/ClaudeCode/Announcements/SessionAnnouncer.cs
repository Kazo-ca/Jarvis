using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.ClaudeCode.Announcements;

/// <summary>What happened to a session that is worth telling.</summary>
public enum SessionNews
{
    /// <summary>The session waits for a permission.</summary>
    WaitsApproval,

    /// <summary>The session asked the user a question.</summary>
    WaitsAnswer,

    /// <summary>The session ended its work, or its turn.</summary>
    Ended,

    /// <summary>The session cannot go on.</summary>
    Blocked,
}

/// <summary>
/// Announces the Claude Code sessions (US2.12; ADR 0021, section 9): when one ends or waits for the user, Jarvis
/// says so. It watches the board of the tasks, so a session Jarvis started and one seen through its hooks are
/// announced the same way. The choice of the folder decides whether an event is announced; the quiet time decides
/// whether it is said or only written (ADR 0024). The last message of a session is a piece of data: it is cut, or
/// shortened by the local model which is told to follow nothing in it, and never executed.
/// </summary>
public sealed partial class SessionAnnouncer : IHostedService, IAsyncDisposable
{
    /// <summary>A last message this short is said as it is.</summary>
    public const int ShortEnough = 160;

    /// <summary>The longest summary said.</summary>
    public const int MostSaid = 300;

    /// <summary>The longest summary written when nothing is said.</summary>
    public const int MostWritten = 500;

    private static readonly ResourceManager Texts = new(typeof(ClaudeAnnouncementsTexts));
    private static readonly TimeSpan ShortenLimit = TimeSpan.FromSeconds(20);

    private readonly ITaskBoard board;
    private readonly ISettingsStore settings;
    private readonly QuietTime quiet;
    private readonly IAssistantLanguageReader language;
    private readonly IMessageBus bus;
    private readonly IAssistantModel? model;
    private readonly ILogger logger;
    private readonly Lock gate = new();
    private readonly Dictionary<string, WorkTaskStatus> known = new(StringComparer.Ordinal);
    private readonly Channel<(WorkTask Task, SessionNews News)> news = Channel.CreateUnbounded<(WorkTask, SessionNews)>();
    private Task? telling;
    private bool watching;

    /// <summary>Creates the announcer.</summary>
    /// <param name="board">The board of the tasks, watched.</param>
    /// <param name="settings">The settings, for the choice of each folder.</param>
    /// <param name="quiet">When nothing is said.</param>
    /// <param name="language">The language of the assistant, in which the announcements are written.</param>
    /// <param name="bus">The bus the announcements leave on.</param>
    /// <param name="model">The local model, to shorten a long last message when it is already in memory.</param>
    /// <param name="logger">The log. The text of a session is never written to it.</param>
    public SessionAnnouncer(
        ITaskBoard board,
        ISettingsStore settings,
        QuietTime quiet,
        IAssistantLanguageReader language,
        IMessageBus bus,
        IAssistantModel? model = null,
        ILogger<SessionAnnouncer>? logger = null)
    {
        this.board = board;
        this.settings = settings;
        this.quiet = quiet;
        this.language = language;
        this.bus = bus;
        this.model = model;
        this.logger = logger ?? NullLogger<SessionAnnouncer>.Instance;
    }

    /// <summary>The announcements being prepared; for the tests.</summary>
    internal Task Telling => telling ?? Task.CompletedTask;

    /// <summary>
    /// What a change of status is worth (ADR 0021, section 9). A wait is told each time it starts. An end is told
    /// once: when a session that worked becomes idle or done. A session stopped by the user is not announced to
    /// them, and a task that never started was already answered in its turn.
    /// </summary>
    /// <param name="previous">The status before, or <see langword="null"/> for a task just learned.</param>
    /// <param name="current">The status now.</param>
    /// <returns>What to tell, or <see langword="null"/>.</returns>
    public static SessionNews? NewsOf(WorkTaskStatus? previous, WorkTaskStatus current)
    {
        if (previous == current)
        {
            return null;
        }

        var worked = previous is WorkTaskStatus.Working or WorkTaskStatus.WaitingApproval or WorkTaskStatus.WaitingAnswer;
        return current switch
        {
            WorkTaskStatus.WaitingApproval => SessionNews.WaitsApproval,
            WorkTaskStatus.WaitingAnswer => SessionNews.WaitsAnswer,
            WorkTaskStatus.Idle or WorkTaskStatus.Done when worked => SessionNews.Ended,
            WorkTaskStatus.Blocked when worked || previous is WorkTaskStatus.Idle or WorkTaskStatus.Paused => SessionNews.Blocked,
            _ => null,
        };
    }

    /// <summary>The name said for the folder of <paramref name="task"/>: its last part, or the name of its project.</summary>
    /// <param name="task">A task.</param>
    /// <returns>The name.</returns>
    public static string NameOf(WorkTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        var folder = task.Folder?.TrimEnd('/', '\\');
        if (!string.IsNullOrEmpty(folder))
        {
            var cut = folder.LastIndexOfAny(['/', '\\']);
            return cut >= 0 && cut < folder.Length - 1 ? folder[(cut + 1)..] : folder;
        }

        return TaskKeys.ProjectName(task.Project) is { Length: > 0 } project ? project : task.Title;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (watching)
            {
                return Task.CompletedTask;
            }

            // What is on the board at start is not news.
            foreach (var task in Sessions())
            {
                known[task.Id] = task.Status;
            }

            watching = true;
            telling = Task.Run(TellAsync, CancellationToken.None);
        }

        board.Changed += OnChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        board.Changed -= OnChanged;
        news.Writer.TryComplete();
        if (telling is { } work)
        {
            await work.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    /// <summary>The sentence of an announcement.</summary>
    /// <param name="happened">What happened.</param>
    /// <param name="name">The name of the folder.</param>
    /// <param name="summary">What the session said last, already shortened, or <see langword="null"/>.</param>
    /// <param name="language">The language.</param>
    /// <returns>The sentence.</returns>
    public static string Sentence(SessionNews happened, string name, string? summary, AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var key = happened switch
        {
            SessionNews.WaitsApproval => "WaitsApproval",
            SessionNews.WaitsAnswer => "WaitsAnswer",
            SessionNews.Blocked => summary is null ? "Blocked" : "BlockedBecause",
            _ => summary is null ? "Ended" : "EndedWith",
        };
        return string.Format(CultureInfo.InvariantCulture, Text(key, language), name, summary);
    }

    internal static string Text(string key, AssistantLanguage language) => Texts.GetString(key, language.Culture) ?? key;

    /// <summary><paramref name="text"/> on one line, cut at the end of a sentence when one ends before <paramref name="most"/>.</summary>
    internal static string Cut(string text, int most)
    {
        var line = Spaces().Replace(text, " ").Trim();
        if (line.Length <= most)
        {
            return line;
        }

        var head = line[..most];
        var end = head.LastIndexOfAny(['.', '!', '?']);
        return end >= most / 3 ? head[..(end + 1)] : head.TrimEnd() + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private IEnumerable<WorkTask> Sessions() =>
        board.Tasks.Where(task => task.ParentId is null && task.Operator.Account == TaskOperator.ClaudeCode);

    private void OnChanged()
    {
        lock (gate)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var task in Sessions())
            {
                seen.Add(task.Id);
                WorkTaskStatus? previous = known.TryGetValue(task.Id, out var before) ? before : null;
                known[task.Id] = task.Status;
                if (NewsOf(previous, task.Status) is { } happened)
                {
                    news.Writer.TryWrite((task, happened));
                }
            }

            foreach (var gone in known.Keys.Where(id => !seen.Contains(id)).ToList())
            {
                known.Remove(gone);
            }
        }
    }

    private async Task TellAsync()
    {
        await foreach (var (task, happened) in news.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await TellAsync(task, happened).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One announcement lost is not a reason to lose the next ones.
                LogFailed(exception, task.Id);
            }
        }
    }

    private async Task TellAsync(WorkTask task, SessionNews happened)
    {
        var level = settings.Read(ClaudeAnnouncementsOptions.Section).LevelOf(task.Folder);
        var waits = happened is SessionNews.WaitsApproval or SessionNews.WaitsAnswer;
        if (level == AnnouncementLevel.None || (level == AnnouncementLevel.WaitsOnly && !waits))
        {
            return;
        }

        var aloud = !quiet.IsQuiet;
        var spoken = language.Current;
        string? summary = null;
        if (!waits && !string.IsNullOrWhiteSpace(task.Summary))
        {
            // The cause of a block is written by Jarvis or by the hook: it is cut, never reworded.
            summary = happened == SessionNews.Ended && aloud
                ? await ShortenAsync(task.Summary, spoken).ConfigureAwait(false)
                : Cut(task.Summary, aloud ? MostSaid : MostWritten);
        }

        LogTold(task.Id, happened, aloud);
        await bus.PublishAsync(new AnnouncementToSay(Sentence(happened, NameOf(task), summary, spoken), spoken, aloud), CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// The last message of a session, short enough to be said. A long one is shortened by the local model when it
    /// is already in memory: an announcement is not worth loading it. The message is given as a content, with the
    /// instruction to follow nothing in it; without a model, or when it fails, the message is cut.
    /// </summary>
    private async Task<string> ShortenAsync(string message, AssistantLanguage spoken)
    {
        var line = Cut(message, int.MaxValue);
        if (line.Length <= ShortEnough || model is not { LoadState: ModelLoadState.Loaded, Client: { } client })
        {
            return Cut(line, MostSaid);
        }

        try
        {
            using var limit = new CancellationTokenSource(ShortenLimit);
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, line)],
                new ChatOptions { Instructions = Text("Shorten_Instructions", spoken) },
                limit.Token).ConfigureAwait(false);
            return response.Text?.Trim() is { Length: > 0 } shorter ? Cut(shorter, MostSaid) : Cut(line, MostSaid);
        }
        catch (Exception exception)
        {
            LogShortenFailed(exception);
            return Cut(line, MostSaid);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Task {Task}: {News} announced (aloud: {Aloud}).")]
    private partial void LogTold(string task, SessionNews news, bool aloud);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {Task}: its announcement could not be made.")]
    private partial void LogFailed(Exception exception, string task);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The local model could not shorten the last message of a session; it is cut instead.")]
    private partial void LogShortenFailed(Exception exception);
}

/// <summary>Marker of the resources <c>ClaudeAnnouncementsTexts.resx</c> and <c>ClaudeAnnouncementsTexts.fr.resx</c>.</summary>
public sealed class ClaudeAnnouncementsTexts
{
    private ClaudeAnnouncementsTexts()
    {
    }
}
