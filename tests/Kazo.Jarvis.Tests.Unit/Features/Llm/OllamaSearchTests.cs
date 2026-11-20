using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>
/// The search for Ollama on this PC (work item 14): its standard port, then 11435, where Benoit runs it in Docker.
/// </summary>
[TestFixture]
public sealed class OllamaSearchTests
{
    private static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(3);

    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private FakeConnector connector = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        connector = new FakeConnector();
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task Ollama_on_11435_is_found_after_11434_and_put_in_the_draft()
    {
        connector.Answer = provider => provider.Endpoint.Port == 11435
            ? LlmConnectionResult.Passed(Quick, ["qwen3:14b"])
            : LlmConnectionResult.Failed(LlmFailure.Unreachable, "refused", Quick);
        var editor = new ProviderEditor(settings, connector);
        Assert.That(editor.IsUntouchedDefault, Is.True);

        var found = await editor.FindOllamaAsync(LlmProvidersOptions.LocalId);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.EqualTo(new Uri("http://127.0.0.1:11435")));
            Assert.That(connector.Tested.Select(p => p.Endpoint.Port), Is.EqualTo(new[] { 11434, 11435 }));
            Assert.That(editor.Drafts[0].Address, Is.EqualTo("http://127.0.0.1:11435"));
            Assert.That(editor.ResultOf(LlmProvidersOptions.LocalId)?.Succeeded, Is.True);
            Assert.That(editor.IsDirty, Is.True);
            Assert.That(editor.IsUntouchedDefault, Is.False);
        });
    }

    [Test]
    public async Task The_standard_port_wins_and_leaves_the_defaults_untouched()
    {
        var editor = new ProviderEditor(settings, connector);

        var found = await editor.FindOllamaAsync(LlmProvidersOptions.LocalId);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.EqualTo(new Uri("http://127.0.0.1:11434")));
            Assert.That(connector.Tested, Has.Count.EqualTo(1));
            Assert.That(editor.IsDirty, Is.False);
            Assert.That(editor.IsUntouchedDefault, Is.True);
        });
    }

    [Test]
    public async Task A_server_without_the_model_counts_as_found()
    {
        connector.Answer = provider => provider.Endpoint.Port == 11435
            ? LlmConnectionResult.Failed(LlmFailure.ModelMissing, "no model", Quick, ["llama3"])
            : LlmConnectionResult.Failed(LlmFailure.Unreachable, "refused", Quick);
        var editor = new ProviderEditor(settings, connector);

        var found = await editor.FindOllamaAsync(LlmProvidersOptions.LocalId);

        Assert.Multiple(() =>
        {
            Assert.That(found?.Port, Is.EqualTo(11435));
            Assert.That(editor.ResultOf(LlmProvidersOptions.LocalId)?.Failure, Is.EqualTo(LlmFailure.ModelMissing));
        });
    }

    [Test]
    public async Task Nothing_found_leaves_the_draft_as_it_was()
    {
        connector.TestResult = LlmConnectionResult.Failed(LlmFailure.Unreachable, "refused", Quick);
        var editor = new ProviderEditor(settings, connector);

        var found = await editor.FindOllamaAsync(LlmProvidersOptions.LocalId);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Null);
            Assert.That(connector.Tested, Has.Count.EqualTo(2));
            Assert.That(editor.Drafts[0].Address, Is.EqualTo("http://127.0.0.1:11434"));
            Assert.That(editor.IsTesting(LlmProvidersOptions.LocalId), Is.False);
        });
    }

    [Test]
    public async Task The_search_keeps_the_model_of_the_draft()
    {
        var editor = new ProviderEditor(settings, connector);
        editor.Update(editor.Drafts[0] with { Model = "qwen3:8b" });

        await editor.FindOllamaAsync(LlmProvidersOptions.LocalId);

        Assert.Multiple(() =>
        {
            Assert.That(connector.Tested[0].Model, Is.EqualTo("qwen3:8b"));
            Assert.That(editor.IsUntouchedDefault, Is.False);
        });
    }

    [Test]
    public void Only_an_ollama_provider_is_searched()
    {
        var editor = new ProviderEditor(settings, connector);
        var claude = editor.Add(LlmProviderKind.Anthropic, "Claude");

        Assert.ThrowsAsync<ArgumentException>(() => editor.FindOllamaAsync(claude.Id));
    }
}
