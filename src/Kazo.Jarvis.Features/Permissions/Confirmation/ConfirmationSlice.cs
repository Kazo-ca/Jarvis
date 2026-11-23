using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Permissions.Confirmation;

/// <summary>Registration of the confirmation requests (ADR 0009, principle 2; ADR 0023).</summary>
public static class ConfirmationSlice
{
    /// <summary>Key of the confirmation page in the settings window.</summary>
    public const string PageKey = "Permissions.Confirmation";

    /// <summary>
    /// Adds the desk of the requests, its window and the page that lists the words that approve. Without this slice the decision point denies
    /// every call that needs a confirmation.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddConfirmationSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<ConfirmationDesk>();
        services.TryAddSingleton<IConfirmation>(provider => provider.GetRequiredService<ConfirmationDesk>());
        services.TryAddSingleton<IOpenConfirmations>(provider => provider.GetRequiredService<ConfirmationDesk>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ConfirmationAnswerHeard>, ConfirmationDesk>(
            provider => provider.GetRequiredService<ConfirmationDesk>()));

        // After the models page (40): the permissions come last, with the log of the decisions (50).
        services.AddSingleton(new SettingsPage(PageKey, typeof(ConfirmationSettingsPage), typeof(ConfirmationSettingsPage), "Title", Order: 45));
        return services;
    }
}
