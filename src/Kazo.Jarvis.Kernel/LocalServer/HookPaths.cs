namespace Kazo.Jarvis.Kernel.LocalServer;

/// <summary>
/// The paths of the hook routes (ADR 0013, section 7). In the kernel because the slice that serves them, the one
/// that writes them into the user's Claude Code settings and the one that starts sessions must agree on them.
/// </summary>
public static class HookPaths
{
    /// <summary>What every hook path starts with; the token of the user's hooks opens nothing else.</summary>
    public const string Prefix = "/hook/";

    /// <summary><c>SessionStart</c>: links the session to its task.</summary>
    public const string Context = "/hook/task.context";

    /// <summary><c>UserPromptSubmit</c>: the task works.</summary>
    public const string Start = "/hook/task.start";

    /// <summary><c>PermissionRequest</c>: the task waits for an approval.</summary>
    public const string Approval = "/hook/approval.requested";

    /// <summary><c>Stop</c>: the task is idle.</summary>
    public const string Idle = "/hook/task.idle";

    /// <summary><c>SubagentStop</c>: a sub-task is done.</summary>
    public const string SubtaskIdle = "/hook/subtask.idle";
}
