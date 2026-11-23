# ADR 0026: the view of projects, subjects and their threads

## Status

**Accepted** by Benoit on 2026-11-22 (work item `stage2/adr-0026-projects-view`), from his six answers of the
same day about the mockups, then his answer "ça me va" (fine by me) to the last three points (section 11).

- **Completes:** [ADR 0013](0013-relais-readiness.md) (the Relais hierarchy Project → Subject → Task → Sub-task
  does not change; this ADR says how the screen shows it and where people talk) and
  [ADR 0025](0025-task-agents-and-their-memories.md) (a project is a local folder created in Jarvis, with its
  agent and its memory).
- **Changes:** the full screen of the [UI catalog](../ui/catalog.md) (the "À traiter" column becomes a rail
  entry, section 7).
- **Builds on:** [ADR 0027](0027-orb-gestures.md) (the double click on the orb asks for the projects view, the
  right click opens the task fan).
- **Built:** right after ADR 0025 (answer 6), which defines the project.

## Context

The full screen validated on 19 November shows what waits for Benoit, an open item and a search. It does not
show **where** each conversation lives. Benoit: "the interface where we see all subjects / projects /
conversations… add this concept and the conversation architecture on two levels".

He gave the work in progress as the example: "the project is Jarvis, the subject is 'Conception vue projets',
the task would be 'Préparation documentation'. You are in the sub-task 'Poser des questions', you just finished
the task 'Lire l'image', then 'Créer une maquette'." On a subject he wants "a clear view of the steps done, to
see and re-read what was done in each finished task, and to see what is planned next".

Mockups: [project rail](../../../../design/images/projects-rail.jpg),
[all projects](../../../../design/images/projects-all.jpg),
[subject thread](../../../../design/images/projects-subject-thread.jpg),
[from Général to a project](../../../../design/images/projects-general-to-project.jpg)
(prototypes in [`design/prototypes/`](../../../../design/prototypes/)).

## Decision

### 1. Four levels of work, two levels of conversation (answer 1)

Work keeps the four Relais levels: project, subject, task, sub-task. Conversation has only two:

| Thread | With whom | What it shows |
| --- | --- | --- |
| **Subject thread** | the project agent (ADR 0025) and whoever does the tasks | the user's messages, the answers addressed to them, the start and end of each task |
| **Task thread** | the agent doing the task | all the work of the task, its summary, its files, its sub-tasks |

A sub-task has no thread one writes in: it is followed read-only inside its task (answer 4, Relais rule
unchanged).

On screen the subject is a label (branch, PR, issue) and a thread, not one more screen between the project and
the tasks.

### 2. The project thread (answer 2)

Each project has **one thread**, with the project agent. The subjects' conversations show there too, as in a
Claude project: a word from the agent when a subject moves, with a link to the subject.

### 3. The tasks of a subject

At the top of the subject thread, a strip lists its tasks in order, numbered `T1`, `T2`…:

- **done** (✓, green): a click opens their thread to re-read what was done;
- **in progress** (●): **always visible**, with their sub-tasks on one line; amber when they wait for the user;
- **planned** (○, dashed): what the agent intends to do next.

"Tout voir" unfolds the whole list when it does not fit on one line. Done tasks can fold; tasks in progress
never do.

### 4. Answering a task in the subject thread

One answers a task **in the subject thread**, as one answers someone in an mIRC channel:
`T3> yes, you are right, let's do that`. The message shows with the `T3` label; a click on the label opens that
task's thread. The message is also stored in the task thread.

- Without a prefix, the message goes to the project agent, in the subject thread.
- By voice, "for task 3, …" means `T3>`.
- Each task message in the subject thread carries its label, author and time, on one line.

### 5. The thread of a task

A click on a task opens its thread in the right panel: status, agent, duration, summary, read-only sub-tasks,
files, task messages, and the full log on request. "Retour au sujet" closes it.

### 6. The "Général" project (answer 3)

Conversations without a project (weather, mail…) go to a built-in **Général** project that cannot be deleted.
Its subjects have the same threads as the others.

When Jarvis sees that a Général subject takes a lot of room, **it offers to make it a project**: name, proposed
folder, and "the subject, its tasks and their threads move to the new project". The user creates, changes the
folder, postpones or refuses; Jarvis never creates a project on its own. A refusal is not offered again for
that subject.

### 7. The layout (answer 5)

The full screen keeps the status bar and takes four columns:

1. **A rail**: ◆ À traiter (all projects, same number as the orb badge), ▦ All projects, one icon per project
   with its badge, Général, + New project.
2. **The project panel**: folder, agent, project thread, active subjects (label and done / in progress /
   planned bar), finished subjects.
3. **Centre**: the path Project › Subject, the subject thread, the task strip, and the field to talk or type
   with the agent choice (ADR 0025, section 4).
4. **Right**: the thread of the open task; otherwise "Retrouver", filtered on the open project (Ctrl+K stays
   global).

"All projects" shows one card per project: what waits, what runs with the sub-task progress, the last word of
the thread. The "folder without a project" request of ADR 0025 (section 6) has its card there.

### 8. Opening the view from the orb

The gestures are set by [ADR 0027](0027-orb-gestures.md): a double click on the orb sends
`ProjectsViewRequested`, and the slice of this view answers by opening the full screen on the last open
project (provisional). The right click opens the task fan, the left click starts listening.

### 9. What does not change

The Relais model (statuses, automatic attachment of a session to a subject), the approval requests
(ADR 0023), the task fan (ADR 0010, opened by the right click since ADR 0027) and the orb badge.

### 10. Consequences

- Jarvis tasks take the Relais shape now: each `WorkTask` has a project and a subject; what has none goes to
  Général.
- A Claude Code session followed by Jarvis becomes a task of its subject; its declared steps become its
  sub-tasks.
- The full screen becomes one more Razor page (its own slice); the task fan does not change.

### 11. The last three points (answer "ça me va", recommendations taken)

1. **The subject thread** shows only what is addressed to the user, plus the start and end of each task.
   What the agents say to each other stays in each task's thread.
2. **A subject** is created by Jarvis, from the branch or the request (as in Relais), and also by the user with
   "+ Nouveau sujet".
3. **Jarvis offers to make a project** of a Général subject from 4 tasks or 30 messages in a week. The
   threshold is adjustable.
