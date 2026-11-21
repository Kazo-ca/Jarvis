using Kazo.Jarvis.Kernel.LocalServer;

namespace Kazo.Jarvis.Features.Tasks.Board;

/// <summary>One of the five <c>/hook/…</c> routes of the local server (ADR 0013, section 7), handled by <see cref="TaskHooks"/>.</summary>
public sealed class TaskHookRoute : ILocalRoute
{
    private readonly TaskHooks hooks;

    /// <summary>Creates the route of one hook.</summary>
    /// <param name="kind">Which hook.</param>
    /// <param name="hooks">The handler.</param>
    public TaskHookRoute(TaskHookKind kind, TaskHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        Kind = kind;
        this.hooks = hooks;
    }

    /// <summary>Which hook this route serves.</summary>
    public TaskHookKind Kind { get; }

    /// <inheritdoc />
    public string Path => PathOf(Kind);

    /// <summary>The path of a hook, a name of ADR 0013 that the installed hooks carry.</summary>
    /// <param name="kind">Which hook.</param>
    /// <returns>The path, starting with <c>/hook/</c>.</returns>
    public static string PathOf(TaskHookKind kind) => kind switch
    {
        TaskHookKind.Context => HookPaths.Context,
        TaskHookKind.Start => HookPaths.Start,
        TaskHookKind.Approval => HookPaths.Approval,
        TaskHookKind.Idle => HookPaths.Idle,
        TaskHookKind.SubtaskIdle => HookPaths.SubtaskIdle,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <inheritdoc />
    public ValueTask<LocalResponse> HandleAsync(LocalRequest request, CancellationToken cancellationToken) =>
        hooks.HandleAsync(Kind, request, cancellationToken);
}
