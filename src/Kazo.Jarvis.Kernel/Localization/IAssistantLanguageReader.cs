namespace Kazo.Jarvis.Kernel.Localization;

/// <summary>
/// Read access to the language of the assistant (ADR 0008), for a slice that needs it when it starts. A
/// change is announced by <c>AssistantLanguageChanged</c>. The language slice provides it.
/// </summary>
public interface IAssistantLanguageReader
{
    /// <summary>The language in use now.</summary>
    AssistantLanguage Current { get; }
}
