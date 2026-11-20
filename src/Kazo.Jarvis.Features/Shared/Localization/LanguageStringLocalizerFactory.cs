using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;
using Kazo.Jarvis.Kernel.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.Shared.Localization;

/// <summary>
/// Creates the <see cref="LanguageStringLocalizer"/> of each type (ADR 0008, section 2). The resources of a
/// type <c>N.T</c> are <c>T.resx</c> (English, neutral) and <c>T.fr.resx</c> next to it, so their manifest
/// name is the full name of the type.
/// </summary>
public sealed class LanguageStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<string, ResourceManager> managers = new(StringComparer.Ordinal);
    private readonly IAssistantLanguageReader language;
    private readonly ILoggerFactory loggers;

    /// <summary>Creates the factory.</summary>
    /// <param name="language">Gives the language of every lookup.</param>
    /// <param name="loggers">Logs the missing texts; optional.</param>
    public LanguageStringLocalizerFactory(IAssistantLanguageReader language, ILoggerFactory? loggers = null)
    {
        this.language = language;
        this.loggers = loggers ?? NullLoggerFactory.Instance;
    }

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);
        var name = resourceSource.FullName ?? resourceSource.Name;
        return Create(name, resourceSource.Assembly);
    }

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(location);
        return Create(baseName, Assembly.Load(new AssemblyName(location)));
    }

    private LanguageStringLocalizer Create(string baseName, Assembly assembly)
    {
        var manager = managers.GetOrAdd(baseName + ", " + assembly.FullName, _ => new ResourceManager(baseName, assembly));
        return new LanguageStringLocalizer(manager, baseName, language, loggers.CreateLogger<LanguageStringLocalizer>());
    }
}
