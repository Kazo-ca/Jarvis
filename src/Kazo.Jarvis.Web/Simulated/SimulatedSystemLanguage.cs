using System.Globalization;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated Windows display language: <c>Simulated:SystemLanguage</c> in the configuration, English when
/// absent, so the proposal of the language slice is the same on every machine (ADR 0003, section 3).
/// </summary>
public sealed class SimulatedSystemLanguage : ISystemLanguage
{
    /// <summary>Configuration key of the simulated language.</summary>
    public const string ConfigurationKey = "Simulated:SystemLanguage";

    /// <summary>Reads the simulated language.</summary>
    /// <param name="configuration">The host configuration.</param>
    public SimulatedSystemLanguage(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        DisplayCulture = CultureInfo.GetCultureInfo(configuration[ConfigurationKey] ?? "en");
    }

    /// <inheritdoc />
    public CultureInfo DisplayCulture { get; }
}
