using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>The configured providers, read from the settings each time: the list is short and changes rarely.</summary>
public sealed class LlmProviderList : ILlmProviderList, IDisposable
{
    private readonly ISettingsStore settings;
    private readonly IDisposable watch;

    /// <summary>Creates the list.</summary>
    /// <param name="settings">The settings, for <see cref="LlmProvidersOptions.Section"/>.</param>
    public LlmProviderList(ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        watch = settings.Watch(LlmProvidersOptions.Section, _ => Changed?.Invoke());
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<LlmProvider> Providers =>
        [.. settings.Read(LlmProvidersOptions.Section).Providers.Select(provider => provider.ToProvider()).OfType<LlmProvider>()];

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();
}
