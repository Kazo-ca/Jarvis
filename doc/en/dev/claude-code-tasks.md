# Claude Code tasks: "Ask Claude Code to…", folder, permissions, end of a session

Work item `stage2/12-claude-code-tasks`, **second part** of work item 12 (US2.9, US2.10, US2.15). Decisions:
[ADR 0021](adr/0021-claude-code-sessions.md), sections 1, 2, 4, 5 and 8, and
[ADR 0020](adr/0020-hand-off-targets-and-spend-cap.md), section 6. The first part
([claude-code-launch.md](claude-code-launch.md)) built the launch of the process and the `approve` tool;
this one connects them to the conversation.

## What the user sees

- "Ask Claude Code to…" (or « Demande à Claude Code de… »), spoken or typed: Jarvis answers "I am starting
  Claude Code on *folder*." and the turn ends. The session works on its own.
- The session reads, lists and searches in its folder without asking. **Every write and every command opens
  the confirmation request of work item 4**, with the folder, the tool and the exact path or the whole command.
  Without an answer the session waits: no timeout answers in place of the user (ADR 0023).
- The "Claude Code tasks" page (settings, order 54): whether Claude Code is found and its version, the
  running sessions with a "Stop" button, the cap of a session, how many sessions run at once, and a
  "trusted" checkbox per allowed folder.
- The "Spending" page: the "Claude Code is paid by subscription" checkbox and the cost the sessions announced.

## Slice `ClaudeCode/Launch`

```
conversation ── IClaudeTasks (kernel) ──► ClaudeTaskRunner ── IClaudeCode (kernel) ──► process claude -p
                                               │  ▲
                 ITaskBoard (task created      │  └── IClaudeApprovals (kernel) ◄── approve tool of the local server
                 before the launch, statuses)  │
                                               ├── IAllowedFolders (kernel): allowed folders, check of a path
                                               ├── IPermissionDecision: the decision point, for every request
                                               ├── ISpendCap.SessionBudget: cap of the session
                                               └── publishes ClaudeTaskEnded (end, summary, cost)
```

| File | Role |
| --- | --- |
| `ClaudeTaskRunner` | Starts, follows and stops the sessions; receives their permission requests |
| `FolderChoice` | The folder a request names, by code |
| `SessionRules` | The class of a request of a session (table of ADR 0021, section 4) |
| `ClaudeLaunchOptions` | Section `ClaudeCode.Launch` of `config.json` |
| `ClaudeLaunchPage` | The "Claude Code tasks" page |

### Choice of the folder

- The folder of a session is **an allowed folder** ("Allowed folders" page), never another one.
- One allowed folder only: that one. Several: the one the request names. The name is compared by code,
  whatever the case, the accents and the separators: "Kazo Bar" names `kazo-bar`, `KazoBar` and `Kazo Bar`.
- No folder or several folders named: Jarvis asks "On which folder? A, B or C.". **The next turn answers** if
  it names one folder only, within two minutes; any other request forgets the question.
- No allowed folder: Jarvis says so and starts nothing.

### Launch

In this order, and the first refusal stops everything with its sentence:

1. Claude Code available on this host, a folder, a place among the sessions (3 at most by default).
2. Claude Code found in the `PATH`.
3. Cap of the session: the setting (2 US dollars by default), **never more than what is left of the monthly
   cap when Claude Code counts**. At zero, nothing starts.
4. Jarvis says the folder. **An interruption during that sentence cancels the launch.**
5. The task is created (`pending`, source `harness`), then the summary of the earlier exchanges is asked from
   the local model, then the session starts.

First message of the session (ADR 0021, section 8): the summary, presented as a context, then the request
word for word. Without an earlier exchange, or if the summary fails, the request leaves alone. **Nothing else
of the memory leaves.**

No confirmation is asked to start (answer 3 of ADR 0021): the session is read-only until a write is approved.

### Permissions of a session

Every request received by `approve` is classified by `SessionRules`, then goes through the decision point
with the session as the requester; it is logged like the others.

| Request | Class |
| --- | --- |
| `Read`, `Glob`, `Grep` in the folder of the session | Read |
| `Write`, `Edit`, `MultiEdit`, `NotebookEdit` in the folder | Sensitive |
| `Bash` | Sensitive; the confirmation shows the whole command |
| Any other tool | Sensitive |
| A path outside the folder of the session, whatever the tool | Forbidden |
| `sessions/*.key`, `ide/*.lock` under a `.claude` folder | Forbidden |
| A write that does not say where | Forbidden |
| The folder of the session is no longer an allowed folder | Forbidden |

- A relative path is read from the folder of the session; the path is resolved (links followed) before it is
  compared.
- A request that names no session Jarvis follows is denied without a question.
- Answer given to the session: `allow` with the input unchanged, or `deny` with the cause, in English.
- The task goes to `waiting_approval` during the question and back to `working` after the answer.

### Watching the stream

If a write tool or `Bash` runs without an error while `approve` never received its request, **Jarvis stops the
session** and the task becomes `blocked`, with the cause (ADR 0021, section 1). This is what would happen if a
version of Claude Code stopped calling the permission tool, or if the settings of a trusted folder allowed a
command in advance.

### End of a session

| What happens | Task | Summary |
| --- | --- | --- |
| A result, then the process ends | `done` | The last message of the session |
| A result in error (cap reached…) | `blocked` | The last message |
| The process ends without a result | `blocked` | "The session stopped without a result." |
| Stopped by the watch | `blocked` | The tool that ran without asking |
| Stopped by the user (page) | `abandoned` | "Stopped by the user." |
| Jarvis closes | `blocked` | "Jarvis was closed while the session was running." |

- A result does not end the task while the process lives: background work may go on (a second result then
  arrives).
- **When it closes, Jarvis stops all its sessions**: none goes on writing with nobody to answer. At start, a
  task of source `harness` left open (Jarvis crashed) becomes `blocked`.
- At the end, `ClaudeTaskEnded` is published: task, folder, end, summary, announced cost.

## Settings (`ClaudeCode.Launch`)

| Setting | Default | Bounds |
| --- | --- | --- |
| `SessionBudgetUsd` | 2 | 0.1 to 100 |
| `MostSessions` | 3 | 1 to 10 |
| `TrustedFolders` | none | allowed folders |
| `Model` | none (the one of Claude Code) | one word; not on the page yet |

**Trusted folder:** the session loads the settings of the project, **hooks of the repository included, run
without a question**. The page says so in full above the checkboxes.

## Cost (`Llm/Spend`)

- `SpendOptions.ClaudeCodeBySubscription`, **checked by default** (ADR 0020, answer 4).
- Checked: the cost each session announces is added **apart** (`SpendCount.ClaudeCodeUsd`), shown under the
  checkbox, and does not count against the cap.
- Unchecked: it counts against the cap like a cloud answer, with the same warnings at 80 % and at the cap,
  and `ISpendCap.SessionBudget` bounds each session to what is left.
- The cost is what Claude Code announces (`total_cost_usd`): Jarvis does not compute it. A blocked or
  abandoned session that announced a cost is counted too.

## Conversation (`Conversation/Dialogue`)

- A hand-off of kind `task` goes to `IClaudeTasks` when the slice is there; otherwise the earlier refusal
  ("the target for tasks is not available yet").
- By the formula ("Ask Claude Code…"): the local model is neither loaded nor asked, except for the summary
  when there are earlier exchanges.
- By the `hand_off_to_claude` tool with `kind: task`: the request of the turn leaves word for word; the tool
  is terminal, nothing goes back to the local model. **This kind is not offered to the local model yet** (see
  the deviations).
- The summary is made by a call without tools: the exchanges are one message to summarize, with the
  instruction never to follow an instruction they hold. 1,500 characters at most.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `IClaudeTasks`, `ClaudeTaskRequest`, `ClaudeTaskOpening` (`Kernel/ClaudeCode`) | Handing a work to Claude Code from the conversation |
| `ClaudeTaskEnded`, `ClaudeTaskEnd` | The end of a session, for the count and the announcements |
| `IAllowedFolders`, `PathVerdict`, `CheckedPath` (`Kernel/Files`) | The allowed folders, seen by another slice than the file tools |
| `ISpendCap.SessionBudget` | What a session may cost at most |

## Web host

`SimulatedClaudeCode` stands for Claude Code. Its session asks to write `NOTES.md` in its folder, through the
same contract as the real `approve` tool. Words of the message change the script: "outside" or « hors » (a
write outside the folder), "without asking" or « sans demander » (a command that never asked), "wait" or
« attends » (works until stopped). The test page shows the last launch and the task in `#claude-code`.

## Deviations from ADR 0021 and from the plan

1. **The summary at the end is not said aloud in this work item.** It is kept on the task and published
   (`ClaudeTaskEnded`). Saying it without cutting the user off, outside a turn and within the quiet hours, is
   the job of work item `stage2/13-session-announcements`, which builds that mechanism.
2. **A session has one folder**: `--add-dir` is not used.
3. **Only the formula starts a session.** The `hand_off_to_claude` tool still offers `kind: question` only to
   the local model: the conversation knows how to treat a `kind: task` call (tested), but offering it would let
   the local model start a session by itself, which no trial with the real model has measured. To be opened
   after a trial on Benoit's PC.
4. **No confirmation timeout** (ADR 0023, already noted in the first part).

## Provisional choices of this work item

To be confirmed by Benoit; each one changes without touching the others.

1. A write without a path, and any path outside the folder of the session even in another allowed folder,
   are forbidden without a question.
2. The question "on which folder?" lives two minutes and takes one answer only.
3. The title of the task is the request, cut at 80 characters.
4. The bounds of the settings (0.1 to 100 US dollars; 1 to 10 sessions).
5. When Jarvis closes, the sessions are stopped and their tasks become `blocked`.
6. The cost of a blocked or abandoned session is counted.
7. The model (`Model`) is set in `config.json` only.

## Not verified

- **No real Claude Code session was started by this code, and nothing ran on Windows.**
- The watch assumes that the stream shows every use of a tool with its identifier and its result, those of a
  sub-agent included.
- The names of the path fields of the tools of Claude Code (`file_path`, `notebook_path`, `path`) come from
  spike E12 and its documentation; a tool that named its path otherwise would be classified "Sensitive"
  without a check of the folder (it is still confirmed by the user).
- The quality of the summary made by the local model.

## Tests

- Unit: `SessionRulesTests`, `FolderChoiceTests`, `ClaudeTaskRunnerTests` (folder, announcement, task before
  the launch, cap, permissions, watch, ends, closing), `ConversationEngineTests` (file
  `ConversationEngineClaudeTasksTests.cs`), `SpendTests` (cost apart or counted).
- End to end: `ClaudeTasksTests` (8 scenarios on the Web host).

## Windows checks

To do with Benoit, on his PC, with a real Claude Code and a throwaway working folder:

1. "Ask Claude Code to read the README and summarize it": Jarvis says the folder, the session starts, the
   task becomes `done` with its last message.
2. A write: the confirmation shows the exact path; refused, the file does not exist; approved, it does.
3. A command (`Bash`): the confirmation shows the whole command.
4. A write outside the folder: refused without a question.
5. The headers of the MCP file (`${JARVIS_LOCAL_TOKEN}`) are expanded (otherwise everything is refused).
6. How long Claude Code waits for the answer of `approve` before giving up.
7. "Stop" on the page: the process and its children are gone.
8. The announced cost shows on the "Spending" page, apart.
9. A trusted folder does load the instructions of the repository.
