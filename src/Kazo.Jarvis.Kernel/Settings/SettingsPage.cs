namespace Kazo.Jarvis.Kernel.Settings;

/// <summary>
/// A page of the settings window, declared by the slice that owns it and registered in the service
/// container. The settings window lists every registration without knowing the slices (ADR 0002, section 3).
/// </summary>
/// <param name="Key">Stable identifier of the page, for instance <c>Settings.Language</c>.</param>
/// <param name="Component">The Razor component of the page. The kernel names it as a <see cref="Type"/> only.</param>
/// <param name="TitleResource">The type whose resources hold the title (an <c>IStringLocalizer</c> of it is used).</param>
/// <param name="TitleKey">Key of the title in those resources.</param>
/// <param name="Order">Position in the list, smallest first.</param>
public sealed record SettingsPage(string Key, Type Component, Type TitleResource, string TitleKey, int Order);
