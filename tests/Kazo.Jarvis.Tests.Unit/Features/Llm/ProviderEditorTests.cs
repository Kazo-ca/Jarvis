using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>Logic of the providers page (US1.8): edit, test before saving, save in one go.</summary>
[TestFixture]
public sealed class ProviderEditorTests
{
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
    public void Added_providers_get_unique_ids_and_the_usual_values_of_their_kind()
    {
        var editor = new ProviderEditor(settings, connector);

        var second = editor.Add(LlmProviderKind.Ollama, "Ollama");
        var llama = editor.Add(LlmProviderKind.OpenAiCompatible, "llama-server");
        var claude = editor.Add(LlmProviderKind.Anthropic, "Claude");

        Assert.Multiple(() =>
        {
            Assert.That(editor.Drafts.Select(p => p.Id), Is.EqualTo(new[] { "local", "ollama", "local-server", "anthropic" }));
            Assert.That(second.Address, Is.EqualTo("http://127.0.0.1:11434"));
            Assert.That(llama.Address, Is.EqualTo("http://127.0.0.1:8080"));
            Assert.That(claude.Model, Is.EqualTo("claude-opus-5-5"));
            Assert.That(editor.DefaultId, Is.EqualTo("local"), "Adding does not change the default.");
            Assert.That(editor.Add(LlmProviderKind.Ollama, "x").Id, Is.EqualTo("ollama-2"));
        });
    }

    [Test]
    public async Task A_test_uses_the_drafted_values_and_is_forgotten_when_they_change()
    {
        var editor = new ProviderEditor(settings, connector);
        editor.Update(editor.Drafts[0] with { Address = "http://127.0.0.1:11435" });

        var result = await editor.TestAsync("local");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(connector.Tested.Single().Endpoint, Is.EqualTo(new Uri("http://127.0.0.1:11435")), "Tested before being saved.");
            Assert.That(editor.ResultOf("local"), Is.SameAs(result));
        });

        editor.Update(editor.Drafts[0] with { Model = "qwen2.5:14b" });
        Assert.That(editor.ResultOf("local"), Is.Null);
    }

    [Test]
    public async Task A_bad_address_fails_without_calling_anything()
    {
        var editor = new ProviderEditor(settings, connector);
        editor.Update(editor.Drafts[0] with { Address = "localhost:11435" });

        var result = await editor.TestAsync("local");

        Assert.Multiple(() =>
        {
            Assert.That(result.Failure, Is.EqualTo(LlmFailure.InvalidAddress));
            Assert.That(connector.Tested, Is.Empty);
        });
    }

    [Test]
    public async Task A_test_that_never_ends_is_a_timeout()
    {
        connector.HoldTest = new TaskCompletionSource();
        var editor = new ProviderEditor(settings, connector);
        using var page = new CancellationTokenSource();

        var running = editor.TestAsync("local", page.Token);
        Assert.That(editor.IsTesting("local"), Is.True);
        page.Cancel();

        Assert.That(async () => await running, Throws.InstanceOf<OperationCanceledException>(), "Closing the page cancels, it is not a timeout.");
        Assert.That(editor.IsTesting("local"), Is.False);
    }

    [Test]
    public void A_provider_that_becomes_cloud_stops_being_the_default_and_removing_the_default_picks_another_local()
    {
        var editor = new ProviderEditor(settings, connector);
        editor.Add(LlmProviderKind.OpenAiCompatible, "llama-server");

        editor.Update(LlmProvidersOptions.Template(LlmProviderKind.OpenAi, "local", "ChatGPT"));
        Assert.That(editor.DefaultId, Is.Null);

        editor.DefaultId = "local-server";
        editor.Remove("local-server");
        Assert.That(editor.DefaultId, Is.Null, "Only a cloud provider is left.");
    }

    [Test]
    public async Task Saving_writes_the_draft_and_reports_the_problems()
    {
        var editor = new ProviderEditor(settings, connector);
        editor.Update(editor.Drafts[0] with { Address = " http://127.0.0.1:11435 ", Name = "Ollama Docker" });
        Assert.That(editor.IsDirty, Is.True);

        var saved = await editor.SaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved.IsSaved, Is.True);
            Assert.That(editor.IsDirty, Is.False);
            Assert.That(settings.Read(LlmProvidersOptions.Section).Default?.Address, Is.EqualTo("http://127.0.0.1:11435"), "Spaces are a copy mistake.");
        });

        editor.Update(editor.Drafts[0] with { Model = " " });
        var refused = await editor.SaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(refused.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(editor.Problems, Has.One.Contains("needs a model"));
            Assert.That(settings.Read(LlmProvidersOptions.Section).Default?.Model, Is.EqualTo("qwen3:14b"));
        });

        editor.Reload();
        Assert.That(editor.IsDirty, Is.False);
    }
}
