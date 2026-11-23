# Plan of the stage 2 series: tools, MCP, hand-off and Claude sessions

2026-11-20 · **Validated by Benoit on 2026-11-20** (work item `stage1/stage-2-plan`). His answers are at the end.

## Goal

At the end of this series, Jarvis acts: it controls the house through Home Assistant, hands heavy tasks to
Claude and tells where the Claude sessions of the PC stand. These are user stories US2.1 to US2.15 of the
[design document](design.md#stage-2-tools-mcp-handoff-and-claude-sessions), plus the task fan of
[ADR 0010](../adr/0010-task-fan-and-orb-click.md).

Unlike the stage 1 plan, this one **starts with decisions**: the accepted ADRs deliberately leave several
points to stage 2 (class of each tool, Claude Code command line, hooks, spend cap). Work items 1 and 2
settle them before any line of code.

## What is already decided

| Topic | Decided by | In one sentence |
| --- | --- | --- |
| Tool format, tool loop kept by Jarvis, tolerant parser | [ADR 0007](../adr/0007-llm-providers-and-tool-format.md) | Names in English, descriptions in the assistant's language, every call goes through permissions and is shown |
| Action classes, confirmation, allowed folders, sessions started by Jarvis | [ADR 0009](../adr/0009-permission-and-confirmation-model.md) | The model proposes, code decides; denied by default; only a human approves |
| Task fan, double click on the orb | [ADR 0010](../adr/0010-task-fan-and-orb-click.md) | Built after spike E11 |
| Task shape, nine statuses, project and subject keys, hooks and `/hook/…` routes | [ADR 0013](../adr/0013-relais-readiness.md), sections 6 and 7 | Everything shown as a "task" takes the Relais shape |
| Local model `qwen3:14b` without thinking, 42 tool calls out of 42 in French | Spike E7 | English still to check |
| Registry of Claude Code sessions, Claude Desktop included | Spikes E2 and E3 | `~/.claude/sessions/<pid>.json`, read in 73 ms |
| `claude -p` as a JSON stream from C# | Spike E8 | The permission request through an MCP tool was not tried |

## Branches and merging

- Series `stage2`, base branch `stage2/base`, started from `main` once `stage1/base` is merged into `main`
  (answer 1 below).
- One work item per branch `stage2/<n>-<topic>`, one PR per work item to `stage2/base`.
- An agent merges its own PR into `stage2/base` once build and tests are green. Only Benoit merges into
  `main`.
- An ADR is merged only once Benoit has accepted it.

## What every work item must deliver

The eight points of the [stage 1 plan](stage-1.md#what-every-work-item-must-deliver) apply as they are.
For this series, add:

9. Every tool declares its class (ADR 0009) and has a test proving denial by default.
10. Nothing leaves the machine without an explicit setting: a work item that adds an outbound connection
    (web search, cloud provider, remote MCP server) says so on the screen where it is set and in the
    acceptance list.
11. Everything shown as a task uses the shape and statuses of ADR 0013, section 6.

## Spikes and ADRs to do first

### Spikes (work item 1, on Benoit's PC)

| Spike | Blocks | What it must prove |
| --- | --- | --- |
| E11 | Work item 14 | Cost and smoothness of the fan's transparent window, click-through, comfort of the delayed single click (ADR 0010, section 7). |
| E12 | Work items 11 and 12 | A permission request from `claude -p` reaches Jarvis through a local MCP tool (`--permission-prompt-tool`) and its answer is obeyed; the `SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `Stop` and `SubagentStop` hooks call a local route; what `--setting-sources` loads (settings, repository instructions, hooks). |
| E13 | Work item 3 | The protocol of spike E7 replayed with tool descriptions in English, and with the "hand off" tool in both languages. |
| E14 | Work items 6 and 7 | The C# MCP client (official `ModelContextProtocol` package) talks to Benoit's Home Assistant MCP server: tool list, one call, reconnection. Also says how to recognize security home automation (entity domain, label, editable list). |

### ADRs (work item 2)

| ADR | Topic | Settles the points left by |
| --- | --- | --- |
| 0017 | Basic tools, class of each tool, permission decision point and display of calls | ADR 0007 (section 3), ADR 0009 (sections 2 and 10) |
| 0018 | MCP client: transports, configuration, per-tool activation, Home Assistant and security home automation | ADR 0009 (sections 2 and 8) |
| 0019 | Conversation memory: what is kept, automatic summary, page to see and erase | ADR 0004, ADR 0007 (section 6) |
| 0020 | Hand-off, targets and spend cap | ADR 0007 (sections 1 and 7), ADR 0009 (section 9) |
| 0021 | Claude Code sessions: exact command line, settings loaded, hooks and their installation, local server for the `/hook/…` routes, announcements | ADR 0009 (section 7), ADR 0013 (sections 6 and 7) |

The numbers are those following ADR 0016; they are confirmed when written. If spike E11 fails, one more ADR
says what to change in the fan (ADR 0010, section 7).

## Overview

| No. | Work item (branch `stage2/…`) | Stories | ADR | Depends on | Size |
| --- | --- | --- | --- | --- | --- |
| 1 | `1-spikes`: spikes E11 to E14 | — | 0010, 0009 | — | M |
| 2 | `2-adrs`: ADRs 0017 to 0021 | — | — | 1 | M |
| 3 | `3-tool-loop`: tool loop, decision point, calls shown, time and calculation | US2.1 (start) | 0017 | 2 | L |
| 4 | `4-confirmation`: confirmation request by voice and next to the orb, log | US2.10 (base) | 0009, 0017 | 3 | L |
| 5 | `5-basic-tools`: files and allowed folders, web search, HTTP request | US2.1 | 0017 | 4 | L |
| 6 | `6-mcp-client`: MCP servers, tool list and activation | US2.2 | 0018 | 4 | L |
| 7 | `7-home-assistant`: Home Assistant through MCP, security home automation | US2.3 | 0018 | 6 | M |
| 8 | `8-memory`: persistent memory, summary, memory page | US2.4 | 0019 | 3 | M |
| 9 | `9-hand-off`: "hand off" tool, "ask Claude", targets, who answers | US2.5, US2.6, US2.7 | 0020 | 3 | L |
| 10 | `10-spend-cap`: monthly cap and counter | US2.8 | 0020 | 9 | M |
| 11 | `11-tasks-and-sessions`: task shape, local hook server, session list | US2.11, US2.14 | 0021, 0013 | 2 | L |
| 12 | `12-claude-code-tasks`: start a session by voice, permissions for its writes | US2.9, US2.10, US2.15 | 0021, 0009 | 4, 9, 11 | L |
| 13 | `13-session-announcements`: announcements per folder, quiet hours, session summary | US2.12, US2.13 | 0021 | 11 | M |
| 14 | `14-task-fan`: task fan and double click on the orb | — | 0010, 0013 | 1, 11 | L |
| 15 | `15-acceptance`: acceptance of the stage on Benoit's PC | all | — | all | S |

Sizes: S, a few hours; M, a day; L, more than a day. These are orders of magnitude.

Independent once work item 4 is done: 5 (basic tools), 6 and 7 (MCP), 8 (memory), 9 and 10 (hand-off).
Work item 11 only depends on the ADRs and can move from work item 2 on.

```
1 ─ 2 ─┬─ 3 ─┬─ 4 ─┬─ 5 ───────────┐
       │     │     ├─ 6 ─ 7 ───────┤
       │     │     └─────┐         │
       │     ├─ 8 ───────┼─────────┤
       │     └─ 9 ─┬─ 10 ┼─────────┼─ 15
       │           └─────┼─ 12 ────┤
       └─ 11 ─┬──────────┘         │
              ├─ 13 ───────────────┤
              └─ 14 ───────────────┘
```

## Work items in detail

### 1. Spikes (`stage2/1-spikes`)

- E11 to E14, each in `spikes/<name>/` with its `README.md` and its `RESULTS.md`.
- Summary added to [spikes.md](../spikes.md), with the decisions that come out of them.
- Everything runs on Benoit's PC (WPF window, Claude Code, Ollama, Home Assistant), by an agent through
  remote control. Benoit has nothing to say or listen to.

### 2. ADRs (`stage2/2-adrs`)

- ADRs 0017 to 0021, in French then in English, one PR per ADR or per pair of linked ADRs.
- Every open question is asked to Benoit in the conversation, with its choices and a recommendation.
- The project memory and the ADR index follow.

### 3. Tool loop (`stage2/3-tool-loop`)

- Kernel contracts: a tool (name, description per language, schema, class), the tool registry, the
  permission decision point.
- The `Conversation/Dialogue` slice gives the tools to the model, runs the calls one by one through the
  decision point and sends the results back as data (ADR 0007, sections 2 and 3).
- Every call and its result show in the conversation window and are stored with the turn.
- Two first tools of class "Read": date and time, calculation.
- The orb stays in thinking during a call; an interruption cancels the running call.

### 4. Confirmation (`stage2/4-confirmation`)

- The confirmation request of ADR 0009, section 3: summary spoken aloud, exact detail shown next to the
  orb, Approve and Deny buttons, timeout, denied by default.
- Spoken answer recognized by code: closed list of words per language, only in the listening opened after
  the question.
- Strong channel for class "Critical": the click only at this stage (the phone comes at stage 4).
- Log of requests and answers, with the channel.
- A fake tool of class "Sensitive" in the Web host proves the path through Playwright.

### 5. Basic tools (`stage2/5-basic-tools`)

- File reading inside the allowed folders: "Allowed folders" page, normalized paths (links and junctions
  resolved), logged denial outside the list.
- Web search and HTTP request, behind contracts; the first search provider is an API with a key
  (answer 2). Both stay off
  until the user turns them on.
- The content of a page or a file goes back to the model as data, cut to an adjustable size.

### 6. MCP client (`stage2/6-mcp-client`)

- Adding a server by address or by local command, connection test with a named cause, list of exposed
  tools, per-tool activation (US2.2).
- An MCP tool enters the registry of work item 3 with a class: the one ADR 0018 sets by default, which the
  user can only make stricter.
- Tokens and headers in the vault.

### 7. Home Assistant (`stage2/7-home-assistant`)

- The Home Assistant MCP server is configured like the others; "Allume le salon" and "turn on the living
  room" work (US2.3).
- Security home automation (lock, alarm, garage door) recognized as ADR 0018 says and classed "Critical":
  click required, voice is not enough.

### 8. Memory (`stage2/8-memory`)

- Persistent memory and automatic summary of old exchanges as ADR 0019 says; the summary goes into the
  system message in place of the turns it replaces.
- "Memory" page: see what Jarvis keeps, erase one entry, erase everything.

### 9. Hand-off (`stage2/9-hand-off`)

- `hand_off_to_claude` tool with a reason (US2.5); "demande à Claude" and "ask Claude" recognized by code
  and sent straight to the target (US2.6).
- Targets page: one for tasks with files or steps (Claude Code, wired in work item 12), one for hard
  questions (`anthropic` or `openai` provider) (US2.7).
- The conversation says who answers; nothing changes on the orb (ADR 0020, answer 2). The screen that turns on a cloud target says what leaves
  the machine.

### 10. Spend cap (`stage2/10-spend-cap`)

- Adjustable prices per model, cost computed from the tokens already recorded with every answer (ADR 0007,
  section 7), visible monthly counter.
- Cap reached: Jarvis says so and stays local (US2.8). The cap of each Claude Code session
  (`--max-budget-usd`) derives from it.

### 11. Tasks and sessions (`stage2/11-tasks-and-sessions`)

- Task shape and its nine statuses in the kernel (ADR 0013, section 6), stored in `jarvis.db`.
- Local server, listening on `localhost` only, for the `/hook/…` routes; installing and removing the hooks
  in the user's Claude Code settings, with their consent, as ADR 0021 says.
- Reading the session registry (spikes E2 and E3): list grouped by project, with folder, state and last
  activity, hand-started sessions and Claude Desktop sessions included (US2.11, US2.14).
- Jarvis never reads `sessions/*.key` or `ide/*.lock`; a test proves it.

### 12. Claude Code tasks (`stage2/12-claude-code-tasks`)

- "Ask Claude to…" on an allowed folder: Jarvis picks or asks for the folder, creates the task, starts
  `claude -p` with its own settings and moves to "delegated task" (US2.9).
- Read-only by default; every write or command arrives through the permission tool and follows work item 4
  (US2.10, US2.15). For sessions Jarvis did not start, it only announces.
- End of session: summary spoken aloud, cost added to the counter of work item 10.

### 13. Announcements (`stage2/13-session-announcements`)

- Announcement when a session ends or waits for a permission, adjustable per folder, silent during quiet
  hours (US2.12).
- "Where is Claude at on Kazo Bar?": summary made by the local model from the session, whose content is
  data (US2.13).

### 14. Fan (`stage2/14-task-fan`)

- The fan of ADR 0010, fed by the tasks of work item 11: one panel per task, sub-tasks inside the panel,
  amber for `waiting_answer` and `waiting_approval`, Approve and Deny in the panel.
- Double click on the orb to open and close; the single click still starts listening, delayed by the
  double-click time.
- Measurements of ADR 0010 redone on the real host.

### 15. Acceptance of the stage (`stage2/15-acceptance`)

- On Benoit's PC, in French then in English: turn on a light, ask a question the local model hands to
  Claude, start a Claude Code task on an allowed folder, approve a write, hear the end announcement.
- List of outbound connections per setting, checked one by one; none with the default configuration,
  except the update check.
- `features.md` moves stage 2 to "Available", the project memory says the series is over.
- Benoit merges `stage2/base` into `main`.

## Windows checks at a glance

| Check | Work item |
| --- | --- |
| Spikes E11 to E14 | 1 |
| Reliable tool calls with the real `qwen3:14b`, in both languages | 3 |
| Spoken confirmation with the real microphone: "yes", "no", any other sentence means denial | 4 |
| Windows junctions and links denied outside the allowed folders | 5 |
| MCP server started by local command (process closed on "Quit") | 6 |
| A real light; a lock or the alarm asks for the click | 7 |
| Real hand-off to the API, delay before the first sound | 9 |
| Hooks installed then removed from Benoit's settings without losing anything | 11 |
| Real Claude Code session: write denied, then approved | 12 |
| Announcement of a hand-started session; quiet hours | 13 |
| Cost of the fan, click-through, comfort of the single click | 14 |

## Benoit's answers

Questions asked in the conversation on 2026-11-20. Benoit answered the same day: "I agree with your
recommendations. You can also merge all the branches into main (through a PR) then merge the PRs into
main."

1. **Starting point of `stage2/base`.** `stage1/base` is merged into `main` right now, through a PR the
   agent merges with Benoit's consent; `stage2/base` starts from `main`. The stage 1 acceptance (work item
   17) and the pending Windows checks remain to be done with him; their fixes go through a small separate
   series.
2. **Web search provider.** An API with a key (Brave Search), the key in the vault, behind a contract so
   others can be added. The terms of the free plan are checked when ADR 0017 is written.
3. **Order of the first work items.** Spikes, then ADRs, then tools; the fan towards the end, as in the
   table.

## Out of this series

Scheduled tasks, triggers (including Home Assistant events through WebSocket), Gmail and Calendar: stage 3.
Mobile app and confirmation from the paired phone: stage 4. Relais module and work tracking (ADRs 0013 and
0015): v2. "Command post" full screen: not planned yet.
