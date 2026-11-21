# Tasks and Claude Code sessions: task shape, board, list of the sessions

Work item `stage2/11-tasks-and-sessions`, **first part** (US2.11, US2.14). Decisions:
[ADR 0013](adr/0013-relais-readiness.md), section 6, and [ADR 0021](adr/0021-claude-code-sessions.md),
sections 6, 7 and 10. The second part of the work item, the local server of the `/hook/…` routes and the
installation of the hooks in the user's settings, is not done: see "What is left of the work item".

## What is built

```
%USERPROFILE%\.claude\sessions\<pid>.json ─► Adapters/ClaudeCode/ClaudeSessionRegistry
   (read only, never *.key nor ide/*.lock)            │
                                                      ▼
                                   IClaudeSessionRegistry (kernel)
                                                      │
ClaudeCode/Sessions ─► ClaudeSessionList: a reading every 5 s, grouping by project ─► "Claude sessions" page
                                                      │
Tasks/Board ─► ITaskBoard (kernel) ─► collection Tasks.Board of jarvis.db
   the task a session carries, when Jarvis follows it
```

The slices `Tasks/Board` and `ClaudeCode/Sessions` do not know each other: `ITaskBoard` links them.

## Shape of a task (`Kernel/Tasks`)

That of Relais (ADR 0013, section 6). The type is named `WorkTask`, so that it is never mistaken for a .NET
`Task`.

| Field | Role |
| --- | --- |
| `Id` | Identifier (`task-…`), given to a session as `RELAIS_TASK_ID` |
| `ParentId` | Empty for a task, set for a sub-task. **One level only** |
| `Project`, `Topic` | Project key and topic key |
| `Title`, `Summary` | Title; what the task says of itself, or the cause when it is blocked |
| `Operator` | Account (`claude-code`) and session |
| `Source` | `harness`, `hook`, `mcp`, `orchestrator` |
| `Status` | One of the nine statuses |
| `CreatedAt`, `UpdatedAt` | Dates |
| `Folder` | Folder of the work. **An addition of Jarvis** to the shape, for the list of the sessions |

**The nine statuses, and no other:** `pending`, `working`, `paused`, `idle`, `waiting_answer`,
`waiting_approval`, `blocked`, `done`, `abandoned`. `waiting_answer` and `waiting_approval` wait for the human
(amber in the fan); `done` and `abandoned` end the task.

### Keys (`TaskKeys`)

- **Project:** the address of the Git remote, normalized to `host/owner/name` in lower case, whatever its form
  (`https://github.com/Kazo-ca/Jarvis.git`, `git@github.com:Kazo-ca/Jarvis`). A user name or a password in the
  address never enters the key. Without a remote: the name of the folder.
- **Topic:** `branch:<name>`; `_inbox` for `main`, `master`, a detached head or a folder without Git. The keys
  `pr:<n>` and `issue:<n>` are not found at stage 2.

## Board of the tasks (`Tasks/Board`, `ITaskBoard`)

- `CreateAsync`: a task is born `pending` (ADR 0021, section 5). The sub-task of a sub-task hangs on the task.
- `ChangeAsync`: status, summary, session of the operator, title; what is not named stays. **A task that ended
  no longer moves**: a late hook is ignored. A change that changes nothing is not announced.
- `Find`, `FindBySession`, `Tasks` (the most recently changed first), `RemoveEndedAsync` (the tasks that ended
  before a date, with their sub-tasks; never a task in progress).
- Storage: one document per task in the collection `Tasks.Board` of `jarvis.db`, read again at start. A summary
  is cut at 2,000 characters, a title at 200.
- The tasks in memory are the truth while Jarvis runs: "Erase everything" empties the store, and a task still
  in progress stays on the board until Jarvis stops.

In this part nothing creates a task yet: the hooks (second part) and the launch of a session (work item
`stage2/12`) will.

## Registry of the sessions (`IClaudeSessionRegistry`, `Adapters/ClaudeCode`)

- Reads `%USERPROFILE%\.claude\sessions\<pid>.json` (spikes E2 and E3). A session of code of Claude Desktop is
  a Claude Code process like the others (`entrypoint: claude-desktop`).
- **Opens only the `.json` files placed directly in `sessions`.** Never `sessions/*.key`, never `ide/*.lock`,
  never anything else of the `.claude` folder: a test sends every file opening through a witness and proves it.
  **Writes nothing.**
- The format is internal to Claude Code: only `sessionId`, `pid` and `cwd` are required; any other field may be
  missing; an unknown state shows as "State unknown"; a file that cannot be read or is half written is skipped.
- A session whose process ended is not listed.
- The remote `origin` and the branch come from the `.git` folder of the working folder or of a parent, read
  directly (`HEAD`, `config`), without running Git; worktrees (`gitdir:`, `commondir`) are followed.

## List of the sessions (`ClaudeCode/Sessions`)

Section `ClaudeCode.Sessions`:

| Setting | Default | Role |
| --- | --- | --- |
| `Enabled` | yes | Read the registry. No: Jarvis does not look at the folder of Claude Code |
| `PollSeconds` | 5 | Time between two readings, from 2 to 60 seconds |

- `ClaudeSessionList` reads at a regular interval, groups **by project and not by path** (two clones of the
  same repository are one project), puts first the projects and the sessions that wait, then sorts by last
  activity. The page is told of a change only.
- A session Jarvis follows as a task (`FindBySession`) carries that task: its status shows, and a `waiting_…`
  task makes the session wait even when the registry says "working".
- No registry (Claude Code never ran) and a registry that cannot be read are said apart. **Never a fault of the
  assistant.**
- **Jarvis only looks** at these sessions: it never answers in their place (US2.15).

### "Claude sessions" page

After "Home Assistant". The switch, what Jarvis does and does not do with that folder, the list by project
(name, state and reason of the wait, folder, origin "Started by hand", "Claude Desktop" or "Started by Jarvis",
topic, last activity, status of the task), the time between two readings.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `WorkTask`, `NewWorkTask`, `TaskOperator`, `WorkTaskStatus`, `TaskSource`, `TaskWire` (`Kernel/Tasks`) | The shape of a task and the names of the spec |
| `TaskKeys` | Project key, topic key |
| `ITaskBoard`, `TaskChange` | Create, change, find, list the tasks |
| `IClaudeSessionRegistry`, `ClaudeSession`, `ClaudeSessionListing`, `ClaudeSessionState` (`Kernel/ClaudeCode`) | The running sessions, a native service of the host |

## Web host

`SimulatedClaudeSessions`: three sessions (two clones of `Kazo-ca/bar` in two folders, one of them from Claude
Desktop, and a folder without Git). The button "Make the Claude Code sessions move" of the test page advances
the scenario: a session waits, then no session at all, then no registry.

## What is left of the work item

Second part, branch `stage2/11-hook-server`:

- the local server, listening on `127.0.0.1` only, with a token, for the routes `/hook/task.context`,
  `/hook/task.start`, `/hook/approval.requested`, `/hook/task.idle`, `/hook/subtask.idle` (ADR 0021, section 3),
  which create and move the tasks of the board;
- the installation and the removal of the hooks in `%USERPROFILE%\.claude\settings.json`, on the user's request
  only, with a preview of the lines and a dated copy (ADR 0021, section 6);
- the check the ADR asks for: a hook whose server does not answer must neither block nor slow the session of
  the user.

## Provisional choices of this work item

The ADR does not settle them; to be confirmed by Benoit:

1. **The reading of the registry is on at first**: it is local, writes nothing and needs no setting
   (recommendation of spike E2). A switch turns it off.
2. **A reading every 5 seconds**, adjustable, rather than watching the folder: simpler, and a reading of 14
   sessions took 73 ms.
3. **A session without a task does not become a task**: it shows with the state of the registry (ADR 0021,
   section 6), and will not appear in the fan until its hooks are installed.
4. **The last message of a session is not read** in this part: the transcript serves the summary of work item
   `stage2/13`.
5. **The tasks of a previous run that did not end stay as they are at start.** Work item 12 will decide about
   those whose process is gone.

## Not verified

- **Nothing ran on Windows**: the real registry was not read by this code (spike E2 read it with other code).
  The `waiting` state was never observed live, even in the spike.
- The check that a process is alive compares the process number only, not `procStart`: a number reused by
  another program would make an ended session whose file stayed look alive.

## Tests

- Unit: `TaskKeysTests`, `TaskBoardTests`, `ClaudeSessionRegistryTests` (among them "the files that hold
  secrets are never opened"), `ClaudeSessionListTests`.
- Playwright: `ClaudeSessionsTests` (list by project; a session that starts to wait; switch and interval).

## Windows checks

To do with Benoit:

1. Open the page with a session in a terminal and one in Claude Desktop: both appear, under the right project.
2. Two clones of the same repository: one project.
3. Let a session ask for a permission: the line turns to "Waiting for you", with the reason.
4. Close a session: it disappears within a few seconds.
5. Turn the switch off: the list empties.
