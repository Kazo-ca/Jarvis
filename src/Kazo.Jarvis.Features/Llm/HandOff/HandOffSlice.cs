using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Llm.HandOff;

/// <summary>Registration of the hand-off slice (US2.5 to US2.7, ADR 0020).</summary>
public static class HandOffSlice
{
    /// <summary>Key of the hand-off page in the settings window.</summary>
    public const string PageKey = "Llm.HandOff";

    /// <summary>
    /// Adds the targets, which provide <see cref="IHandOff"/> to the conversation and keep the tool
    /// <c>hand_off_to_claude</c> in the registry while a target is set, and the settings page. The providers come
    /// from the language models slice through <see cref="Kernel.Llm.ILlmProviderList"/>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddHandOffSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<HandOffTargets>();
        services.TryAddSingleton<IHandOff>(provider => provider.GetRequiredService<HandOffTargets>());
        services.AddHostedService(provider => provider.GetRequiredService<HandOffTargets>());

        // Right after the language models (10): a target is one of them.
        services.AddSingleton(new SettingsPage(PageKey, typeof(HandOffPage), typeof(HandOffPage), "Title", Order: 11));
        return services;
    }
}
