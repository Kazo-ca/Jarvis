using Kazo.Jarvis.Kernel.Conversation;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// Where a message written in a thread is shown today (ADR 0026, sections 4 and 6). A message keeps the address
/// it was written at, and the tasks move when a folder gets its project: a message that answers a task follows
/// that task; a message to a subject of « Général » whose tasks left for a project follows them.
/// </summary>
public static class ThreadHomes
{
    /// <summary>The messages shown in the thread of a subject, oldest first.</summary>
    /// <param name="overview">The projects, their subjects and their tasks.</param>
    /// <param name="project">The project of the subject.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="messages">Every message written in a thread.</param>
    /// <returns>Those whose home is this subject.</returns>
    public static IReadOnlyList<ThreadMessage> Of(ProjectsOverview overview, ProjectBoard project, SubjectBoard subject, IReadOnlyList<ThreadMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(messages);
        return [.. messages.Where(message => HomeOf(overview, message) == (project.Id, subject.Key)).OrderBy(message => message.At)];
    }

    /// <summary>The subject a message is shown in.</summary>
    /// <param name="overview">The projects, their subjects and their tasks.</param>
    /// <param name="message">The message.</param>
    /// <returns>The project and the key of the subject; the address of the message when nothing moved.</returns>
    public static (string Project, string Subject) HomeOf(ProjectsOverview overview, ThreadMessage message)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(message);
        var address = message.Address;
        var written = (address.Project, address.Subject);

        // 1. An answer to a task is where the task is.
        if (address.Task is { } task)
        {
            foreach (var project in overview.Projects)
            {
                if (project.Subjects.FirstOrDefault(subject => subject.Steps.Any(step => step.Task.Id == task)) is { } holder)
                {
                    return (project.Id, holder.Key);
                }
            }
        }

        // 2. The subject still exists where the message was written.
        if (overview.Find(address.Project)?.Subjects.Any(subject => subject.Key == address.Subject) == true || address.Project != ProjectsOverview.GeneralId)
        {
            return written;
        }

        // 3. Its tasks left « Général » for a project: the message goes with the task that was the newest when
        //    it was written, or with the oldest one when it was written before them all.
        var moved = overview.Projects
            .Where(project => !project.General)
            .SelectMany(project => project.Subjects.SelectMany(subject => subject.Steps
                .Where(step => ProjectsOverview.GeneralKey(step.Task) == address.Subject)
                .Select(step => (Project: project.Id, Subject: subject.Key, step.Task.CreatedAt))))
            .OrderBy(step => step.CreatedAt)
            .ToList();
        if (moved.Count == 0)
        {
            return written;
        }

        var current = moved.LastOrDefault(step => step.CreatedAt <= message.At);
        var home = current.Project is null ? moved[0] : current;
        return (home.Project, home.Subject);
    }
}
