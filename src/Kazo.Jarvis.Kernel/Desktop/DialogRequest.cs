namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>A component to show in its own window (ADR 0003, section 2).</summary>
/// <param name="Key">One window per key: opening a key already open brings its window forward.</param>
/// <param name="Component">The Razor component type. The kernel names it as a <see cref="Type"/> only.</param>
/// <param name="Title">The window title, already translated.</param>
/// <param name="Parameters">The component parameters, or <see langword="null"/>.</param>
public sealed record DialogRequest(
    string Key,
    Type Component,
    string Title,
    IReadOnlyDictionary<string, object?>? Parameters = null)
{
    /// <summary>
    /// Whether the window takes the whole screen when it opens (the projects view, ADR 0026, section 7). A host
    /// with no window of its own may ignore it.
    /// </summary>
    public bool Maximized { get; init; }
}
