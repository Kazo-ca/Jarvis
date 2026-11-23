using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Llm.Spend;

/// <summary>Registration of the spending slice (US2.8, ADR 0020, section 6).</summary>
public static class SpendSlice
{
    /// <summary>Key of the spending page in the settings window.</summary>
    public const string PageKey = "Llm.Spend";

    /// <summary>
    /// Adds the ledger, which gives <see cref="ISpendCap"/> to whoever calls a cloud provider and counts each
    /// <see cref="ProviderAnswered"/>, and the settings page.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSpendSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SpendLedger>();
        services.TryAddSingleton<ISpendCap>(Ledger);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ProviderAnswered>, SpendLedger>(Ledger));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ClaudeTaskEnded>, SpendLedger>(Ledger));
        services.AddHostedService(Ledger);

        // Right after the hand-off (11): what it costs.
        services.AddSingleton(new SettingsPage(PageKey, typeof(SpendPage), typeof(SpendPage), "Title", Order: 12));
        return services;
    }

    private static SpendLedger Ledger(IServiceProvider provider) => provider.GetRequiredService<SpendLedger>();
}
