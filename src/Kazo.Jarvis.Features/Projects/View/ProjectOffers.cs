using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// What the projects view keeps (ADR 0026, sections 6 and 11): from how much work Jarvis offers to make a project
/// of a subject of « Général », and the offers the user refused or put off.
/// </summary>
public sealed record ProjectsViewOptions
{
    /// <summary>The settings section.</summary>
    public static SettingsSection<ProjectsViewOptions> Section { get; } = new("Projects.View", () => new ProjectsViewOptions(), Validate);

    /// <summary>The tasks of a subject, in <see cref="OfferDays"/> days, from which a project is offered.</summary>
    public int OfferTasks { get; init; } = 4;

    /// <summary>The messages written in the thread of a subject, in <see cref="OfferDays"/> days, from which a project is offered.</summary>
    public int OfferMessages { get; init; } = 30;

    /// <summary>The days counted.</summary>
    public int OfferDays { get; init; } = 7;

    /// <summary>The days an offer put off stays hidden.</summary>
    public int LaterDays { get; init; } = 7;

    /// <summary>The subjects of « Général » whose offer was refused: never offered again.</summary>
    public IReadOnlyList<string> Refused { get; init; } = [];

    /// <summary>The subjects whose offer was put off, and until when.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Later { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>The most subjects the user may make.</summary>
    public const int MostSubjects = 200;

    /// <summary>The most characters in the name of a subject.</summary>
    public const int MostNameCharacters = 60;

    /// <summary>The subjects the user made with « + Nouveau sujet » (ADR 0026, section 11, point 2).</summary>
    public IReadOnlyList<UserSubject> Subjects { get; init; } = [];

    private static IReadOnlyList<string> Validate(ProjectsViewOptions value)
    {
        var problems = new List<string>();
        if (value.Subjects is null || value.Subjects.Count > MostSubjects
            || value.Subjects.Any(subject => subject is null || string.IsNullOrWhiteSpace(subject.Project) || string.IsNullOrWhiteSpace(subject.Key)
                || string.IsNullOrWhiteSpace(subject.Name) || subject.Name.Length > MostNameCharacters)
            || value.Subjects.Select(subject => (subject?.Project, subject?.Key)).Distinct().Count() != value.Subjects.Count)
        {
            problems.Add($"At most {MostSubjects} subjects, each with a project, a key of its own and a name of 1 to {MostNameCharacters} characters.");
        }

        if (value.OfferTasks is < 1 or > 1000 || value.OfferMessages is < 1 or > 10000)
        {
            problems.Add("A project is offered from 1 to 1000 tasks or from 1 to 10000 messages.");
        }

        if (value.OfferDays is < 1 or > 365 || value.LaterDays is < 1 or > 365)
        {
            problems.Add("The days counted and the days an offer is put off go from 1 to 365.");
        }

        if (value.Refused is null || value.Later is null)
        {
            problems.Add("The refused and postponed offers are missing.");
        }

        return problems;
    }
}

/// <summary>The offer to make a project of a subject of « Général ».</summary>
/// <param name="Subject">The key of the subject.</param>
/// <param name="Name">The name proposed for the project.</param>
/// <param name="Folder">The folder proposed: where its tasks work.</param>
/// <param name="Tasks">Its tasks in the days counted.</param>
/// <param name="Messages">The messages written in its thread in the days counted.</param>
public sealed record ProjectOffer(string Subject, string Name, string Folder, int Tasks, int Messages)
{
    /// <summary>
    /// The offers to show: a subject of « Général » that took <see cref="ProjectsViewOptions.OfferTasks"/> tasks
    /// or <see cref="ProjectsViewOptions.OfferMessages"/> messages in the days counted, whose tasks work in a
    /// folder, and whose offer was neither refused nor put off. Jarvis never creates a project by itself.
    /// </summary>
    /// <param name="overview">The projects, their subjects and their tasks.</param>
    /// <param name="messages">Every message written in a thread.</param>
    /// <param name="options">The thresholds and the answers of the user.</param>
    /// <param name="now">The time.</param>
    /// <returns>The offers, in the order of the subjects.</returns>
    public static IReadOnlyList<ProjectOffer> For(ProjectsOverview overview, IReadOnlyList<ThreadMessage> messages, ProjectsViewOptions options, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        var since = now - TimeSpan.FromDays(options.OfferDays);
        var offers = new List<ProjectOffer>();
        foreach (var subject in overview.General.Subjects)
        {
            if (options.Refused.Contains(subject.Key, StringComparer.Ordinal)
                || (options.Later.TryGetValue(subject.Key, out var until) && until > now))
            {
                continue;
            }

            // The folder most of its tasks work in; a subject with no folder cannot become a project by itself.
            var folder = subject.Steps
                .Where(step => !string.IsNullOrWhiteSpace(step.Task.Folder))
                .GroupBy(step => step.Task.Folder!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .FirstOrDefault()?.Key;
            if (folder is null)
            {
                continue;
            }

            var tasks = subject.Steps.Count(step => step.Task.CreatedAt >= since);
            var written = messages.Count(message => message.At >= since && ThreadHomes.HomeOf(overview, message) == (ProjectsOverview.GeneralId, subject.Key));
            if (tasks >= options.OfferTasks || written >= options.OfferMessages)
            {
                offers.Add(new ProjectOffer(subject.Key, subject.Name ?? subject.Key, folder, tasks, written));
            }
        }

        return offers;
    }
}
