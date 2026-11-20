using System.Globalization;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Tests.Unit.Features.Settings;

/// <summary>A fixed Windows display language.</summary>
internal sealed class FixedSystemLanguage(string culture) : ISystemLanguage
{
    public CultureInfo DisplayCulture { get; } = CultureInfo.GetCultureInfo(culture);
}

/// <summary>A language reader the test sets by hand.</summary>
internal sealed class ManualLanguage : IAssistantLanguageReader
{
    public AssistantLanguage Current { get; set; } = AssistantLanguage.English;
}
