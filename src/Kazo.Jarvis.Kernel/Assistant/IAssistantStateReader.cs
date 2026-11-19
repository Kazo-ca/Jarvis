namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// Read access to the current assistant state, for a component that starts after the last
/// <c>AssistantStateChanged</c> (the orb when its window opens, for instance). Nobody writes it directly.
/// </summary>
public interface IAssistantStateReader
{
    /// <summary>The state after the last input processed.</summary>
    AssistantState Current { get; }
}
