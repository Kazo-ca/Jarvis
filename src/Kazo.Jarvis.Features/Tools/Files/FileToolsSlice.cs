using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Tools.Files;

/// <summary>Registration of the file tools <c>list_directory</c> and <c>read_file</c> (ADR 0017, section 3).</summary>
public static class FileToolsSlice
{
    /// <summary>Key of the « Dossiers autorisés » page in the settings window.</summary>
    public const string PageKey = "Tools.Files";

    /// <summary>
    /// Adds the allowed folders, their page and the two tools. The files of the PC come from the host
    /// (<see cref="ILocalFiles"/>); without them the tools are not registered.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddFileToolsSlice(this IServiceCollection services)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(ILocalFiles)))
        {
            return services;
        }

        services.TryAddSingleton<AllowedFolders>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ListDirectoryTool>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ReadFileTool>());

        // After the tools page (46): what the file tools may see.
        services.AddSingleton(new SettingsPage(PageKey, typeof(AllowedFoldersPage), typeof(AllowedFoldersPage), "Title", Order: 47));
        return services;
    }
}
