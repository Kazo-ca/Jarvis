using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// A few Claude Code sessions in memory, in place of the registry of the user (ADR 0003, section 3): two clones
/// of the same repository in two folders, one of them started by Claude Desktop, and a folder without Git. The
/// test page makes them move: a session starts to wait, then every session ends, then Claude Code was never there.
/// </summary>
public sealed class SimulatedClaudeSessions : IClaudeSessionRegistry
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly Lock gate = new();
    private int step;

    /// <summary>How many times the registry was read.</summary>
    public int Reads { get; private set; }

    /// <summary>The step of the scenario: 0 at first, 1 a session waits, 2 no session, 3 no registry; then 0 again.</summary>
    public int Step
    {
        get
        {
            lock (gate)
            {
                return step;
            }
        }
    }

    /// <summary>Goes to the next step of the scenario.</summary>
    public void Advance()
    {
        lock (gate)
        {
            step = (step + 1) % 4;
        }
    }

    /// <summary>Back to the first step.</summary>
    public void Reset()
    {
        lock (gate)
        {
            step = 0;
        }
    }

    /// <inheritdoc />
    public ValueTask<ClaudeSessionListing> ReadAsync(CancellationToken cancellationToken = default)
    {
        int now;
        lock (gate)
        {
            Reads++;
            now = step;
        }

        return ValueTask.FromResult(now switch
        {
            3 => ClaudeSessionListing.None,
            2 => new ClaudeSessionListing(true, []),
            _ => new ClaudeSessionListing(true,
            [
                new ClaudeSession("sess-bar-cli", 4101, @"C:\src\kazo-bar", "kazo-bar", ClaudeSessionState.Busy, null, Start.AddMinutes(12), "cli",
                    "https://github.com/Kazo-ca/bar.git", "feature/tray-menu"),
                new ClaudeSession("sess-bar-desktop", 4102, @"C:\src\kazo-bar-review", "Relire la PR du menu", now == 1 ? ClaudeSessionState.Waiting : ClaudeSessionState.Idle,
                    now == 1 ? "permission to run Bash" : null, Start.AddMinutes(now == 1 ? 20 : 5), ClaudeSession.DesktopEntrypoint,
                    "git@github.com:Kazo-ca/Bar.git", "main"),
                new ClaudeSession("sess-notes", 4103, @"C:\Users\demo\Notes", "Notes", ClaudeSessionState.Idle, null, Start.AddMinutes(8), "cli"),
            ]),
        });
    }
}
