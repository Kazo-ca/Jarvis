using System.Globalization;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Llm.Spend;

/// <summary>
/// The count of the month and the cap (US2.8, ADR 0020, section 6). Each answer of a cloud provider is priced
/// from its tokens and added to the count of the calendar month; the cap is checked by code before each call,
/// never by a model. The count is an estimate: the bill of the provider is what counts.
/// </summary>
public sealed partial class SpendLedger : ISpendCap, IEventHandler<ProviderAnswered>, IHostedService, IDisposable
{
    /// <summary>The collection of the counted answers.</summary>
    public const string EntriesCollection = "Llm.SpendEntries";

    // How often the month is looked at: the first hour of a new month gives the cloud back.
    private static readonly TimeSpan MonthWatch = TimeSpan.FromMinutes(10);

    private readonly ISettingsStore settings;
    private readonly IDocumentStore store;
    private readonly TimeProvider time;
    private readonly ILogger<SpendLedger> logger;
    private readonly IDisposable watch;
    private readonly Lock gate = new();
    private SpendCount count;
    private SpendNotice? notice;
    private ITimer? timer;
    private string seenMonth;

    /// <summary>Creates the ledger and reads the count kept.</summary>
    /// <param name="settings">The settings, for <see cref="SpendOptions.Section"/> and <see cref="SpendCount.Section"/>.</param>
    /// <param name="store">The store of the documents, for the counted answers.</param>
    /// <param name="time">The clock: the month is the local calendar month.</param>
    /// <param name="logger">The log.</param>
    public SpendLedger(ISettingsStore settings, IDocumentStore store, TimeProvider time, ILogger<SpendLedger> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        this.settings = settings;
        this.store = store;
        this.time = time;
        this.logger = logger;
        count = settings.Read(SpendCount.Section);
        seenMonth = MonthKey();
        watch = settings.Watch(SpendOptions.Section, _ => Changed?.Invoke());
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public SpendStatus Status
    {
        get
        {
            var now = time.GetLocalNow();
            var month = new DateOnly(now.Year, now.Month, 1);
            var cap = settings.Read(SpendOptions.Section).MonthlyCapUsd;
            lock (gate)
            {
                // A count of another month is not the count of this one: the cap starts again on the first day.
                return new SpendStatus(month, count.Month == MonthKey(now) ? count.SpentUsd : 0m, cap);
            }
        }
    }

    /// <inheritdoc />
    public SpendVerdict Check(LlmProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider.Kind.IsLocal())
        {
            return SpendVerdict.Allowed;
        }

        if (settings.Read(SpendOptions.Section).PriceOf(provider.Id) is null)
        {
            return SpendVerdict.NoPrice;
        }

        return Status.Reached ? SpendVerdict.CapReached : SpendVerdict.Allowed;
    }

    /// <inheritdoc />
    public SpendNotice? TakeNotice()
    {
        lock (gate)
        {
            var taken = notice;
            notice = null;
            return taken;
        }
    }

    /// <summary>The counted answers of the current month, the newest first.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The entries.</returns>
    public async ValueTask<IReadOnlyList<SpendEntry>> ReadMonthAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetLocalNow();
        var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        var stored = await Entries().ReadPeriodAsync(from, from.AddMonths(1), cancellationToken).ConfigureAwait(false);
        return [.. stored.Select(document => document.Content).OrderByDescending(entry => entry.At)];
    }

    /// <summary>Counts one answer of a cloud provider. An answer without tokens counts for nothing: it is logged.</summary>
    /// <param name="message">The answer.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>A task that ends once the count is kept.</returns>
    public async ValueTask HandleAsync(ProviderAnswered message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var provider = message.Provider;
        if (provider.Kind.IsLocal())
        {
            return;
        }

        var options = settings.Read(SpendOptions.Section);
        if (options.PriceOf(provider.Id) is not { } price)
        {
            // Check refuses such a call; an answer that still arrives is told, not hidden.
            LogUnpriced(provider.Id);
            return;
        }

        if (message.InputTokens is null && message.OutputTokens is null)
        {
            LogNoTokens(provider.Id);
            return;
        }

        var cost = price.Cost(message.InputTokens ?? 0, message.OutputTokens ?? 0);
        var month = MonthKey();
        SpendCount kept;
        lock (gate)
        {
            var before = count.Month == month ? count.SpentUsd : 0m;
            var after = before + cost;
            count = kept = new SpendCount { Month = month, SpentUsd = after };
            var cap = options.MonthlyCapUsd;
            if (before < cap && after >= cap)
            {
                notice = SpendNotice.CapReached;
            }
            else if (before < cap * SpendStatus.WarningShare && after >= cap * SpendStatus.WarningShare && after < cap)
            {
                notice = SpendNotice.NearCap;
            }
        }

        LogCounted(provider.Id, cost, kept.SpentUsd);
        var written = await settings.WriteAsync(SpendCount.Section, kept, cancellationToken).ConfigureAwait(false);
        if (written.Outcome != SettingsWriteOutcome.Saved)
        {
            // The count stays right while Jarvis runs; it is said because it would be lost at the next start.
            LogCountNotKept(written.Outcome.ToString());
        }

        try
        {
            await Entries().AddAsync(
                new SpendEntry
                {
                    At = message.At,
                    ProviderId = provider.Id,
                    ProviderName = provider.Name,
                    Model = message.Model ?? provider.Model,
                    InputTokens = message.InputTokens ?? 0,
                    OutputTokens = message.OutputTokens ?? 0,
                    CostUsd = cost,
                },
                message.At,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The detail is lost, not the count.
            LogEntryNotKept(exception);
        }

        Changed?.Invoke();
    }

    /// <summary>Puts the count of the month back to zero and erases its detail. For the user, from the page.</summary>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>A task that ends once it is done.</returns>
    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        SpendCount kept;
        lock (gate)
        {
            count = kept = new SpendCount();
            notice = null;
        }

        await settings.WriteAsync(SpendCount.Section, kept, cancellationToken).ConfigureAwait(false);
        await Entries().ClearAsync(cancellationToken).ConfigureAwait(false);
        LogReset();
        Changed?.Invoke();
    }

    /// <summary>Starts looking at the month, so that a cap reached is lifted when the month changes.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        timer ??= time.CreateTimer(_ => WatchMonth(), null, MonthWatch, MonthWatch);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        timer?.Dispose();
        timer = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        timer?.Dispose();
        watch.Dispose();
    }

    private void WatchMonth()
    {
        var month = MonthKey();
        bool changed;
        lock (gate)
        {
            changed = month != seenMonth;
            seenMonth = month;
        }

        if (changed)
        {
            LogNewMonth(month);
            Changed?.Invoke();
        }
    }

    private string MonthKey() => MonthKey(time.GetLocalNow());

    private static string MonthKey(DateTimeOffset now) => now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private IDocumentCollection<SpendEntry> Entries() => store.Collection<SpendEntry>(EntriesCollection);

    [LoggerMessage(Level = LogLevel.Information, Message = "Answer of {Provider} counted: {Cost} USD, {Spent} USD this month.")]
    private partial void LogCounted(string provider, decimal cost, decimal spent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An answer of {Provider} arrived while it has no price: it is not counted.")]
    private partial void LogUnpriced(string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An answer of {Provider} gave no token count: it is not counted.")]
    private partial void LogNoTokens(string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The count of the month could not be kept ({Outcome}).")]
    private partial void LogCountNotKept(string outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A counted answer could not be kept in the store.")]
    private partial void LogEntryNotKept(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "The count of the month was put back to zero by the user.")]
    private partial void LogReset();

    [LoggerMessage(Level = LogLevel.Information, Message = "New month {Month}: the count starts again.")]
    private partial void LogNewMonth(string month);
}
