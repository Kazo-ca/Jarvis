using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>
/// Logic of the providers page (US1.8), apart from the component so that it is tested without a browser: a
/// draft of the list, edited in place, tested before it is saved, and saved in one go.
/// </summary>
public sealed class ProviderEditor
{
    /// <summary>The longest a connection test waits (ADR 0007, section 1: « délai dépassé » is one of the causes).</summary>
    public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(20);

    private readonly ISettingsStore settings;
    private readonly ILlmConnector connector;
    private readonly Dictionary<string, LlmConnectionResult> results = new(StringComparer.Ordinal);
    private readonly HashSet<string> testing = new(StringComparer.Ordinal);

    /// <summary>Creates the editor on the saved settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="connector">Reaches the providers for the tests.</param>
    public ProviderEditor(ISettingsStore settings, ILlmConnector connector)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(connector);
        this.settings = settings;
        this.connector = connector;
        Reload();
    }

    /// <summary>The providers being edited, in order.</summary>
    public List<LlmProviderSettings> Drafts { get; } = [];

    /// <summary>Id of the default provider in the draft.</summary>
    public string? DefaultId { get; set; }

    /// <summary>The problems of the last save, empty when it passed.</summary>
    public IReadOnlyList<string> Problems { get; private set; } = [];

    /// <summary>Outcome of the last save, <see langword="null"/> before any.</summary>
    public SettingsWriteOutcome? LastSave { get; private set; }

    /// <summary>Whether the draft differs from the saved settings.</summary>
    public bool IsDirty
    {
        get
        {
            var saved = settings.Read(LlmProvidersOptions.Section);
            return saved.DefaultId != DefaultId || !saved.Providers.SequenceEqual(Drafts);
        }
    }

    /// <summary>The result of the last test of a provider, if any and if it was not changed since.</summary>
    /// <param name="id">The id of the provider.</param>
    /// <returns>The result, or <see langword="null"/>.</returns>
    public LlmConnectionResult? ResultOf(string id) => results.GetValueOrDefault(id);

    /// <summary>Whether a test of the provider is running.</summary>
    /// <param name="id">The id of the provider.</param>
    /// <returns><see langword="true"/> while it runs.</returns>
    public bool IsTesting(string id) => testing.Contains(id);

    /// <summary>Drops the draft and reads the saved settings again.</summary>
    public void Reload()
    {
        var saved = settings.Read(LlmProvidersOptions.Section);
        Drafts.Clear();
        Drafts.AddRange(saved.Providers);
        DefaultId = saved.DefaultId;
        results.Clear();
        Problems = [];
    }

    /// <summary>Adds a provider of <paramref name="kind"/> with the usual address and model of that kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="name">Its name, already translated.</param>
    /// <returns>The new provider.</returns>
    public LlmProviderSettings Add(LlmProviderKind kind, string name)
    {
        var stem = kind switch
        {
            LlmProviderKind.Ollama => "ollama",
            LlmProviderKind.OpenAiCompatible => "local-server",
            LlmProviderKind.OpenAi => "openai",
            _ => "anthropic",
        };
        var id = stem;
        for (var n = 2; Drafts.Any(p => p.Id == id); n++)
        {
            id = $"{stem}-{n}";
        }

        var added = LlmProvidersOptions.Template(kind, id, name);
        Drafts.Add(added);

        // The first local provider becomes the default, so that Jarvis can answer.
        if (DefaultId is null && kind.IsLocal())
        {
            DefaultId = id;
        }

        return added;
    }

    /// <summary>Replaces a provider by its edited copy. The id never changes: it names the key in the vault.</summary>
    /// <param name="edited">The edited provider.</param>
    public void Update(LlmProviderSettings edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        var index = Drafts.FindIndex(p => p.Id == edited.Id);
        if (index < 0)
        {
            return;
        }

        // A kind that is no longer local cannot stay the default (ADR 0007, section 1).
        Drafts[index] = edited;
        if (DefaultId == edited.Id && !edited.Kind.IsLocal())
        {
            DefaultId = null;
        }

        // A test of other values says nothing about these ones.
        results.Remove(edited.Id);
    }

    /// <summary>Removes a provider from the draft. Its key stays in the vault until removed with its field.</summary>
    /// <param name="id">The id of the provider.</param>
    public void Remove(string id)
    {
        Drafts.RemoveAll(p => p.Id == id);
        results.Remove(id);
        if (DefaultId == id)
        {
            DefaultId = Drafts.FirstOrDefault(p => p.Kind.IsLocal())?.Id;
        }
    }

    /// <summary>
    /// Tests the drafted values of a provider, saved or not, within <see cref="TestTimeout"/>.
    /// </summary>
    /// <param name="id">The id of the provider.</param>
    /// <param name="cancellationToken">Cancels the test (the page closes).</param>
    /// <returns>The result, also kept for <see cref="ResultOf"/>.</returns>
    public async Task<LlmConnectionResult> TestAsync(string id, CancellationToken cancellationToken = default)
    {
        var draft = Drafts.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"No provider '{id}'.", nameof(id));
        LlmConnectionResult result;
        if (draft.ToProvider() is not { } provider || !LlmProviderAddress.IsCallable(provider.Endpoint))
        {
            result = LlmConnectionResult.Failed(LlmFailure.InvalidAddress, null, TimeSpan.Zero);
        }
        else
        {
            testing.Add(id);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(TestTimeout);
            try
            {
                result = await connector.TestConnectionAsync(provider, bounded.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = LlmConnectionResult.Failed(LlmFailure.TimedOut, null, TestTimeout);
            }
            finally
            {
                testing.Remove(id);
            }
        }

        // The values may have changed during the test: keep the result only if they did not.
        if (Drafts.FirstOrDefault(p => p.Id == id) == draft)
        {
            results[id] = result;
        }

        return result;
    }

    /// <summary>Saves the draft; the default model follows at once.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Whether it was saved, and why not.</returns>
    public async Task<SettingsWriteResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        var trimmed = Drafts.Select(p => p with { Name = p.Name.Trim(), Address = p.Address.Trim(), Model = p.Model.Trim() }).ToList();
        var result = await settings.WriteAsync(
            LlmProvidersOptions.Section,
            new LlmProvidersOptions { Providers = trimmed, DefaultId = DefaultId },
            cancellationToken).ConfigureAwait(false);
        LastSave = result.Outcome;
        Problems = result.Problems;
        if (result.IsSaved)
        {
            Drafts.Clear();
            Drafts.AddRange(trimmed);
        }

        return result;
    }
}

/// <summary>Checks on a provider address, shared by the page and the editor.</summary>
public static class LlmProviderAddress
{
    /// <summary>Whether <paramref name="endpoint"/> is an absolute <c>http</c> or <c>https</c> address.</summary>
    /// <param name="endpoint">The address.</param>
    /// <returns><see langword="true"/> when it can be called.</returns>
    public static bool IsCallable(Uri endpoint) =>
        endpoint.IsAbsoluteUri && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);
}
