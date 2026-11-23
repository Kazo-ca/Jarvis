using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Features.Tasks.Fan;

/// <summary>
/// What the fan shows (ADR 0010, sections 1 and 2; ADR 0013, section 6): one panel per task that is not over,
/// its sub-tasks inside it, amber when it waits for the user, and the open confirmation request in the panel of
/// the task that asks, or in a panel of its own when no task does.
/// </summary>
/// <param name="Panels">The panels on the arc, the ones that wait first.</param>
/// <param name="Tasks">How many tasks are not over, shown or not.</param>
/// <param name="Decisions">How many decisions wait for the user.</param>
/// <param name="Hidden">How many tasks have no panel because the arc is full.</param>
public sealed record FanContent(IReadOnlyList<FanPanel> Panels, int Tasks, int Decisions, int Hidden)
{
    /// <summary>How many sub-tasks a panel lists.</summary>
    public const int MostSubTasks = 2;

    /// <summary>Builds the content from the board and from the open request.</summary>
    /// <param name="tasks">The tasks of the board.</param>
    /// <param name="open">The confirmation request that waits, or <see langword="null"/>.</param>
    /// <returns>The content.</returns>
    public static FanContent Of(IReadOnlyList<WorkTask> tasks, OpenConfirmation? open)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        // The ones that wait first, then the most recently changed.
        var current = tasks
            .Where(task => task.ParentId is null && !task.Ended)
            .OrderByDescending(task => task.WaitsForHuman)
            .ThenByDescending(task => task.UpdatedAt)
            .ToList();
        var asker = open?.Request.TaskId is { } id ? current.FirstOrDefault(task => task.Id == id) : null;
        var apart = open is not null && asker is null;

        var panels = new List<FanPanel>();
        if (apart)
        {
            panels.Add(new FanPanel("decision:" + open!.Id.ToString("N"), null, [], 0, open));
        }

        // The task that asks always has its panel: its buttons are there.
        var ordered = asker is null ? current : [asker, .. current.Where(task => task.Id != asker.Id)];
        foreach (var task in ordered.Take(FanLayout.MostPanels - panels.Count))
        {
            var children = tasks
                .Where(child => child.ParentId == task.Id && !child.Ended)
                .OrderByDescending(child => child.UpdatedAt)
                .ToList();
            panels.Add(new FanPanel(
                "task:" + task.Id,
                task,
                [.. children.Take(MostSubTasks)],
                Math.Max(0, children.Count - MostSubTasks),
                asker is not null && task.Id == asker.Id ? open : null));
        }

        var waiting = current.Count(task => task.WaitsForHuman);
        var asksWithoutWaiting = asker is { WaitsForHuman: false };
        return new FanContent(
            panels,
            current.Count,
            waiting + (apart || asksWithoutWaiting ? 1 : 0),
            current.Count - panels.Count(panel => panel.Task is not null));
    }
}

/// <summary>One panel of the fan.</summary>
/// <param name="Key">Stable from one drawing to the next.</param>
/// <param name="Task">The task, or <see langword="null"/> for a request no task asks.</param>
/// <param name="SubTasks">The sub-tasks listed in the panel.</param>
/// <param name="MoreSubTasks">How many sub-tasks are not listed.</param>
/// <param name="Decision">The confirmation request shown in this panel, with its buttons.</param>
public sealed record FanPanel(string Key, WorkTask? Task, IReadOnlyList<WorkTask> SubTasks, int MoreSubTasks, OpenConfirmation? Decision)
{
    /// <summary>Whether the panel waits for a decision: amber, and its line too (ADR 0010, section 1).</summary>
    public bool Amber => Decision is not null || Task is { WaitsForHuman: true };
}
