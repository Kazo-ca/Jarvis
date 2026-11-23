# Local server and Claude Code hooks

Work item `stage2/11-hook-server`, **second part** of work item 11 (US2.12). Decisions:
[ADR 0021](adr/0021-claude-code-sessions.md), sections 3, 5 and 6, and [ADR 0013](adr/0013-relais-readiness.md),
section 7. The first part is in [tasks-and-sessions.md](tasks-and-sessions.md).

## What is built

```
Claude Code session ── hook ──► http://127.0.0.1:<port>/hook/…      (token required)
                                        │
            Adapters/LocalServer/KestrelLocalServer ── ILocalServer, ILocalRoute (kernel)
                                        │
            Tasks/Board ─► TaskHooks: five routes ─► ITaskBoard (creates and moves the tasks)

ClaudeCode/Hooks ─► ClaudeHooks, HookEntries ─► IClaudeUserSettings (kernel)
   "Claude Code hooks" page                      └► Adapters/ClaudeCode/ClaudeUserSettings
   preview, install, remove                          %USERPROFILE%\.claude\settings.json, dated copy
```

## Local server (`ILocalServer`, `Adapters/LocalServer`)

- Kestrel inside the Jarvis process, bound to **`127.0.0.1` and nothing else**. A test opens the port on the
  other addresses of the machine and finds it closed. It is not the server of the mobile app (ADR 0003).
- **No route without a token**, header `Authorization: Bearer <token>`. Without a valid token the answer is
  `401`, whatever the path: a caller does not even learn which routes exist. The comparison runs in constant
  time.
- Two tokens:

  | Token | Where it lives | What it opens |
  | --- | --- | --- |
  | Token of the run (`ILocalServer.SessionToken`) | In memory, drawn at each start (32 bytes) | Every route. For the sessions started by Jarvis (work item 12) |
  | Token of the user's hooks | In the vault, reference `ClaudeCode.UserHookToken` | The `/hook/…` routes only; `403` elsewhere |

- Only `POST` is served (`405` otherwise), unknown path `404`, body over 1 MB `413`. A route that fails
  answers `500` without detail and does not stop the server. A route never sees the token.
- The routes come from the slices, as `ILocalRoute` services. The server knows no slice.
- Section `Local.Server`:

  | Setting | Default | Role |
  | --- | --- | --- |
  | `Port` | 47821 | Fixed port, from 1024 to 65535: the installed hooks carry it |

  A change of port takes effect at once. **A port already taken does not bring Jarvis down**: the server does
  not listen, the page says so and offers to change the port.
- `Kazo.Jarvis.Adapters` now references `Microsoft.AspNetCore.App`. The release is self-contained (ADR 0001):
  the user installs nothing more. `/mcp` and the `approve` tool arrive with work item 12 on this same server.

## Hook routes (`Tasks/Board`, `TaskHooks`)

The paths are in the kernel (`HookPaths`): the slice that serves them, the one that writes them into the
user's settings and the one that will start sessions must agree.

| Claude Code hook | Route | Effect |
| --- | --- | --- |
| `SessionStart` | `/hook/task.context` | Links the session to the task |
| `UserPromptSubmit` | `/hook/task.start` | `working` |
| `PermissionRequest` | `/hook/approval.requested` | `waiting_approval` |
| `SubagentStop` | `/hook/subtask.idle` | The sub-task of that agent goes to `done`, with its last message |
| `Stop` | `/hook/task.idle` | `idle`, with the last message as summary |

- **A hook decides nothing and receives nothing**: the answer is always empty (`204`). Claude Code goes on as
  if nobody listened.
- **The body is data**: only `session_id`, `cwd`, `prompt`, `last_assistant_message`, `agent_id` and
  `agent_type` are read, cut to length. Nothing is executed nor given to a model here. A body that is not a
  JSON object, or without a session, answers `400` and changes nothing. The text of a request or of an answer
  is never written to the log.
- **Session started by Jarvis:** it names its task by the `X-Relais-Task` header. The task exists before the
  session; an unknown task answers `404` and is not created.
- **Session started by hand:** it has no task. Its first hook creates one, of source `hook`, filed by the
  remote and the branch the session registry gives (failing that, by the folder of the hook), found again
  afterwards by the session identifier. Title: the first line of the request when the first hook carries one;
  otherwise the name of the session, then that of the project.
- **The token of the user's hooks cannot name a task**: the `X-Relais-Task` header is listened to for the
  token of the run only.
- A `UserPromptSubmit` whose text starts with `<task-notification>` is not a human request: the task goes
  back to `working`, but that text never becomes a title.
- Hooks are handled one at a time: the first two hooks of a session do not create two tasks.
- A session whose task ended and that resumes gets a new task.

## Hooks in the user's settings (`ClaudeCode/Hooks`)

For the sessions the user starts themselves. **Jarvis writes to `%USERPROFILE%\.claude\settings.json` only on
a click**, from the "Claude Code hooks" page.

- The page first shows **the exact lines** that will be added, token included.
- "Install": dated copy of the file next to it (`settings.json.jarvis-YYYYMMDD-HHMMSS.bak`), then the five
  entries are added. The new file is written aside then put in place in one move: a failure leaves the file
  whole. Everything the user wrote is kept; **only the layout of the JSON is rewritten**.
- The entries of Jarvis are recognized by their address, `http://127.0.0.1:<port>/hook/<route>`, **whatever
  the port**: a removal also finds those written before a change of port.
- "Remove" takes away those entries only, then erases the token from the vault: lines kept elsewhere open
  nothing any more. A group or an event the removal emptied goes with them; what was empty before stays.
- **A file Jarvis does not know how to read is not modified**: invalid JSON, comments, `hooks` that is not an
  object, an event that is not a list. The page then gives the lines to paste by hand.
- States shown: not installed, installed, **not up to date** (another port, another token, or a line removed
  by hand), unreadable file.
- The token is drawn the first time the page shows the lines.

### Shape of the installed hooks

```json
{ "hooks": { "Stop": [ { "hooks": [ {
  "type": "command",
  "command": "curl.exe -s -m 2 --noproxy 127.0.0.1 -X POST -H \"Authorization: Bearer <token>\" -H \"Content-Type: application/json\" --data-binary @- http://127.0.0.1:47821/hook/task.idle",
  "async": true,
  "timeout": 5
} ] } ] } }
```

**They are background commands (`async`), not `http` hooks.** ADR 0021, section 6, asks to check that a hook
whose server does not answer neither blocks nor slows the session, and otherwise to install commands that
fail silently. That check needs a real Claude Code on Windows; it was not done. Background commands are
therefore installed from the start:

- Claude Code does not wait for an `async` hook: with Jarvis closed, the session does not slow down;
- `curl` gives up by itself after 2 seconds (`-m 2`) and says nothing (`-s`);
- `curl.exe` by its full name: in Windows PowerShell, `curl` is another command. The line is valid in Git Bash
  and in PowerShell;
- the body of the hook, received on the standard input, is posted as it is.

A test runs this very line through a shell against the real server: the body arrives, the answer is empty;
with the server stopped, the command fails without writing anything, in less than 4 seconds.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `ILocalServer`, `LocalServerStatus` (`Kernel/LocalServer`) | The local server, a native service of the host; the token of the run |
| `ILocalRoute`, `LocalRequest`, `LocalResponse`, `LocalCaller` | A route given by a slice, and who calls it |
| `LocalServerOptions` | The port; the reference of the hooks token in the vault |
| `HookPaths` | The five `/hook/…` paths |
| `IClaudeUserSettings`, `ClaudeUserSettingsFile`, `ClaudeUserSettingsState` (`Kernel/ClaudeCode`) | The user's settings file, a native service of the host |

## Web host

`SimulatedLocalServer` opens no port: it says it listens on the port set. `SimulatedClaudeUserSettings` keeps
in memory a file with a setting and a hook of the user's own. Buttons of the test page: "Make the Claude Code
settings unreadable", "Put the Claude Code settings back".

## Deviations from ADR 0021

1. **A page of its own**, "Claude Code hooks", right after "Claude sessions", instead of a button in that
   page: two slices do not share a page (ADR 0002).
2. **Background commands rather than `http` hooks** for the sessions started by hand: the case section 6
   provides for when the check is not done. The sessions started by Jarvis (work item 12) keep the `http`
   hooks of section 5.

## Provisional choices of this work item

To be confirmed by Benoit:

1. **Port 47821** by default.
2. **A session started by hand becomes a task at its first hook**, `idle` if that hook is `SessionStart`.
3. **The token is in clear in the user's `settings.json`**: a hook must know it. It opens the `/hook/…`
   routes only, which decide nothing.
4. **A removal erases the token**; a new installation draws another one.
5. **The file is rewritten with a two-space indentation.** A file with comments is not touched.
6. **A sub-task appears when its sub-agent stops**: no installed hook tells that it starts.

## Known limits

- A task of source `hook` stays `waiting_approval` after the user's answer, until `Stop` or the next request:
  no installed hook tells that the permission was given.
- Nothing is announced aloud yet: that is work item `stage2/13`.
- The hooks only apply to the sessions started after the installation.

## Not verified

- **Nothing ran on Windows, and no real Claude Code called these routes.** The shape of the hooks (`async`,
  `timeout`, command) comes from the Claude Code documentation, read on 21 November 2026, and from the bodies
  observed in spike E12.
- That `curl.exe` does receive the standard input of the hook in Git Bash and in PowerShell.
- That a failing hook shows nothing in the user's session.
- The writing of the real `settings.json`: done in a test folder only.

## Tests

- Unit: `KestrelLocalServerTests` (tokens, loopback address only, port taken, change of port, the real
  command line), `ClaudeUserSettingsTests` (dated copy), `TaskHooksTests`, `HookEntriesTests`,
  `ClaudeHooksTests`.
- Playwright: `ClaudeHooksTests` (preview without writing; install then remove; change of port; unreadable
  file).

## Windows checks

To do with Benoit, on his PC. **Jarvis writes to his `settings.json` only by his click.**

1. Open the page: the server listens on port 47821; `netstat -ano | findstr 47821` shows `127.0.0.1` only.
2. Read the preview, click "Install": the dated copy exists, the other settings are intact.
3. Start a Claude Code session, write to it: the "Claude sessions" page shows the task `working`, then `idle`
   at the end of the answer; `waiting_approval` when it asks for a permission.
4. **Close Jarvis, then write to a session**: it answers without delay and without a hook error message.
5. Do point 4 again without Git Bash (PowerShell alone), if the case arises.
6. Click "Remove": only the lines of Jarvis are gone.
