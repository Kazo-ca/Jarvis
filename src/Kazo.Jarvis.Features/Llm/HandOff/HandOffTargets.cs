using System.Globalization;
using System.Resources;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Features.Llm.HandOff;

/// <summary>
/// The targets of the hand-off (US2.7, ADR 0020, section 1). The target of the questions is one of the configured
/// cloud providers; its client follows the settings. The tool is in the registry only while that target is set:
/// the local model is not offered what it cannot do. The target of the tasks, Claude Code, comes with its
/// sessions (ADR 0021): until then a hand-off of that kind is refused with its cause.
/// </summary>
public sealed class HandOffTargets : IHandOff, IHostedService, IDisposable
{
    private static readonly ResourceManager Texts = new(typeof(HandOffTexts));

    private readonly ISettingsStore settings;
    private readonly ILlmProviderList providers;
    private readonly ILlmConnector connector;
    private readonly IToolRegistry registry;
    private readonly HandOffTool tool;
    private readonly IDisposable watch;
    private readonly Lock gate = new();
    private LlmProvider? provider;
    private IChatClient? client;
    private IDisposable? offered;
    private bool disposed;

    /// <summary>Creates the targets and reads the settings.</summary>
    /// <param name="settings">The settings, for <see cref="HandOffOptions.Section"/>.</param>
    /// <param name="providers">The configured providers.</param>
    /// <param name="connector">Reaches the providers.</param>
    /// <param name="registry">The registry the tool joins while a target is set.</param>
    public HandOffTargets(ISettingsStore settings, ILlmProviderList providers, ILlmConnector connector, IToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(providers);
        this.settings = settings;
        this.providers = providers;
        this.connector = connector;
        this.registry = registry;
        tool = new HandOffTool(() => QuestionsTarget is { } target ? SpokenName(target) : null);

        // Watch before reading: a save between the two is not missed.
        watch = settings.Watch(HandOffOptions.Section, _ => Refresh());
        providers.Changed += Refresh;
        Refresh();
    }

    /// <inheritdoc />
    public string ToolName => HandOffTool.ToolName;

    /// <summary>The cloud provider that answers the questions now, or <see langword="null"/>.</summary>
    public LlmProvider? QuestionsTarget
    {
        get
        {
            lock (gate)
            {
                return provider;
            }
        }
    }

    /// <summary>The providers that can be a target of the questions: the cloud ones (ADR 0020, section 1).</summary>
    /// <param name="all">The configured providers.</param>
    /// <returns>The cloud providers, in the same order.</returns>
    public static IReadOnlyList<LlmProvider> Candidates(IReadOnlyList<LlmProvider> all)
    {
        ArgumentNullException.ThrowIfNull(all);
        return [.. all.Where(candidate => !candidate.Kind.IsLocal())];
    }

    /// <summary>The name said to the user for <paramref name="target"/>: the assistant behind the API, not the name typed in the settings.</summary>
    /// <param name="target">A cloud provider.</param>
    /// <returns>« Claude » or « ChatGPT ».</returns>
    public static string SpokenName(LlmProvider target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Kind == LlmProviderKind.OpenAi ? "ChatGPT" : "Claude";
    }

    /// <summary>A text of <c>HandOffTexts.resx</c> in <paramref name="language"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="language">The language of the turn.</param>
    /// <param name="arguments">The values of its placeholders.</param>
    /// <returns>The text.</returns>
    public static string Text(string key, AssistantLanguage language, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(language);
        var text = Texts.GetString(key, language.Culture) ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, arguments);
    }

    /// <inheritdoc />
    public ForcedHandOff? Forced(string request) => HandOffPhrases.Match(request);

    /// <inheritdoc />
    public HandOffCall? Read(JsonElement arguments) => HandOffTool.Read(arguments);

    /// <inheritdoc />
    public HandOffOpening Open(HandOffKind kind)
    {
        if (kind == HandOffKind.Task)
        {
            return HandOffOpening.Refused(HandOffRefusal.NoTasksTarget);
        }

        lock (gate)
        {
            return provider is not null && client is not null
                ? HandOffOpening.To(new HandOffTarget(provider, client, SpokenName(provider), settings.Read(HandOffOptions.Section).WithMemory))
                : HandOffOpening.Refused(HandOffRefusal.NoQuestionsTarget);
        }
    }

    /// <summary>Nothing to start: creating the targets read the settings. Being a hosted service only makes that happen when Jarvis starts.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        IChatClient? last;
        IDisposable? registration;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            (last, client, provider) = (client, null, null);
            (registration, offered) = (offered, null);
        }

        providers.Changed -= Refresh;
        watch.Dispose();
        registration?.Dispose();
        last?.Dispose();
    }

    /// <summary>Follows the settings: the client of the target, and the tool in or out of the registry.</summary>
    private void Refresh()
    {
        var id = settings.Read(HandOffOptions.Section).QuestionsProviderId;
        var next = id is null ? null : Candidates(providers.Providers).FirstOrDefault(candidate => candidate.Id == id);
        IChatClient? replaced = null;
        IDisposable? withdrawn = null;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (next != provider)
            {
                replaced = client;
                provider = next;
                client = next is null ? null : connector.CreateClient(next);
            }

            if (next is null)
            {
                (withdrawn, offered) = (offered, null);
            }
            else
            {
                offered ??= registry.Register(tool);
            }
        }

        withdrawn?.Dispose();
        replaced?.Dispose();
    }
}

/// <summary>Marker of the resources <c>HandOffTexts.resx</c> and <c>HandOffTexts.fr.resx</c>.</summary>
public sealed class HandOffTexts
{
    private HandOffTexts()
    {
    }
}
