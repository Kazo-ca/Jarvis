# ADR 0021: Claude Code sessions (launch, permissions, hooks, announcements)

## Status

Proposed on 2026-11-20 (work item `stage2/2-adrs`). The decisions Benoit took after spike E12 are in the
"Benoit's answers" section; the open questions in "Questions for Benoit".

- **Settles the points left by:** [ADR 0009](0009-permission-and-confirmation-model.md) (section 7: the exact
  command line, and whether the project settings are loaded), [ADR 0013](0013-relais-readiness.md) (sections
  6 and 7: installing the hooks of sessions started by hand),
  [ADR 0020](0020-hand-off-targets-and-spend-cap.md) (the class of `hand_off_to_claude` for `task`, and how
  the summary and the initial request reach Claude Code).
- **Stories:** US2.9 to US2.15 of the [design document](../plans/design.md).
- **Spikes:** E2, E3, E8 and E12
  ([E12 results](../../../../spikes/e12-claude-code-permissions/RESULTS.md), in French).
- **Builds on:** [ADR 0017](0017-tools-classes-and-decision-point.md) (decision point).

## Context

- Spike E12, Claude Code 2.1.328: every permission request of `claude -p` reaches the local MCP tool given by
  `--permission-prompt-tool`, and the answer is honoured (2 denials, 2 approvals).
- The hooks declared in the file passed with `--settings` call local routes and see `RELAIS_TASK_ID`. **An
  `http` hook never fires on `SessionStart`**: a command is needed.
- **A folder's `CLAUDE.md` is only loaded with the `project` source**, which also runs the repository's
  hooks, without asking in `-p` mode. `--append-system-prompt-file` gives the `CLAUDE.md` alone.
- A sub-agent started in the background makes a second turn: second `UserPromptSubmit`, second `Stop`, second
  `result`.
- Spikes E2 and E3: the registry `~/.claude/sessions/<pid>.json` lists the PC's sessions, Claude Desktop
  included, and reads in 73 ms. `sessions/*.key` and `ide/*.lock` hold secrets.
- ADR 0013 sets the shape of a task, its nine statuses and the names of the `/hook/…` routes.

## Decision

### 1. Command line of a session launched by Jarvis

```text
claude -p --input-format stream-json --output-format stream-json --verbose
  --setting-sources ""                                   (or "project" for a trusted folder, section 2)
  --append-system-prompt-file <folder>\CLAUDE.md         (if the file exists and the source is "")
  --settings <Jarvis's settings file>                    (hooks, section 5)
  --mcp-config <Jarvis's MCP file> --strict-mcp-config
  --permission-prompt-tool mcp__jarvis__approve
  --allowedTools Read,Glob,Grep
  --add-dir <allowed folders useful to the task>
  --max-budget-usd <session cap>
  [--model <configured model>] [--resume <session> for a following turn]
```

- The process starts in the task's folder, with `RELAIS_TASK_ID=<task identifier>` in its environment. **The
  task is created before the launch** (ADR 0013, section 7).
- Jarvis's files are under `%USERPROFILE%\.kazo\jarvis\claude\`. Jarvis rewrites them when it starts. **It
  never writes into `%USERPROFILE%\.claude\`** for its own sessions.
- Jarvis looks for `claude` in the `PATH` and reads its version. Below 2.1.328, the page says so and offers
  the update. The `--permission-prompt-tool` option no longer shows in the help: **Jarvis therefore watches
  the stream of each session, and stops it if a write or command tool runs without `approve` having received
  its request.**
- Folder of the task: an allowed folder (ADR 0009, section 5). If the request does not designate a single
  one, Jarvis asks which.
- Cap of a session: a setting, **US$2 by default**, never more than what is left of the monthly cap when
  Claude Code is counted (ADR 0020, section 6).
- Sessions launched by Jarvis at the same time: 3 at most, adjustable.

### 2. Settings loaded

- **By default: nothing from the user or the repository** (`--setting-sources ""`), and the folder's
  instructions through its `CLAUDE.md` alone (answer 2). Known limit: only the root `CLAUDE.md` is read, not
  those of subfolders, nor `@imports`, nor `.claude/rules`.
- **"Trusted" folder:** one box per allowed folder, unchecked by default. Checked, the session loads the
  `project` source: full instructions, **and the repository's hooks run without asking**. The box says so in
  full.
- The user's personal settings and hooks are never loaded (ADR 0009, section 7).

### 3. Jarvis's local server

- An HTTP server inside Jarvis's process, **listening on `127.0.0.1` only**, adjustable port (a fixed port:
  the hooks installed at the user's must know it). It carries:
  - `/mcp`: Jarvis's MCP server (`ModelContextProtocol.AspNetCore`), with the `approve` tool;
  - `/hook/task.context`, `/hook/task.start`, `/hook/approval.requested`, `/hook/task.idle`,
    `/hook/subtask.idle`: the ADR 0013 routes.
- **No route without a token.** Jarvis draws a token at each start, keeps it in memory and gives it to its
  sessions through an environment variable, picked up in a header (`allowedEnvVars`, proven by E12 for hooks;
  to verify for the MCP file in work item `stage2/11-tasks-and-sessions`). The hooks installed at the user's
  (section 6) have a separate token, in the vault, which only gives access to the `/hook/…` routes.
- This server is not the mobile's built-in server ([ADR 0003](0003-hosts-and-native-services.md), stage 4):
  it never opens to the network.
- A hook's body is **data** (ADR 0009, principle 2).

### 4. Permissions of a session launched by Jarvis

- **A single decision point** (answer 1): the `approve` tool. It passes the request to the ADR 0017 decision
  point, with the session as requester.
- Class of a request:

  | Claude Code tool | Class |
  | --- | --- |
  | `Read`, `Glob`, `Grep` in a folder of the session | Read (allowed by `--allowedTools`, the request does not arrive) |
  | `Write`, `Edit`, `NotebookEdit` in a folder of the session | Sensitive |
  | `Bash` and any command launch | Sensitive |
  | Any path outside the session's folders, `sessions/*.key`, `ide/*.lock` | Forbidden |
  | Any other tool (`WebFetch`, `WebSearch`, `Agent`, MCP tool…) | Sensitive |

- The confirmation request (ADR 0009, section 3) says the folder, the tool and the exact detail: the path and
  the differences for a write, the whole command for `Bash`.
- Answer returned to Claude Code: `allow` with the received input, **unchanged**, or `deny` with the cause.
  Jarvis never corrects an input.
- No human answer within the delay: `deny`. The delay after which Claude Code gives up the call was not
  measured; it is measured in work item `stage2/12-claude-code-tasks`, and Jarvis's confirmation delay is set
  below it.
- The `PermissionRequest` hook **decides nothing**: it only moves the task to `waiting_approval`.

### 5. Hooks of sessions launched by Jarvis

In Jarvis's settings file, with the header `X-Relais-Task: $RELAIS_TASK_ID`:

| Hook | Type | Route | Effect on the task |
| --- | --- | --- | --- |
| `SessionStart` | command (`curl`, shipped with Windows) | `/hook/task.context` | Ties the session to the task |
| `UserPromptSubmit` | `http` | `/hook/task.start` | `working` |
| `PermissionRequest` | `http` | `/hook/approval.requested` | `waiting_approval` |
| `SubagentStop` | `http` | `/hook/subtask.idle` | The subtask moves to `done` |
| `Stop` | `http` | `/hook/task.idle` | `idle` |

- A task created by Jarvis is `pending` until the first hook.
- **A task launched by Jarvis is `done` only at the stream's `result` with no background task running.** A
  `UserPromptSubmit` whose text starts with `<task-notification>` is not a human request.
- The process stops without a `result`: the task moves to `blocked`, with the cause. Stopped by the user:
  `abandoned`.
- The decision returned by `approve` puts the task back to `working`.

### 6. Sessions started by hand

- Jarvis sees them through the registry `~/.claude/sessions/<pid>.json` (US2.11, US2.14), read at a regular
  interval. **It never reads `sessions/*.key` or `ide/*.lock`**; a test proves it.
- To know that a session is waiting or has finished (US2.12), its hooks are needed. **Jarvis installs them in
  `%USERPROFILE%\.claude\settings.json` only when the user asks**, with a button of the "Claude sessions"
  page:
  - the page first shows the exact lines that will be added;
  - Jarvis makes a dated copy of the file, then adds its entries without touching the others;
  - its entries are recognized by their address (Jarvis's port and routes); the "Remove" button only removes
    those;
  - a file Jarvis cannot read is not modified: the page gives the lines to paste by hand.
- **Before offering this button**, work item 11 verifies that a hook whose server does not answer (Jarvis
  closed) neither blocks nor slows the user's session. If it does, the installed hooks are commands that fail
  silently.
- For these sessions, Jarvis **only announces**: it never answers in their place (ADR 0009, section 7,
  US2.15). Its `/hook/approval.requested` route returns no decision.
- A session seen through the registry without a hook shows with the registry's state and its last activity.

### 7. Shape and storage of tasks

- The shape, the nine statuses, the project key and the topic key are those of ADR 0013, section 6. Tasks are
  in `jarvis.db`.
- The session list is grouped by project. A session launched by Jarvis has the source `harness`, a session
  seen through its hooks the source `hook`.

### 8. Hand-off to Claude Code

- `hand_off_to_claude` with `kind: task` is of class **"Read"**: the session starts read-only on an allowed
  folder, and every write is confirmed afterwards (question 1). Jarvis says the folder before launching
  ("Starting Claude Code on Kazo Bar."); an interruption cancels.
- The first message of the session, sent through the input stream, carries in this order (ADR 0020,
  answer 1):
  1. a **summary of the previous exchanges**, made by the local model, presented as context;
  2. the **complete initial request**, word for word.
- Nothing else from the memory leaves (ADR 0020, section 4).
- At the end, Jarvis says the session's summary (its last message, shortened by the local model) and adds the
  announced cost to the counter, according to the "Claude Code is paid by subscription" setting (ADR 0020).

### 9. Announcements

- Jarvis announces aloud when a session **finishes** or **waits for a permission** (US2.12).
- Setting per folder: everything, waits only, nothing. By default: everything for sessions launched by
  Jarvis; everything for the others once their hooks are installed.
- **Quiet hours** (question 2): during those hours, nothing is said; the orb keeps its amber badge (ADR 0011)
  and the announcement stays in the conversation.
- An announcement does not interrupt the user nor an answer in progress: it waits until the assistant is
  idle.
- "Where is Claude at on Kazo Bar?" (US2.13): the local model summarizes from the last message and the task's
  state, received as data.

### 10. Slices

- `Tasks/Board`: the shape of a task, its storage, the `/hook/…` routes.
- `ClaudeCode/Sessions`: the registry and the "Claude sessions" page.
- `ClaudeCode/Launch`: the launch, the JSON stream, the `approve` tool.
- `ClaudeCode/Hooks`: installing and removing the hooks at the user's.
- `ClaudeCode/Announcements`: the announcements and the quiet hours.
- The `claude` process and the local server are native services behind kernel contracts, implemented in
  `Kazo.Jarvis.Adapters`; the Web host has simulated versions.

## Benoit's answers

Questions asked in the conversation on 2026-11-20, after spike E12. Answer: "I agree with your
recommendations".

1. **Where is a write asked by Claude Code decided?** In a single place: Jarvis's permission tool, which asks
   by voice or next to the orb.
2. **Does a session launched by Jarvis load the repository's settings?** No: the `CLAUDE.md` alone, without
   settings or hooks. The full settings only for folders marked as "trusted".

## Questions for Benoit

Asked in the conversation on 2026-11-20.

1. **Does launching a Claude Code session ask for a confirmation?** Recommendation: no. Jarvis says the
   folder and launches; the session is read-only and every write is confirmed.
2. **Default quiet hours.** Recommendation: none by default, adjustable.

## Options set aside

- **Letting Claude Code decide with a wide `--allowedTools` or `--permission-mode acceptEdits`.** The
  approval would no longer come from a human (ADR 0009, principle 4).
- **Deciding in the `PermissionRequest` hook as well.** Two places that decide: the first to answer wins
  (E12).
- **Always loading the `project` source.** The hooks of a cloned repository would run without asking.
- **Installing the hooks at the user's without asking.** Jarvis does not touch another program's settings
  without agreement.

## Consequences

- A session launched by Jarvis benefits from neither the user's plugins nor their hooks: smaller context (60
  commands instead of 470 in the spike), predictable behaviour.
- Without the "trusted" box, a repository's subfolder instructions and `@imports` are not read.
- Jarvis depends on Claude Code options that may change from one version to the next; the watch of section 1
  stops a session whose permissions do not arrive, rather than letting it write.
- **Not verified by E12:** the delay before the `approve` call is given up, a permission mode other than the
  default one, the cost of a real task with Sonnet or Opus, the behaviour when the local server does not
  answer, the token in the MCP file. These points are verified in work items 11 and 12.
- In v2, with Relais, the human will be able to decide from Jarvis for a session they did not launch (ADR
  0013, section 9). A new ADR will then change section 6.
