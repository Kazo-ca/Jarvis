using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>The faults of the language model outside a turn (US1.17): raised from the loads, cleared by a successful one.</summary>
[TestFixture]
public sealed class LanguageModelFaultWatcherTests
{
    private ServiceProvider provider = null!;
    private IMessageBus bus = null!;
    private IAssistantStateReader state = null!;

    [SetUp]
    public void SetUp()
    {
        provider = new ServiceCollection()
            .AddJarvisKernel()
            .AddSingleton<IEventHandler<AssistantModelLoadChanged>, LanguageModelFaultWatcher>()
            .BuildServiceProvider();
        bus = provider.GetRequiredService<IMessageBus>();
        state = provider.GetRequiredService<IAssistantStateReader>();
    }

    [TearDown]
    public void TearDown() => provider.Dispose();

    [Test]
    public async Task One_failed_load_is_not_a_fault_two_in_a_row_are()
    {
        await LoadAsync(ModelLoadState.Failed, LlmFailure.Unreachable);
        Assert.That(state.Current.Fault, Is.Null, "Ollama may still be starting after the logon");

        await LoadAsync(ModelLoadState.Failed, LlmFailure.Unreachable);
        Assert.That(state.Current.Fault, Is.EqualTo(new AssistantFault(FaultCause.ModelUnreachable, "Unreachable", FaultOrigin.LanguageModel)));
    }

    [Test]
    public async Task A_missing_model_is_a_fault_at_once()
    {
        await LoadAsync(ModelLoadState.Failed, LlmFailure.ModelMissing);

        Assert.That(state.Current.Fault, Is.EqualTo(new AssistantFault(FaultCause.ModelMissing, "ModelMissing", FaultOrigin.LanguageModel)));
    }

    [TestCase(ModelLoadState.Loaded)]
    [TestCase(ModelLoadState.Unknown)]
    public async Task A_successful_load_clears_the_fault_and_the_count(ModelLoadState success)
    {
        await LoadAsync(ModelLoadState.Failed, LlmFailure.Unreachable);
        await LoadAsync(ModelLoadState.Failed, LlmFailure.Unreachable);

        await LoadAsync(success, null);
        Assert.That(state.Current.Fault, Is.Null);

        await LoadAsync(ModelLoadState.Failed, LlmFailure.Unreachable);
        Assert.That(state.Current.Fault, Is.Null, "the count of failures starts again");
    }

    [Test]
    public async Task A_successful_load_leaves_the_faults_of_other_parts()
    {
        var whisper = new AssistantFault(FaultCause.ModelMissing, "ggml-large-v3-turbo.bin", FaultOrigin.Transcription);
        await bus.PublishAsync<AssistantInput>(new FaultRaised(whisper));

        await LoadAsync(ModelLoadState.Loaded, null);

        Assert.That(state.Current.Fault, Is.EqualTo(whisper));
    }

    [Test]
    public async Task The_unknown_state_of_a_load_cancelled_before_it_started_clears_nothing()
    {
        await bus.PublishAsync<AssistantInput>(new FaultRaised(new AssistantFault(FaultCause.ModelUnreachable, "Unreachable", FaultOrigin.LanguageModel)));

        await bus.PublishAsync(new AssistantModelLoadChanged("Ollama", "qwen3:14b", ModelLoadState.Unknown));

        Assert.That(state.Current.Fault, Is.Not.Null);
    }

    private async Task LoadAsync(ModelLoadState result, LlmFailure? failure)
    {
        await bus.PublishAsync(new AssistantModelLoadChanged("Ollama", "qwen3:14b", ModelLoadState.Loading));
        await bus.PublishAsync(new AssistantModelLoadChanged("Ollama", "qwen3:14b", result, failure));
    }
}
