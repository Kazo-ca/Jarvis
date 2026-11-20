using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>Registration of the language model providers slice (US1.8, US1.9, ADR 0007).</summary>
public static class LlmProvidersSlice
{
    /// <summary>Key of the providers page in the settings window.</summary>
    public const string PageKey = "Llm.Providers";

    /// <summary>
    /// Adds the keeper of the default model, which provides <see cref="IAssistantModel"/> and preloads the model
    /// in the background, and the settings page. The host provides <see cref="ILlmConnector"/> and a
    /// <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddLlmProvidersSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AssistantModelKeeper>();
        services.TryAddSingleton<IAssistantModel>(provider => provider.GetRequiredService<AssistantModelKeeper>());
        services.AddHostedService(provider => provider.GetRequiredService<AssistantModelKeeper>());
        services.TryAddTransient<ProviderEditor>();

        // After the language page: the language changes every other page.
        services.AddSingleton(new SettingsPage(PageKey, typeof(LlmProvidersSettingsPage), typeof(LlmProvidersSettingsPage), "Title", Order: 10));
        return services;
    }
}
