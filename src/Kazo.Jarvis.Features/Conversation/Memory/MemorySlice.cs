using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Conversation;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>Registration of the memory slice (US2.4, ADR 0019).</summary>
public static class MemorySlice
{
    /// <summary>Key of the « Mémoire » page in the settings window.</summary>
    public const string PageKey = "Conversation.Memory";

    /// <summary>
    /// Adds what Jarvis keeps (facts and summary), what it recalls to the model, the background work that draws
    /// facts and writes the summary, the three tools of the memory and the « Mémoire » page. The conversation slice
    /// only knows the kernel contract <see cref="IConversationMemory"/> and the messages of a recorded turn.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddMemorySlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<MemoryBook>();
        services.TryAddSingleton<MemoryRecaller>();
        services.TryAddSingleton<IConversationMemory>(provider => provider.GetRequiredService<MemoryRecaller>());

        services.TryAddSingleton<MemoryWorker>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnRecorded>, MemoryWorker>(Worker));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ExchangesLeftHistory>, MemoryWorker>(Worker));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AssistantStateChanged>, MemoryWorker>(Worker));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, RememberFactTool>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ForgetFactTool>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, SearchMemoryTool>());

        // Right after the personality (15): what the model is told next.
        services.AddSingleton(new SettingsPage(PageKey, typeof(MemoryPage), typeof(MemoryPage), "Title", Order: 16));
        return services;
    }

    private static MemoryWorker Worker(IServiceProvider provider) => provider.GetRequiredService<MemoryWorker>();
}
