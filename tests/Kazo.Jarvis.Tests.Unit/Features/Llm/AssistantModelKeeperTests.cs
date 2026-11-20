using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>The default model (US1.9, ADR 0007, section 5): preloaded at start, reloaded before a turn, follows the settings.</summary>
[TestFixture]
public sealed class AssistantModelKeeperTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider services = null!;
    private ISettingsStore settings = null!;
    private FakeConnector connector = null!;
    private FakeTimeProvider time = null!;
    private List<AssistantModelLoadChanged> announced = null!;
    private IDisposable subscription = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        services = new ServiceCollection().AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance).AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>)).AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        connector = new FakeConnector();
        time = new FakeTimeProvider();
        announced = [];
        var bus = services.GetRequiredService<IMessageBus>();
        subscription = bus.Subscribe<AssistantModelLoadChanged>((message, _) =>
        {
            lock (announced)
            {
                announced.Add(message);
            }

            return ValueTask.CompletedTask;
        });
    }

    [TearDown]
    public void TearDown()
    {
        subscription.Dispose();
        services.Dispose();
        folder.Dispose();
    }

    private AssistantModelKeeper Create() =>
        new(settings, connector, services.GetRequiredService<IMessageBus>(), time, NullLogger<AssistantModelKeeper>.Instance);

    private ModelLoadState[] States()
    {
        lock (announced)
        {
            return [.. announced.Select(a => a.State)];
        }
    }

    [Test]
    public async Task The_default_model_is_loaded_at_start_and_announced()
    {
        using var keeper = Create();
        Assert.That(keeper.LoadState, Is.EqualTo(ModelLoadState.Unknown));

        await keeper.StartAsync(CancellationToken.None);
        await WaitUntil(() => keeper.LoadState == ModelLoadState.Loaded);
        await keeper.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(connector.Preloaded.Single().Model, Is.EqualTo("qwen3:14b"));
            Assert.That(States(), Is.EqualTo(new[] { ModelLoadState.Loading, ModelLoadState.Loaded }));
            Assert.That(keeper.Client, Is.SameAs(connector.Clients.Single()));
        });
    }

    [Test]
    public async Task A_failed_load_is_tried_again_later()
    {
        connector.PreloadOutcomes.Enqueue(LlmFailure.Unreachable);
        using var keeper = Create();

        await keeper.StartAsync(CancellationToken.None);
        await WaitUntil(() => keeper.LoadState == ModelLoadState.Failed);
        Assert.That(announced.Last().Failure, Is.EqualTo(LlmFailure.Unreachable));

        await WaitUntil(() => time.GetUtcNow() > DateTimeOffset.MinValue && Advance(AssistantModelKeeper.RetryDelays[0]) && keeper.LoadState == ModelLoadState.Loaded);
        await keeper.StopAsync(CancellationToken.None);

        Assert.That(connector.Preloaded, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Before_a_turn_a_freed_model_is_loaded_again_and_the_conversation_is_told()
    {
        using var keeper = Create();

        await keeper.EnsureLoadedAsync();
        await keeper.EnsureLoadedAsync();
        Assert.That(connector.Preloaded, Has.Count.EqualTo(1), "Loaded once, then left alone.");

        connector.Loaded.Clear();
        await keeper.EnsureLoadedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(connector.Preloaded, Has.Count.EqualTo(2));
            Assert.That(States(), Is.EqualTo(new[] { ModelLoadState.Loading, ModelLoadState.Loaded, ModelLoadState.Loading, ModelLoadState.Loaded }));
        });
    }

    [Test]
    public async Task A_change_of_default_swaps_the_client_and_loads_the_new_model()
    {
        using var keeper = Create();
        await keeper.StartAsync(CancellationToken.None);
        await WaitUntil(() => keeper.LoadState == ModelLoadState.Loaded);
        var first = connector.Clients.Single();

        var options = settings.Read(LlmProvidersOptions.Section);
        await settings.WriteAsync(LlmProvidersOptions.Section, options with { Providers = [options.Providers[0] with { Model = "qwen2.5:14b" }] });
        await WaitUntil(() => connector.Preloaded.Count == 2 && keeper.LoadState == ModelLoadState.Loaded);
        await keeper.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.Disposed, Is.True);
            Assert.That(keeper.Provider?.Model, Is.EqualTo("qwen2.5:14b"));
            Assert.That(keeper.Client, Is.SameAs(connector.Clients[1]));
            Assert.That(connector.Preloaded[1].Model, Is.EqualTo("qwen2.5:14b"));
        });
    }

    [Test]
    public async Task Without_a_default_nothing_loads_and_a_turn_says_why()
    {
        await settings.WriteAsync(LlmProvidersOptions.Section, new LlmProvidersOptions { Providers = [], DefaultId = null });
        using var keeper = Create();

        await keeper.StartAsync(CancellationToken.None);
        var exception = Assert.ThrowsAsync<LlmException>(() => keeper.EnsureLoadedAsync());
        await keeper.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(keeper.LoadState, Is.EqualTo(ModelLoadState.NotConfigured));
            Assert.That(keeper.Client, Is.Null);
            Assert.That(exception.Failure, Is.EqualTo(LlmFailure.NotConfigured));
            Assert.That(connector.Preloaded, Is.Empty);
        });
    }

    private bool Advance(TimeSpan delay)
    {
        time.Advance(delay);
        return true;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition was not met in 5 s.");
            }

            await Task.Delay(10);
        }
    }
}
