using System.Collections;
using System.Globalization;
using System.Resources;
using Kazo.Jarvis.Kernel.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Shared.Localization;

/// <summary>
/// Texts of one resource file in the language of the assistant (ADR 0008). The language is read at each
/// lookup from <see cref="IAssistantLanguageReader"/>, never from the thread culture: a change applies to
/// the next render, in every window, and the formats keep the Windows regional settings.
/// </summary>
public sealed partial class LanguageStringLocalizer : IStringLocalizer
{
    private readonly ResourceManager resources;
    private readonly string baseName;
    private readonly IAssistantLanguageReader language;
    private readonly ILogger logger;

    /// <summary>Creates the localizer; <see cref="LanguageStringLocalizerFactory"/> does it.</summary>
    /// <param name="resources">The resources of the type.</param>
    /// <param name="baseName">Their name, for the log.</param>
    /// <param name="language">Gives the language of every lookup.</param>
    /// <param name="logger">Logs the missing texts.</param>
    public LanguageStringLocalizer(ResourceManager resources, string baseName, IAssistantLanguageReader language, ILogger logger)
    {
        this.resources = resources;
        this.baseName = baseName;
        this.language = language;
        this.logger = logger;
    }

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            var value = Find(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null, searchedLocation: baseName);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            var format = Find(name);

            // Numbers and dates in a text follow the Windows regional settings, not the assistant language.
            var value = string.Format(CultureInfo.CurrentCulture, format ?? name, arguments);
            return new LocalizedString(name, value, resourceNotFound: format is null, searchedLocation: baseName);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = language.Current.Culture;
        var names = new SortedSet<string>(StringComparer.Ordinal);
        AddNames(names, culture);
        if (includeParentCultures)
        {
            AddNames(names, CultureInfo.InvariantCulture);
        }

        foreach (var name in names)
        {
            var value = resources.GetString(name, culture);
            yield return new LocalizedString(name, value ?? name, resourceNotFound: value is null, searchedLocation: baseName);
        }
    }

    private string? Find(string name)
    {
        var culture = language.Current.Culture;
        var value = SafeGetString(name, culture);
        if (value is null)
        {
            LogMissing(name, baseName);
            return null;
        }

        // The English text shows when the French one is missing (ADR 0008, section 2): say so in the log.
        if (!language.Current.Equals(AssistantLanguage.English) && logger.IsEnabled(LogLevel.Debug)
            && resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?.GetString(name) is null)
        {
            LogMissingTranslation(name, baseName, language.Current.Code);
        }

        return value;
    }

    private string? SafeGetString(string name, CultureInfo culture)
    {
        try
        {
            return resources.GetString(name, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }

    private void AddNames(SortedSet<string> names, CultureInfo culture)
    {
        ResourceSet? set;
        try
        {
            set = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        }
        catch (MissingManifestResourceException)
        {
            return;
        }

        if (set is null)
        {
            return;
        }

        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key)
            {
                names.Add(key);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The text '{Name}' is missing from the resources {Resources}.")]
    private partial void LogMissing(string name, string resources);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The text '{Name}' of {Resources} has no '{Language}' translation: the English text shows.")]
    private partial void LogMissingTranslation(string name, string resources, string language);
}

/// <summary>The <see cref="LanguageStringLocalizer"/> of the resources of <typeparamref name="T"/>, injected by components.</summary>
/// <typeparam name="T">The type whose resources hold the texts.</typeparam>
public sealed class LanguageStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly IStringLocalizer inner;

    /// <summary>Creates the localizer.</summary>
    /// <param name="factory">The localizer factory.</param>
    public LanguageStringLocalizer(IStringLocalizerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        inner = factory.Create(typeof(T));
    }

    /// <inheritdoc />
    public LocalizedString this[string name] => inner[name];

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] => inner[name, arguments];

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
}
