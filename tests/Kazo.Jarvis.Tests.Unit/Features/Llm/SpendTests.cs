using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.Llm.Spend;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>
/// The spending slice (US2.8, ADR 0020, section 6): a cap of 10 US dollars by default, a cost counted from the
/// tokens and the price typed, a cloud provider without a price never called, the warning at 80 %, the cap that
/// stops the calls until the month changes or the user raises it, and a count that survives a restart.
/// </summary>
[TestFixture]
public sealed class SpendTests
{
    private static readonly LlmProvider Local = new("local", "Ollama", LlmProviderKind.Ollama, new Uri("http://127.0.0.1:11435"), "qwen3:14b", new LlmTuning());
    private static readonly LlmProvider Cloud = new("anthropic", "Anthropic", LlmProviderKind.Anthropic, new Uri("https://api.anthropic.com"), "claude-opus-5-5", new LlmTuning());
    private static readonly LlmProvider Other = new("openai", "OpenAI", LlmProviderKind.OpenAi, new Uri("https://api.openai.com"), "gpt-5", new LlmTuning());

    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;
    private JsonFileSettingsStore settings = null!;
    private SqliteDocumentStore store = null!;
    private SpendLedger ledger = null!;
    private int changes;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        settings = new JsonFileSettingsStore(folder.Paths, [], time);
        store = new SqliteDocumentStore(folder.Paths, time);

        // 4 USD per million tokens sent, 20 per million written: a million of each costs 24 USD.
        await settings.WriteAsync(SpendOptions.Section, new SpendOptions().WithPrice(Cloud.Id, new ProviderPrice(4m, 20m)));
        ledger = Ledger();
    }

    [TearDown]
    public void TearDown()
    {
        ledger.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    [Test]
    public void The_cap_is_ten_dollars_before_the_user_chose()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SpendOptions().MonthlyCapUsd, Is.EqualTo(10m));
            Assert.That(ledger.Status, Is.EqualTo(new SpendStatus(new DateOnly(2026, 11, 1), 0m, 10m)));
            Assert.That(ledger.Status.Reached, Is.False);
        });
    }

    [Test]
    public void A_local_provider_is_always_allowed_and_a_cloud_one_needs_a_price()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ledger.Check(Local), Is.EqualTo(SpendVerdict.Allowed));
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.Allowed));
            Assert.That(ledger.Check(Other), Is.EqualTo(SpendVerdict.NoPrice), "Its cost could not be counted: it is not called.");
        });
    }

    [Test]
    public async Task An_answer_is_counted_from_its_tokens_and_the_price()
    {
        await AnswerAsync(Cloud, 100_000, 10_000);

        var entry = (await ledger.ReadMonthAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(ledger.Status.SpentUsd, Is.EqualTo(0.6m), "0.4 for the tokens sent, 0.2 for those written.");
            Assert.That(settings.Read(SpendCount.Section), Is.EqualTo(new SpendCount { Month = "2026-11", SpentUsd = 0.6m }));
            Assert.That(entry.ProviderId, Is.EqualTo("anthropic"));
            Assert.That(entry.Model, Is.EqualTo("claude-opus-5-5"));
            Assert.That((entry.InputTokens, entry.OutputTokens, entry.CostUsd), Is.EqualTo((100_000L, 10_000L, 0.6m)));
            Assert.That(changes, Is.GreaterThan(0), "Who shows the count is told.");
            Assert.That(ledger.TakeNotice(), Is.Null);
        });
    }

    [Test]
    public async Task What_is_not_a_priced_cloud_answer_is_not_counted()
    {
        await AnswerAsync(Local, 1_000_000, 1_000_000);
        await AnswerAsync(Other, 1_000_000, 1_000_000);
        await ledger.HandleAsync(new ProviderAnswered(Cloud, null, null, null, time.GetUtcNow()), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(ledger.Status.SpentUsd, Is.Zero);
            Assert.That(await ledger.ReadMonthAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task The_warning_then_the_cap_are_each_told_once_and_the_cap_stops_the_calls()
    {
        await AnswerAsync(Cloud, 1_000_000, 100_000);
        Assert.That(ledger.TakeNotice(), Is.Null, "6 USD: under 80 %.");

        await AnswerAsync(Cloud, 500_000, 25_000);
        Assert.Multiple(() =>
        {
            Assert.That(ledger.Status.SpentUsd, Is.EqualTo(8.5m));
            Assert.That(ledger.TakeNotice(), Is.EqualTo(SpendNotice.NearCap));
            Assert.That(ledger.TakeNotice(), Is.Null, "Taking it removes it.");
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.Allowed));
        });

        await AnswerAsync(Cloud, 100_000, 0);
        Assert.That(ledger.TakeNotice(), Is.Null, "Told once in the month.");

        await AnswerAsync(Cloud, 0, 100_000);
        Assert.Multiple(() =>
        {
            Assert.That(ledger.Status.SpentUsd, Is.EqualTo(10.9m), "The overrun is bounded by one answer.");
            Assert.That(ledger.Status.Reached, Is.True);
            Assert.That(ledger.TakeNotice(), Is.EqualTo(SpendNotice.CapReached));
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.CapReached));
            Assert.That(ledger.Check(Local), Is.EqualTo(SpendVerdict.Allowed), "Jarvis stays local.");
        });
    }

    [Test]
    public async Task One_answer_that_jumps_over_the_cap_tells_the_cap_only()
    {
        await AnswerAsync(Cloud, 0, 600_000);

        Assert.Multiple(() =>
        {
            Assert.That(ledger.TakeNotice(), Is.EqualTo(SpendNotice.CapReached));
            Assert.That(ledger.TakeNotice(), Is.Null);
        });
    }

    [Test]
    public async Task Raising_the_cap_gives_the_cloud_back()
    {
        await AnswerAsync(Cloud, 0, 600_000);
        changes = 0;

        await settings.WriteAsync(SpendOptions.Section, settings.Read(SpendOptions.Section) with { MonthlyCapUsd = 50m });

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.Allowed));
            Assert.That(ledger.Status, Is.EqualTo(new SpendStatus(new DateOnly(2026, 11, 1), 12m, 50m)));
            Assert.That(changes, Is.GreaterThan(0), "The hand-off puts its tool back.");
        });
    }

    [Test]
    public async Task A_cap_of_zero_allows_no_cloud_call()
    {
        await settings.WriteAsync(SpendOptions.Section, settings.Read(SpendOptions.Section) with { MonthlyCapUsd = 0m });

        Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.CapReached));
    }

    [Test]
    public async Task The_first_day_of_the_month_lifts_a_cap_reached()
    {
        await AnswerAsync(Cloud, 0, 600_000);
        await ledger.StartAsync(CancellationToken.None);
        changes = 0;

        time.Advance(TimeSpan.FromDays(11));

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Status, Is.EqualTo(new SpendStatus(new DateOnly(2026, 12, 1), 0m, 10m)));
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.Allowed));
            Assert.That(changes, Is.GreaterThan(0), "The month is watched: the tool comes back without a restart.");
        });

        await AnswerAsync(Cloud, 100_000, 10_000);
        Assert.That(settings.Read(SpendCount.Section), Is.EqualTo(new SpendCount { Month = "2026-12", SpentUsd = 0.6m }));
    }

    [Test]
    public async Task The_count_survives_a_restart_and_the_erasing_of_the_documents()
    {
        await AnswerAsync(Cloud, 0, 600_000);
        ledger.Dispose();

        // « Tout effacer » of the conversation window empties every collection, not the settings.
        await store.EraseAllAsync();
        ledger = Ledger();

        Assert.Multiple(async () =>
        {
            Assert.That(ledger.Status.SpentUsd, Is.EqualTo(12m));
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.CapReached), "Erasing the conversation does not lift the cap.");
            Assert.That(await ledger.ReadMonthAsync(), Is.Empty, "Only the detail went.");
        });
    }

    [Test]
    public async Task The_user_can_put_the_count_back_to_zero()
    {
        await AnswerAsync(Cloud, 0, 600_000);

        await ledger.ResetAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(ledger.Status.SpentUsd, Is.Zero);
            Assert.That(ledger.TakeNotice(), Is.Null);
            Assert.That(ledger.Check(Cloud), Is.EqualTo(SpendVerdict.Allowed));
            Assert.That(await ledger.ReadMonthAsync(), Is.Empty);
            Assert.That(settings.Read(SpendCount.Section).SpentUsd, Is.Zero);
        });
    }

    [Test]
    public async Task A_cap_or_a_price_out_of_bounds_is_refused()
    {
        var negative = await settings.WriteAsync(SpendOptions.Section, new SpendOptions { MonthlyCapUsd = -1m });
        var price = await settings.WriteAsync(SpendOptions.Section, new SpendOptions().WithPrice("anthropic", new ProviderPrice(-1m, 5m)));

        Assert.Multiple(() =>
        {
            Assert.That(negative.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(price.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(settings.Read(SpendOptions.Section).MonthlyCapUsd, Is.EqualTo(10m));
        });
    }

    [Test]
    public void Removing_a_price_leaves_the_others()
    {
        var options = new SpendOptions().WithPrice("a", new ProviderPrice(1m, 2m)).WithPrice("b", new ProviderPrice(3m, 4m)).WithPrice("a", null);

        Assert.Multiple(() =>
        {
            Assert.That(options.PriceOf("a"), Is.Null);
            Assert.That(options.PriceOf("b"), Is.EqualTo(new ProviderPrice(3m, 4m)));
        });
    }

    [TestCase("claude-opus-5-5", 4, 20)]
    [TestCase("claude-opus-5", 5, 25)]
    [TestCase("claude-fable-5-2", 8, 40)]
    [TestCase("claude-fable-5-1", 10, 50)]
    [TestCase("claude-sonnet-5-5", 2, 10)]
    [TestCase("claude-sonnet-4-6", 3, 15)]
    [TestCase("claude-haiku-4-5-20251001", 1, 5)]
    public void The_known_prices_are_proposed_by_the_start_of_the_model_id(string model, int input, int output) =>
        Assert.That(KnownPrices.For(Cloud with { Model = model }), Is.EqualTo(new ProviderPrice(input, output)));

    [Test]
    public void No_price_is_proposed_for_what_this_version_does_not_know() =>
        Assert.Multiple(() =>
        {
            Assert.That(KnownPrices.For(Cloud with { Model = "claude-unknown-9" }), Is.Null);
            Assert.That(KnownPrices.For(Other), Is.Null, "The user types it.");
            Assert.That(KnownPrices.For(Local), Is.Null);
        });

    private SpendLedger Ledger()
    {
        var created = new SpendLedger(settings, store, time, NullLogger<SpendLedger>.Instance);
        created.Changed += () => changes++;
        return created;
    }

    private ValueTask AnswerAsync(LlmProvider provider, long input, long output) =>
        ledger.HandleAsync(new ProviderAnswered(provider, null, input, output, time.GetUtcNow()), CancellationToken.None);
}
