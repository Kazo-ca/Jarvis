namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The agent the user chose in the conversation window (ADR 0025, section 4): a typed turn then goes straight to
/// it, without the Jarvis agent. The choice holds until the user removes it, and is forgotten when Jarvis stops.
/// A spoken request never follows it: by voice the Jarvis agent always receives the request (section 9).
/// </summary>
public sealed class AgentChoice
{
    private string? chosen;
    private string? project;

    /// <summary>Raised after the choice changed; on any thread.</summary>
    public event Action? Changed;

    /// <summary>The identity of the agent chosen, or <see langword="null"/> when the Jarvis agent sorts the requests.</summary>
    public string? Chosen
    {
        get => Volatile.Read(ref chosen);
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? null : value;
            if (Interlocked.Exchange(ref chosen, next) != next)
            {
                Changed?.Invoke();
            }
        }
    }

    /// <summary>
    /// The project the user chose for the typed requests (ADR 0025, section 10), by its identity, or
    /// <see langword="null"/> when a request names its project itself. Forgotten when Jarvis stops, like the agent.
    /// </summary>
    public string? Project
    {
        get => Volatile.Read(ref project);
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? null : value;
            if (Interlocked.Exchange(ref project, next) != next)
            {
                Changed?.Invoke();
            }
        }
    }
}
