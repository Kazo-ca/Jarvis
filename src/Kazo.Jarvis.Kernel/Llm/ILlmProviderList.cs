namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>
/// The providers the user configured (ADR 0007, section 1), for a slice that lets one be chosen without knowing
/// the slice that keeps them: the hand-off picks its target among the cloud ones (ADR 0020, section 1).
/// </summary>
public interface ILlmProviderList
{
    /// <summary>Raised after the list changed; on any thread.</summary>
    event Action? Changed;

    /// <summary>The providers that can be reached, in the order of the settings.</summary>
    IReadOnlyList<LlmProvider> Providers { get; }
}
