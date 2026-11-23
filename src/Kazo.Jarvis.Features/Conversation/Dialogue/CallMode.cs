using System.Globalization;
using System.Text;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The « Appel » mode (ADR 0025, section 12): a conversation that goes on. While it lasts, the agent that
/// answered keeps the hand from one turn to the next, with its project, until the user asks to go back to
/// Jarvis; and Jarvis listens to what follows a spoken turn without the wake word. It starts with the button
/// « Appeler » or a sentence like « On doit discuter de… », and ends when the user says goodbye. Forgotten when
/// Jarvis stops.
/// </summary>
public sealed class CallMode
{
    /// <summary>How many times in a row Jarvis listens and hears nothing before it stops listening by itself; the call stays open.</summary>
    public const int MostSilences = 3;

    // Said to start a call. Compared by code, never by a model, on words without accents.
    private static readonly string[] Openings =
    [
        "on doit discuter", "on doit parler", "il faut qu on discute", "il faut qu on parle", "j aimerais discuter", "je veux discuter",
        "we need to talk", "we need to discuss", "we have to talk", "we have to discuss", "let s discuss", "let s talk about", "i want to discuss",
    ];

    // The whole request, to end one: « Au revoir », « Au revoir Jarvis », « Bye ».
    private static readonly string[] Goodbyes =
    [
        "au revoir", "au revoir jarvis", "bye", "bye bye", "bye jarvis", "goodbye", "goodbye jarvis", "good bye", "a plus tard", "a la prochaine", "salut jarvis a plus",
    ];

    private readonly Lock gate = new();
    private bool active;
    private string? agent;
    private string? project;
    private int silences;

    /// <summary>Raised after the call started, ended or changed hands; on any thread.</summary>
    public event Action? Changed;

    /// <summary>Whether a call goes on.</summary>
    public bool Active
    {
        get
        {
            lock (gate)
            {
                return active;
            }
        }
    }

    /// <summary>The identity of the agent that keeps the hand, or <see langword="null"/> while the Jarvis agent has it.</summary>
    public string? Agent
    {
        get
        {
            lock (gate)
            {
                return active ? agent : null;
            }
        }
    }

    /// <summary>The identity of the project the call is about, or <see langword="null"/>.</summary>
    public string? Project
    {
        get
        {
            lock (gate)
            {
                return active ? project : null;
            }
        }
    }

    /// <summary>Whether Jarvis still listens by itself after a spoken turn: not after <see cref="MostSilences"/> silences in a row.</summary>
    public bool Listens
    {
        get
        {
            lock (gate)
            {
                return active && silences < MostSilences;
            }
        }
    }

    /// <summary>Whether <paramref name="request"/> starts a call: « On doit discuter de Kazo OS ».</summary>
    /// <param name="request">The request, as heard or typed.</param>
    /// <returns><see langword="true"/> when it holds one of the opening sentences.</returns>
    public static bool Opens(string? request)
    {
        var words = " " + Plain(request) + " ";
        return Openings.Any(opening => words.Contains(" " + opening + " ", StringComparison.Ordinal));
    }

    /// <summary>Whether <paramref name="request"/> is a goodbye, and nothing else.</summary>
    /// <param name="request">The request, as heard or typed.</param>
    /// <returns><see langword="true"/> for « Au revoir », « Bye »…</returns>
    public static bool IsGoodbye(string? request) => Goodbyes.Contains(Plain(request), StringComparer.Ordinal);

    /// <summary>Starts a call; nothing changes when one goes on.</summary>
    /// <returns>Whether it started now.</returns>
    public bool Start()
    {
        lock (gate)
        {
            if (active)
            {
                return false;
            }

            (active, agent, project, silences) = (true, null, null, 0);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Ends the call: the next request goes to the Jarvis agent, and the wake word is needed again.</summary>
    /// <returns>Whether one was going on.</returns>
    public bool End()
    {
        lock (gate)
        {
            if (!active)
            {
                return false;
            }

            (active, agent, project, silences) = (false, null, null, 0);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Keeps who answered a turn of the call, for the next one.</summary>
    /// <param name="answeredBy">The agent that ended the turn; <see langword="null"/> for the Jarvis agent.</param>
    /// <param name="about">The project of the turn; <see langword="null"/> keeps the one of the call.</param>
    public void Keep(string? answeredBy, string? about)
    {
        lock (gate)
        {
            if (!active || (agent == answeredBy && (about is null || about == project)))
            {
                return;
            }

            agent = answeredBy;
            project = about ?? project;
        }

        Changed?.Invoke();
    }

    /// <summary>Counts what Jarvis heard after listening by itself: a silence, or words that start the count again.</summary>
    /// <param name="heard">Whether the user said something.</param>
    public void Heard(bool heard)
    {
        lock (gate)
        {
            silences = heard ? 0 : silences + 1;
        }
    }

    private static string Plain(string? text)
    {
        var builder = new StringBuilder();
        foreach (var character in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }
}
