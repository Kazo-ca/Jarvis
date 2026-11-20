using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Settings.Language;

/// <summary>Registration of the language slice (US1.4, ADR 0008).</summary>
public static class LanguageSlice
{
    /// <summary>Key of the language page in the settings window.</summary>
    public const string PageKey = "Settings.Language";

    /// <summary>
    /// Adds the language keeper, which provides <see cref="IAssistantLanguageReader"/>, and its settings page.
    /// The host provides <see cref="ISystemLanguage"/>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddLanguageSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<LanguageKeeper>();
        services.TryAddSingleton<IAssistantLanguageReader>(provider => provider.GetRequiredService<LanguageKeeper>());

        // First page of the settings window: the language changes every other page.
        services.AddSingleton(new SettingsPage(PageKey, typeof(LanguageSettingsPage), typeof(LanguageSettingsPage), "Title", Order: 0));
        return services;
    }
}
