using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// A Claude Code session the user started by hand, as its hooks would tell it (ADR 0021, section 6): the first
/// hook creates its task, then it works, waits for a permission and stops. The Web host has no session to send
/// hooks, so the buttons of the test page move the board the way the hook routes do.
/// </summary>
public sealed class SimulatedHandSession
{
    /// <summary>The folder of the session.</summary>
    public const string Folder = "/home/demo/projets/kazo-bar";

    private readonly ITaskBoard board;
    private int sessions;

    /// <summary>Creates the simulation.</summary>
    /// <param name="board">The board the hooks move.</param>
    public SimulatedHandSession(ITaskBoard board) => this.board = board;

    /// <summary>The session works: its task is created by this first hook when there is none.</summary>
    /// <returns>A task that completes when the board is moved.</returns>
    public async Task WorkAsync() => await board.ChangeAsync((await CurrentAsync()).Id, new TaskChange(WorkTaskStatus.Working));

    /// <summary>The session waits for a permission.</summary>
    /// <returns>A task that completes when the board is moved.</returns>
    public async Task WaitAsync() => await board.ChangeAsync((await CurrentAsync()).Id, new TaskChange(WorkTaskStatus.WaitingApproval));

    /// <summary>The session ends its turn, with a last message.</summary>
    /// <returns>A task that completes when the board is moved.</returns>
    public async Task StopAsync() => await board.ChangeAsync((await CurrentAsync()).Id, new TaskChange(WorkTaskStatus.Idle, "Le pied de page est corrigé."));

    /// <summary>The session starts a sub-agent: a sub-task of its task, shown inside its panel of the fan.</summary>
    /// <returns>A task that completes when the board is moved.</returns>
    /// <summary>
    /// The session is over: its task is done, and the next press starts another session in the same folder. The
    /// task takes a title of its own, so that the session in progress stays the only one of its title.
    /// </summary>
    /// <returns>A task that ends once the board changed.</returns>
    public async Task EndAsync()
    {
        var current = await CurrentAsync();
        await board.ChangeAsync(current.Id, new TaskChange(WorkTaskStatus.Done, "Le pied de page est corrigé.", Title: "session terminée " + sessions));
    }

    public async Task SubTaskAsync()
    {
        var parent = await CurrentAsync();
        await board.CreateAsync(new NewWorkTask(
            "cherche les pieds de page",
            parent.Project,
            parent.Topic,
            new TaskOperator(TaskOperator.ClaudeCode),
            TaskSource.Hook,
            Folder,
            parent.Id,
            WorkTaskStatus.Working));
    }

    private async Task<WorkTask> CurrentAsync()
    {
        var session = "hand-" + sessions;
        if (board.FindBySession(TaskOperator.ClaudeCode, session) is { Ended: false } known)
        {
            return known;
        }

        session = "hand-" + Interlocked.Increment(ref sessions);
        return await board.CreateAsync(new NewWorkTask(
            "corrige le pied de page",
            TaskKeys.Project(null, Folder),
            TaskKeys.Inbox,
            new TaskOperator(TaskOperator.ClaudeCode, session),
            TaskSource.Hook,
            Folder,
            Status: WorkTaskStatus.Idle));
    }
}
