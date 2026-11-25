# Projects view

Work item `stage2/24-projects-view`. Decision: [ADR 0026](adr/0026-projects-subjects-and-their-threads.md),
sections 3, 5, 7 and 8; [ADR 0027](adr/0027-orb-gestures.md) for the double click. This first work item
**reads** the task board and the catalog of projects. Work item `stage2/26-subject-thread-writing` adds
writing in the thread of a subject (section 4).

![A project, its subject, the band of tasks and the thread of a task](../../../design/screenshots/projects-subject.png)

## What the user sees

- **A double click on the orb** opens the view, over the whole screen on Windows. It opens again where the
  user left it, as long as Jarvis runs.
- **The rail**, on the left: ◆ "To handle" with the number of tasks that wait, ▦ "All projects", one entry per
  project (two letters and its number that waits), 💬 "General", and "+" which opens the "Projects" page of the
  settings.
- **"To handle"** lists the tasks that wait for the user, all projects together, the oldest first. A click
  opens the project, the subject and the thread of the task.
- **"All projects"** shows one card per project: what waits, up to three tasks in progress with the progress of
  their sub-tasks, the last word. A folder where a session works with no project has its card, which opens the
  "Projects" page.
- **The panel of the project**: its folder, the thread of the project (the last word of its subjects), the
  active subjects with their done / in progress / planned bar, then the subjects ended in the last seven days.
- **At the centre**, the subject: the path Project › Subject, the band of tasks numbered `T1`, `T2`… (✓ done,
  ● in progress, ○ planned, ✕ stopped; amber when it waits), the sub-tasks of the tasks in progress on one
  line, then the thread of the subject. "Show all" unfolds the band beyond six tasks; a task in progress always
  stays visible.
- **On the right**, the thread of the open task (status, who works, duration, summary, read-only sub-tasks,
  folder); otherwise "Find", which searches the titles and the summaries of the tasks of the project.

## Where the projects, the subjects and the tasks come from

`ProjectsOverview.From(tasks, catalog)` is a pure function, with no clock and no storage.

| Level | Rule |
| --- | --- |
| Project | The one of the catalog that holds the folder of the task (`IProjectCatalog.OwnerOf`). Every project of the catalog is shown, even with no task |
| General | Built-in project, always last: any task whose folder belongs to no project |
| Subject | In a project: the topic of the task (`WorkTask.Topic`, so the branch). In General: the project key of the task (the repository or the name of the folder), otherwise its topic |
| Task | A task of the board with no parent task, numbered in the order of creation in its subject |
| Sub-task | A task whose parent task exists; never a task of the subject |

`Pending` is "planned", `Done` "done", `Abandoned` "stopped", everything else "in progress". A subject is ended
when all its tasks are done or stopped.

**The thread of the subject** mixes, oldest first, computed lines (one when each task starts, one when it
ends with its summary, one when it waits for the user) and the messages written in the thread.

## Writing in the thread of a subject

Work item `stage2/26-subject-thread-writing` (ADR 0026, section 4).

- Under the thread, an input line. The message leaves as an ordinary typed request (`TextSubmitted`), with
  the address of the thread: `ThreadAddress(Project, Subject, Task, TaskLabel, TaskTitle)`.
- **`T3> oui, on fait comme ça`** answers the task `T3` of the subject (`SubjectAnswer.Parse`): the prefix is
  removed, the message carries the tag `T3` in the thread and also shows in the thread of the task. A task the
  subject does not have is refused and the text stays in the input.
- **Who answers.** In a project of the catalog, the "Projet" agent receives the request directly, with no
  hand-over, with the folder and the memory of the project, and one line that names the subject (and the task,
  with its title). In General, the Jarvis agent answers as in the conversation window, with the same line.
- **Where it is kept.** The turn goes to the journal of the conversation with its address
  (`ConversationTurn.Thread`). `ISubjectThreads` (kernel), held by the `Conversation/Dialogue` slice, gives the
  turns of a thread; the view does not reference the conversation. The same turn therefore also shows in the
  conversation window.
- The state of the assistant takes a text only at rest: during an answer, the view says to try again and keeps
  the text.

The message **is not handed to the Claude Code session** of the task: the local agent answers. Resuming the
session with the message is the next work item.

## Slice `Projects/View`

| File | Role |
| --- | --- |
| `ProjectsOverview` | Projects, subjects, tasks, lines of the thread, from the board and the catalog |
| `ProjectsViewState` | Entry of the rail, open subject and task; kept in memory |
| `ProjectsViewOpener` | Answers `ProjectsViewRequested`: opens the window `Projects.View` |
| `ProjectsView` | The Razor component, its CSS and its texts |

Kernel: `IProjectCatalog.OwnerOf`; `DialogRequest.Maximized`, which the Windows host applies to the window and
the Web host turns into a wider layer. The slice references neither `Projects/Catalog` nor `Tasks/Board`: it
goes through `IProjectCatalog`, `ITaskBoard` and `SettingsWindowRequested`.

## Provisional choices of this work item

To be confirmed by Benoit:

1. The view is an ordinary window opened maximized, with its title bar; not the status bar of the mock-up
   (model, sessions, spend, time).
2. It opens again on the last entry of the rail, forgotten when Jarvis stops; the first time on "All
   projects".
3. The name of a subject is the name of its branch; a task on the main branch goes to "No subject". No tag
   (PR, issue) and no chosen title yet.
4. In General, the tasks of one folder or repository make one subject.
5. "To handle" counts the tasks that wait. The badge of the orb counts the open requests for agreement: the
   two numbers may differ.
6. "+" and the card of a folder with no project open the "Projects" page of the settings.
7. "Find" searches the tasks of the open project only; no Ctrl+K.
8. The agent shown is always "Project" (ADR 0025: one project agent).
9. A message written in a thread is an ordinary turn of the conversation, tagged: there is no separate
   conversation per subject, and the conversation memory sees it like the others.
10. `T3>` sent to a task does not reach the Claude Code session yet: the local agent answers, knowing the
    task.
11. No choice of the agent in the thread, and nothing by voice ("pour la tâche 3").

## Not built yet

- Handing a `T3>` message to the Claude Code session of the task (resuming it with that message).
- Speaking in the thread of a subject, "pour la tâche 3" (section 4); the choice of the agent.
- The files of a task, the full log (section 5).
- The thread of the project as a conversation with the agent of the project (section 2).
- "+ New subject", the archives (section 11).
- The offer to make a project of a subject of General (section 6).

## Not verified

- Nothing ran on Windows: the maximized window, the real double click and its delay.
- No real Claude Code session: the tasks come from the simulated sessions of the Web host.

## Tests

- Unit: `ProjectsOverviewTests` (grouping, numbers, thread, General, folder with no project);
  `SubjectAnswerTests` (prefix `T3>`); `ConversationEngineTests` (the "Projet" agent answers in the thread of a
  project, the line of the subject and of the task, the messages of a thread); `AssistantStateMachineTests`
  (the address follows the turn).
- Playwright: `ProjectsViewTests` (double click, session with no project, task that waits, project created
  from the view, message written in the thread of a subject and `T1>`).

## Windows checks

1. Double click on the orb: the view opens maximized; a single click still starts listening after the delay.
2. Start Claude Code in a folder of a project: the task appears in the project, under the subject of its
   branch.
3. A permission is asked: the rail and "To handle" count it, a click opens the thread of the task.
