using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Conversation;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>
/// The background work of the memory (ADR 0019, sections 4 and 5): after a turn, the local model reads what the
/// user said and draws zero to three facts from it; when exchanges leave the history, it melts them into the
/// summary.
/// <list type="bullet">
/// <item>The local model only, never a cloud provider.</item>
/// <item>One request at a time, and only while the assistant is idle: an activation cancels the request in
/// progress, which is taken up again later, once at most. The conversation always goes first.</item>
/// <item>A failure gives neither a fault nor a message: it goes to the technical log, without the text.</item>
/// <item>The extraction never sees anything but the words of the user (section 3).</item>
/// </list>
/// </summary>
public sealed partial class MemoryWorker :
    IEventHandler<TurnRecorded>,
    IEventHandler<ExchangesLeftHistory>,
    IEventHandler<AssistantStateChanged>,
    IDisposable
{
    /// <summary>Most facts drawn from one turn.</summary>
    public const int MostFactsPerTurn = 3;

    /// <summary>Most exchanges melted into the summary by one request; the others wait for the next turn.</summary>
    public const int MostExchangesPerSummary = 8;

    /// <summary>Most characters of a request, and of an answer, given to the summary.</summary>
    public const int MostCharactersPerSide = 500;

    /// <summary>Turns waiting for their extraction; past it, the oldest is given up.</summary>
    public const int MostWaiting = 20;

    private readonly IAssistantModel model;
    private readonly MemoryBook book;
    private readonly ISettingsStore settings;
    private readonly IAssistantStateReader state;
    private readonly TimeProvider time;
    private readonly ILogger<MemoryWorker> logger;
    private readonly Lock gate = new();
    private readonly LinkedList<Extraction> waiting = new();
    private ExchangesLeftHistory? left;
    private bool leftRetried;
    private CancellationTokenSource? running;
    private bool disposed;

    /// <summary>Creates the worker.</summary>
    /// <param name="model">The default model; used only when its provider is local.</param>
    /// <param name="book">What is kept.</param>
    /// <param name="settings">The settings, for <see cref="MemoryOptions.Section"/>.</param>
    /// <param name="state">The state of the assistant: the work runs only while it is idle.</param>
    /// <param name="time">The clock of the time limit.</param>
    /// <param name="logger">Logs the failures, never a text (ADR 0004, section 6).</param>
    public MemoryWorker(IAssistantModel model, MemoryBook book, ISettingsStore settings, IAssistantStateReader state, TimeProvider time, ILogger<MemoryWorker> logger)
    {
        this.model = model;
        this.book = book;
        this.settings = settings;
        this.state = state;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Longest time one request to the model may take.</summary>
    internal TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>The work in progress or the last one done; for the tests.</summary>
    internal Task Work { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnRecorded message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var options = settings.Read(MemoryOptions.Section);
        if (options.Enabled && options.Automatic && !string.IsNullOrWhiteSpace(message.Request))
        {
            lock (gate)
            {
                waiting.AddLast(new Extraction(message.Key, message.Request, message.Language));
                if (waiting.Count > MostWaiting)
                {
                    waiting.RemoveFirst();
                }
            }
        }

        Pump();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ExchangesLeftHistory message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (settings.Read(MemoryOptions.Section).Enabled)
        {
            lock (gate)
            {
                // The newest announcement holds every exchange of the older one that still counts.
                left = message;
                leftRetried = false;
            }
        }

        // No pump here: a turn is starting. The summary is written once it has ended.
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(AssistantStateChanged message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Current.Phase == AssistantPhase.Idle)
        {
            Pump();
        }
        else
        {
            // An activation: the conversation always goes first (ADR 0019, section 4).
            lock (gate)
            {
                running?.Cancel();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            running?.Cancel();
        }
    }

    /// <summary>The sentences of an answer of the model: a JSON array of texts, whatever is written around it. Anything else is nothing to keep.</summary>
    /// <param name="answer">What the model wrote.</param>
    /// <returns>Three sentences at most.</returns>
    internal static IReadOnlyList<string> Sentences(string? answer)
    {
        var text = Thinking().Replace(answer ?? string.Empty, string.Empty);
        var start = text.IndexOf('[', StringComparison.Ordinal);
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(text[start..(end + 1)]);
            return document.RootElement.ValueKind != JsonValueKind.Array
                ? []
                : [.. document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    .Select(item => item.GetString()!)
                    .Take(MostFactsPerTurn)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>What the model is given to write the new summary: the former one, then the exchanges, each side cut.</summary>
    internal static string SummaryRequest(AssistantLanguage language, string? former, IReadOnlyList<PastExchange> exchanges)
    {
        var text = new StringBuilder();
        text.Append(MemoryRecaller.Text("Summary_Former", language)).Append('\n')
            .Append(string.IsNullOrWhiteSpace(former) ? MemoryRecaller.Text("Summary_None", language) : former.Trim()).Append("\n\n")
            .Append(MemoryRecaller.Text("Summary_Exchanges", language)).Append('\n');
        foreach (var exchange in exchanges)
        {
            text.Append(MemoryRecaller.Text("Summary_User", language)).Append(' ').Append(Cut(exchange.Request)).Append('\n')
                .Append(MemoryRecaller.Text("Summary_Jarvis", language)).Append(' ').Append(Cut(exchange.Answer)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>A summary of the right size: cut at the end of a sentence when it is too long.</summary>
    internal static string Fit(string? summary)
    {
        var text = Thinking().Replace(summary ?? string.Empty, string.Empty).Trim();
        if (text.Length <= MemorySummary.MostCharacters)
        {
            return text;
        }

        text = text[..MemorySummary.MostCharacters];
        var end = text.LastIndexOfAny(['.', '!', '?']);
        return end > MemorySummary.MostCharacters / 2 ? text[..(end + 1)] : text;
    }

    private static string Cut(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MostCharactersPerSide ? line : line[..MostCharactersPerSide] + "…";
    }

    /// <summary>Starts the work when there is some, nothing runs and the assistant is idle.</summary>
    private void Pump()
    {
        lock (gate)
        {
            if (disposed || running is not null || state.Current.Phase != AssistantPhase.Idle || (waiting.Count == 0 && left is null))
            {
                return;
            }

            running = new CancellationTokenSource();
            var token = running.Token;
            Work = Task.Run(() => RunAsync(token), CancellationToken.None);
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        var emptied = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                Extraction? extraction;
                ExchangesLeftHistory? announced;
                lock (gate)
                {
                    extraction = waiting.First?.Value;
                    announced = extraction is null ? left : null;
                    if (extraction is not null)
                    {
                        waiting.RemoveFirst();
                    }
                    else if (announced is null)
                    {
                        emptied = true;
                        return;
                    }
                }

                if (model.Client is not { } client || model.Provider?.Kind.IsLocal() != true)
                {
                    // Never a cloud provider (ADR 0019, section 4): what waits is given up.
                    lock (gate)
                    {
                        waiting.Clear();
                        left = null;
                    }

                    return;
                }

                using var limit = new CancellationTokenSource(RequestTimeout, time);
                using var both = CancellationTokenSource.CreateLinkedTokenSource(token, limit.Token);
                try
                {
                    if (extraction is not null)
                    {
                        await ExtractAsync(client, extraction, both.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await SummarizeAsync(client, announced!, both.Token).ConfigureAwait(false);
                        lock (gate)
                        {
                            left = ReferenceEquals(left, announced) ? null : left;
                        }
                    }
                }
                catch (Exception exception)
                {
                    // Cancelled by an activation, too slow or failed: taken up again later, once at most.
                    if (exception is not OperationCanceledException)
                    {
                        LogFailed(exception, extraction is not null ? "extraction" : "summary");
                    }

                    lock (gate)
                    {
                        if (extraction is { Retried: false })
                        {
                            waiting.AddFirst(extraction with { Retried = true });
                        }
                        else if (announced is not null && ReferenceEquals(left, announced))
                        {
                            left = leftRetried ? null : left;
                            leftRetried = true;
                        }
                    }

                    return;
                }
            }
        }
        finally
        {
            lock (gate)
            {
                running?.Dispose();
                running = null;
            }

            if (emptied)
            {
                // What arrived between the last look and now would otherwise wait for the next turn.
                Pump();
            }
        }
    }

    private async Task ExtractAsync(IChatClient client, Extraction extraction, CancellationToken token)
    {
        // Only the words of the user: no answer, no tool result, no history (ADR 0019, section 3).
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, extraction.Request)],
            new ChatOptions { Instructions = MemoryPrompts.Extraction(extraction.Language), MaxOutputTokens = 300 },
            token).ConfigureAwait(false);
        var kept = 0;
        foreach (var sentence in Sentences(response.Text))
        {
            kept += await book.AddAsync(sentence, FactSource.Auto, extraction.Turn, token).ConfigureAwait(false) == FactOutcome.Added ? 1 : 0;
        }

        LogExtracted(kept);
    }

    private async Task SummarizeAsync(IChatClient client, ExchangesLeftHistory announced, CancellationToken token)
    {
        var summary = await book.SummaryAsync(token).ConfigureAwait(false);
        var fresh = announced.Exchanges
            .Where(exchange => summary is null || exchange.At > summary.Through)
            .OrderBy(exchange => exchange.At)
            .Take(MostExchangesPerSummary)
            .ToList();
        if (fresh.Count == 0)
        {
            return;
        }

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, SummaryRequest(announced.Language, summary?.Text, fresh))],
            new ChatOptions { Instructions = MemoryPrompts.Summary(announced.Language), MaxOutputTokens = 600 },
            token).ConfigureAwait(false);
        var text = Fit(response.Text);
        if (text.Length == 0 || SecretShapes.LooksLikeSecret(text))
        {
            // Nothing usable: the former summary stays, and these exchanges are not asked again.
            text = summary?.Text ?? string.Empty;
        }

        await book.WriteSummaryAsync(text, fresh[^1].At, token).ConfigureAwait(false);
        LogSummarized(fresh.Count);
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Thinking();

    [LoggerMessage(Level = LogLevel.Information, Message = "Memory: {Count} fact(s) kept from the last turn.")]
    private partial void LogExtracted(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Memory: {Count} exchange(s) melted into the summary.")]
    private partial void LogSummarized(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memory: the {Work} failed; it is tried once more later.")]
    private partial void LogFailed(Exception exception, string work);

    /// <summary>A turn whose words wait to be read.</summary>
    private sealed record Extraction(Guid Turn, string Request, AssistantLanguage Language)
    {
        public bool Retried { get; init; }
    }
}
