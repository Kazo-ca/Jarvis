using Kazo.Jarvis.Features.Shared.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Features.Shared;

/// <summary>Registration of what every slice shares: the texts in the language of the assistant (ADR 0008).</summary>
public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Makes <see cref="IStringLocalizer{T}"/> follow the language of the assistant rather than the thread
    /// culture. Replaces any localization registered before, so that every component reads the same language.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisShared(this IServiceCollection services)
    {
        services.RemoveAll<IStringLocalizerFactory>();
        services.RemoveAll(typeof(IStringLocalizer<>));
        services.AddSingleton<IStringLocalizerFactory, LanguageStringLocalizerFactory>();
        services.AddTransient(typeof(IStringLocalizer<>), typeof(LanguageStringLocalizer<>));
        return services;
    }
}
