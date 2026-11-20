using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>The providers setting (US1.8, US1.9, ADR 0007, sections 1 and 5).</summary>
[TestFixture]
public sealed class LlmProvidersOptionsTests
{
    [Test]
    public void By_default_ollama_answers_with_qwen3_without_reasoning()
    {
        var options = LlmProvidersOptions.Section.CreateDefault();
        var provider = options.Default?.ToProvider();

        Assert.Multiple(() =>
        {
            Assert.That(LlmProvidersOptions.Section.Validate(options), Is.Empty);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider!.Kind, Is.EqualTo(LlmProviderKind.Ollama));
            Assert.That(provider.Model, Is.EqualTo("qwen3:14b"));
            Assert.That(provider.Tuning, Is.EqualTo(new LlmTuning(Think: false, ContextLength: 4096, Temperature: 0.3, KeepLoaded: true)));
            Assert.That(provider.Key?.Value, Is.EqualTo("llm.local.apikey"));
        });
    }

    [Test]
    public void A_cloud_provider_cannot_be_the_default_at_stage_1()
    {
        var claude = LlmProvidersOptions.Template(LlmProviderKind.Anthropic, "claude", "Claude");
        var options = new LlmProvidersOptions { Providers = [claude], DefaultId = "claude" };

        Assert.That(LlmProvidersOptions.Section.Validate(options), Has.One.Contains("must be local"));
    }

    [Test]
    public void Each_problem_is_named()
    {
        var bad = new LlmProviderSettings
        {
            Id = "Bad Id", Name = " ", Kind = LlmProviderKind.Ollama, Address = "127.0.0.1:11435", Model = "",
            ContextLength = 100, Temperature = 3, MaxOutputTokens = 0,
        };
        var twin = LlmProvidersOptions.Template(LlmProviderKind.Ollama, "same", "A");
        var options = new LlmProvidersOptions { Providers = [bad, twin, twin with { Name = "B" }], DefaultId = "missing" };

        var problems = LlmProvidersOptions.Section.Validate(options);

        Assert.That(problems, Has.Count.EqualTo(9));
    }

    [Test]
    public void No_default_is_allowed()
    {
        var options = new LlmProvidersOptions { Providers = [], DefaultId = null };

        Assert.That(LlmProvidersOptions.Section.Validate(options), Is.Empty);
    }

    [Test]
    public async Task The_kind_is_saved_by_name_and_read_back()
    {
        using var folder = new TemporaryFolder();
        var store = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        var options = new LlmProvidersOptions
        {
            Providers = [LlmProvidersOptions.Template(LlmProviderKind.OpenAiCompatible, "llama", "llama-server")],
            DefaultId = "llama",
        };

        var result = await store.WriteAsync(LlmProvidersOptions.Section, options);
        var read = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System).Read(LlmProvidersOptions.Section);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(File.ReadAllText(folder.Paths.ConfigFile), Does.Contain("\"OpenAiCompatible\""));
            Assert.That(read.Providers, Is.EqualTo(options.Providers));
            Assert.That(read.DefaultId, Is.EqualTo("llama"));
        });
    }
}
