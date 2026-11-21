# Starting Claude Code: process, permission tool, requests of a session

Work item `stage2/12-claude-code-launch`, **first part** of work item 12 (US2.9, US2.10). Decisions:
[ADR 0021](adr/0021-claude-code-sessions.md), sections 1 to 5, and [ADR 0017](adr/0017-tools-classes-and-decision-point.md).
This part lays the services; **nothing starts a session yet**: "Ask Claude to…", the choice of the folder, the
delegated task, the summary and the cost are the second part (`stage2/12-claude-code-tasks`).

## What is built

```
slice that starts (second part) ── IClaudeCode (kernel) ──► Adapters/ClaudeCode/ClaudeCodeProcess
                                                               claude -p … (process, JSON stream)
                                                                      │ permission request
Claude Code session ── MCP ──► http://127.0.0.1:<port>/mcp, approve tool   (token of the run)
                                        │
            Adapters/LocalServer/KestrelLocalServer ── IClaudeApprovals (kernel) ──► slice that starts
                                                                                        │ classifies the request
                                                             IPermissionDecision (decision point, ADR 0017)
```

## Requests of a session at the decision point

A tool of Claude Code (`Write`, `Bash`…) is not a tool of Jarvis: it is in no registry. `ActionRequest`
therefore carries, **for a requester of kind `Session` only**, a `SessionAction`: the class given by the code
that received the request (table of ADR 0021, section 4) and the sentence to say.

- Read or everyday effect: allowed without a question. Sensitive or critical: the confirmation request of
  work item 4, with the tool and the exact input. Forbidden: denied without a question.
- Without a class, with an unknown class or an unreadable input: denied.
- **A class given by a tool call of the model is not listened to**: the field only counts for a session.
- Every decision is logged like the others.
- **No timeout**: Jarvis never decides in the place of the user (ADR 0023, which prevails here over the
  sentence "without an answer in time: `deny`" of ADR 0021). If Claude Code gives the call up, the request is
  cancelled.

## `approve` tool (`/mcp`)

- The local server carries the MCP server of Jarvis (`ModelContextProtocol.AspNetCore`), stateless: one
  request, one answer. It offers one tool only, `approve`, which Claude Code calls through
  `--permission-prompt-tool mcp__jarvis__approve`.
- **Closed without the token of the run.** The token of the user's hooks, written in their `settings.json`,
  gets `403`: it never reaches the permission tool.
- The request (`tool_name`, `input`, `tool_use_id`) goes to `IClaudeApprovals` with the task of the
  `X-Relais-Task` header. Answer given to Claude Code, in the form spike E12 proved:

  ```json
  {"behavior":"allow","updatedInput":{ …the input received, unchanged… }}
  {"behavior":"deny","message":"…the cause…"}
  ```

- **Jarvis never corrects an input.** Nobody to decide, or a decision that fails: `deny`, without detail.

## Launch (`IClaudeCode`, `Adapters/ClaudeCode`)

- `FindAsync`: looks for `claude` in the `PATH` and reads its version. `ClaudeCodeInstall.UpToDate` tells
  whether it is at least 2.1.328, the version of spike E12.
- `StartAsync(ClaudeLaunch)`: writes the two files of Jarvis, starts the process in the folder of the task,
  gives it its message and **closes the input**: the session goes to the end of its work, background work
  included, and ends. A later turn is a new launch with `ResumeSession`.
- Command line (ADR 0021, section 1):

  ```text
  claude -p --input-format stream-json --output-format stream-json --verbose
    --setting-sources ""                              ("project" for a trusted folder)
    --append-system-prompt-file <folder>\CLAUDE.md    (if the file exists, and not for a trusted folder)
    --settings <Jarvis root>\claude\settings.json
    --mcp-config <Jarvis root>\claude\mcp.json --strict-mcp-config
    --permission-prompt-tool mcp__jarvis__approve
    --allowedTools Read,Glob,Grep
    [--add-dir …] [--max-budget-usd 2.00] [--model …] [--resume …]
  ```

- **The settings and the hooks of the user are never loaded.**
- Files of Jarvis, under `%USERPROFILE%\.kazo\jarvis\claude\`, rewritten at each launch. **Nothing is written
  under `%USERPROFILE%\.claude\`.** They hold no secret: the token and the task are named there by environment
  variables (`JARVIS_LOCAL_TOKEN`, `RELAIS_TASK_ID`) that only the started process receives. The token is
  never on the command line.
- Without a listening local server, no session starts: it could not ask its permissions.
- Events read from the stream (`ClaudeRunEvent`): `ClaudeStarted` (session, model), `ClaudeToolUse`,
  `ClaudeToolResult`, `ClaudeTurnEnded` (last message, total cost announced, error, denials), and
  `ClaudeExited`, always the last. A line that is not understood is ignored: the format belongs to Claude Code.
- `StopAsync` stops the process and what it started.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `SessionAction`, `ActionRequest.Session` (`Kernel/Permissions`) | The class of an action of a session, given by code |
| `IClaudeApprovals`, `ClaudeApprovalRequest`, `ClaudeApproval` (`Kernel/ClaudeCode`) | The single place where a write of a session started by Jarvis is decided |
| `IClaudeCode`, `ClaudeLaunch`, `IClaudeRun`, `ClaudeRunEvent` and its kinds, `ClaudeCodeInstall`, `ClaudeCodeException` | Claude Code as a program started by Jarvis |

## Deviations from ADR 0021

1. **No `SessionStart` hook** in the file of Jarvis: it only takes commands (spike E12), whose variable syntax
   depends on the shell. The session is known from the start of the stream (`ClaudeStarted`); the slice that
   starts it will link it to the task.
2. **The token is also required on the hooks of the sessions of Jarvis** (`Authorization`), in addition to
   `X-Relais-Task`.
3. **No confirmation timeout** (ADR 0023).

## What is left of the work item

Second part, branch `stage2/12-claude-code-tasks`: the `ClaudeCode/Launch` slice ("Ask Claude to…", allowed
folder chosen or asked, task created before the launch, classification of the requests per the table of the
ADR, stop of a session whose write tool runs without a request, summary said at the end, cost and setting
"Claude Code is paid by subscription", cap per session, three sessions at most), the settings page, and the
simulated version of `IClaudeCode` for the Web host.

## Not verified

- **This code never started a real Claude Code**, and nothing ran on Windows. The command line, the `http`
  hooks and the shape of `approve` come from spike E12, done with other code.
- **The variables in the headers of the MCP file** (`${JARVIS_LOCAL_TOKEN}`, `${RELAIS_TASK_ID}`): the spike
  proved it for the hooks only. If they are not expanded, `/mcp` will answer `401` and the session will not be
  able to write anything (it fails closed).
- The time after which Claude Code gives the call to `approve` up.
- Only `claude.exe` is looked for: an installation through npm (`claude.cmd`) is not found.

## Tests

- Unit: `PermissionDecisionPointTests` (requests of a session), `KestrelApproveToolTests` (a real MCP client
  calls `approve`; closed to the token of the user's hooks), `ClaudeStreamTests`, `ClaudeCommandLineTests`,
  `ClaudeCodeProcessTests` (a script stands for `claude`: environment, files, message, events, stop).

## Windows checks

To do with Benoit, once the second part is built: they require starting a real session.
