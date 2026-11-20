using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Kernel.Settings;

/// <summary>
/// Declares the settings of one slice (ADR 0004, section 2): the name of its section in <c>config.json</c>,
/// its default values and its validation. The slice owns it; no other slice reads that section.
/// </summary>
/// <typeparam name="T">The options class, serialized as JSON. Treat instances as immutable.</typeparam>
public sealed partial class SettingsSection<T>
    where T : class
{
    private readonly Func<T> defaults;
    private readonly Func<T, IReadOnlyList<string>> validate;

    /// <summary>Declares a section.</summary>
    /// <param name="name">Section name, <c>&lt;Area&gt;.&lt;Slice&gt;</c> (letters and digits, one dot), for instance <c>Settings.Language</c>.</param>
    /// <param name="defaults">Builds the default values; called each time they are needed.</param>
    /// <param name="validate">Returns the problems of a value, empty when it is valid. No check when omitted.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not of the form <c>&lt;Area&gt;.&lt;Slice&gt;</c>.</exception>
    public SettingsSection(string name, Func<T> defaults, Func<T, IReadOnlyList<string>>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(defaults);
        if (!IsValidName(name))
        {
            throw new ArgumentException($"A settings section is named <Area>.<Slice>, not '{name}'.", nameof(name));
        }

        Name = name;
        this.defaults = defaults;
        this.validate = validate ?? (_ => []);
    }

    /// <summary>Name of the section in <c>config.json</c>.</summary>
    public string Name { get; }

    /// <summary>A new instance holding the default values.</summary>
    /// <returns>The default values.</returns>
    public T CreateDefault() => defaults();

    /// <summary>The problems of <paramref name="value"/>, empty when it can be saved.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns>One readable sentence per problem. Never holds a secret: settings hold none.</returns>
    public IReadOnlyList<string> Validate(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return validate(value);
    }

    /// <summary>Whether <paramref name="name"/> is a valid section name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> for <c>&lt;Area&gt;.&lt;Slice&gt;</c>.</returns>
    public static bool IsValidName(string name) => name is not null && NamePattern().IsMatch(name);

    /// <inheritdoc />
    public override string ToString() => Name;

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]{0,49}\.[A-Z][A-Za-z0-9]{0,49}$")]
    private static partial Regex NamePattern();
}
