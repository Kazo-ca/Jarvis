using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Kernel;

/// <summary>Registers the kernel services: bus, state machine and sound level feed.</summary>
public static class KernelServiceCollectionExtensions
{
    /// <summary>
    /// Adds the kernel to <paramref name="services"/>. Safe to call more than once. Called by
    /// <c>AddJarvisFeatures</c>, so a host does not call it itself.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisKernel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMessageBus>(provider => new InMemoryMessageBus(provider));

        services.TryAddSingleton(provider => new AssistantStateKeeper(
            provider.GetRequiredService<IMessageBus>(),
            LoggerFor<AssistantStateKeeper>(provider)));
        services.TryAddSingleton<IAssistantStateReader>(provider => provider.GetRequiredService<AssistantStateKeeper>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AssistantInput>, AssistantStateKeeper>(
            provider => provider.GetRequiredService<AssistantStateKeeper>()));

        services.TryAddSingleton(provider => new SoundLevelFeed(provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<ISoundLevelSource>(provider => provider.GetRequiredService<SoundLevelFeed>());
        services.TryAddSingleton<ISoundLevelSink>(provider => provider.GetRequiredService<SoundLevelFeed>());

        return services;
    }

    /// <summary>The host's logger when it registered logging; a silent one otherwise (the Windows host has none yet).</summary>
    private static ILogger LoggerFor<T>(IServiceProvider provider) =>
        (provider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance).CreateLogger<T>();
}
