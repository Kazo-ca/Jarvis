# Technical spikes

Summary of the ten spikes of the [design document](plans/design.md#technical-spikes-to-do-before-coding),
run on November 18, 2026 on Benoit's PC (RTX 3060 12 GB, .NET 10). Details, measurements and how to rerun
each spike are in `spikes/<name>/` (French).

| Spike | Verdict | In one sentence | Detail |
| --- | --- | --- | --- |
| E1 3CX | **Dropped** | Benoit removed 3CX telephony: only the MAUI app. | [e1-3cx-sip](../../../spikes/e1-3cx-sip/README.md) |
| E2 Claude Code sessions | **Passed** | The `~/.claude/sessions/<pid>.json` registry gives every session and its state (`busy`, `idle`, `waiting`), read in 73 ms. | [e2-claude-sessions](../../../spikes/e2-claude-sessions/RESULTS.md) |
| E3 Claude Desktop | **Passed** | Its code sessions are Claude Code processes listed in the same registry. | [e2-claude-sessions](../../../spikes/e2-claude-sessions/RESULTS.md) |
| E4 voice chain | **Passed, two caveats** | 1.6 to 1.8 s from end of sentence to first sound, in C# with no Python; a French-accented "hey jarvis" is poorly detected; Kokoro is slow on CPU. | [e4-voice-chain](../../../spikes/e4-voice-chain/RESULTS.md) |
| E5 GPU memory | **Passed, thin margin** | Whisper turbo and the 14B model fit together: 11.7 GB of 12.3. Whisper on CPU is too slow. | [e4-voice-chain](../../../spikes/e4-voice-chain/RESULTS.md) |
| E6 WPF orb | **Missed the target** | Animated orb: 6 to 18 % of one core with WebView2, 4 to 7 % in pure WPF. Still frame: 1.3 %. | [e6-orb-window](../../../spikes/e6-orb-window/RESULTS.md) |
| E7 local model tools | **Passed, change model** | qwen3 14B (thinking off): 42 of 42, 0.9 s. qwen2.5-coder writes its calls as text. | [e7-local-tool-calls](../../../spikes/e7-local-tool-calls/RESULTS.md) |
| E8 drive Claude Code | **Passed** | `claude -p` with JSON streams from C#, no SDK: file read, unauthorized writes refused and reported. | [e8-drive-claude-code](../../../spikes/e8-drive-claude-code/RESULTS.md) |
| E9 Home Assistant events | **Not through MCP, yes through WebSocket** | MCP has no notifications; `subscribe_trigger` on the WebSocket API works (4 triggers out of 4). | [e9-home-assistant-events](../../../spikes/e9-home-assistant-events/RESULTS.md) |
| E10 mobile audio | **Pending** | Needs an Android phone and a night of battery measurement. | [e10-mobile-audio](../../../spikes/e10-mobile-audio/README.md) |

## Decisions taken on November 18, 2026

Benoit accepted the recommendations from the spikes. They are carried into the stage 1 [ADRs](adr/README.md),
named at the end of each line.

1. **Idle orb** (E6): still when idle (still frame, 1.3 % of one core); the full animation only runs while
   listening, thinking and speaking. The Razor component shared with mobile is kept.
   ([ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md))
2. **Default local model** (E7): `qwen3:14b` with thinking turned off (`think: false`), plus a lenient parser
   for models that write their tool calls as text. ([ADR 0007](adr/0007-llm-providers-and-tool-format.md))
3. **Default voice** (E4): Piper (sherpa-onnx 1.13.8); Kokoro as an option until it runs on the GPU.
   ([ADR 0006](adr/0006-voice-chain.md))
4. **Wake word** (E4): openWakeWord "hey_jarvis" model; adjustable threshold and score indicator (US1.5),
   tuned with Benoit's voice during stage 1. ([ADR 0006](adr/0006-voice-chain.md))
5. **Driving Claude Code** (E8): `claude -p` with JSON streams, with Jarvis's own settings.
   ([ADR 0009](adr/0009-permission-and-confirmation-model.md) for the principle; the exact command line waits
   for the stage 2 ADR.)
6. **Home Assistant triggers** (E9): WebSocket API (`subscribe_trigger`); MCP for commands.
   ([ADR 0009](adr/0009-permission-and-confirmation-model.md) for the principle; the detail waits for the
   stage 3 ADR.)
7. **Mobile** (E10): stage 1 starts without the phone spikes; they will be done before stage 4.
   ([ADR 0003](adr/0003-hosts-and-native-services.md))

## Stage 2 spikes (November 20, 2026)

Run on Benoit's PC by an agent through remote control (work item `stage2/1-spikes`, see the
[stage 2 plan](plans/stage-2.md)). Benoit had nothing to do.

| Spike | Verdict | In one sentence | Detail |
| --- | --- | --- | --- |
| E11 fan window | **Passed** | WPF, window kept hidden: closed 0.5 %, open and still 0.3 %, animation 33 % of one core for 0.4 s, first frame in 6 ms; clicks land right; single click delayed by 500 ms. Recreated WebView2: 2.4 s. | [e11-task-fan-window](../../../spikes/e11-task-fan-window/RESULTS.md) |
| E12 Claude Code permissions and hooks | **Passed, three surprises** | Denials and approvals of the `--permission-prompt-tool` MCP tool are obeyed; the five hooks reach `/hook/…` and see `RELAIS_TASK_ID`; `SessionStart` only accepts `command` hooks; `CLAUDE.md` comes with the `project` source and its hooks. $0.009 per session. | [e12-claude-code-permissions](../../../spikes/e12-claude-code-permissions/RESULTS.md) |
| E13 tools in English | **Passed** | `qwen3:14b`: 117 of 117 in English, French and mixed, all through the official channel, 0.9 s; `kind` of `hand_off_to_claude` right 43 times out of 45. | [e13-tool-calls-english](../../../spikes/e13-tool-calls-english/RESULTS.md) |
| E14 Home Assistant MCP | **Passed, with a warning** | `ModelContextProtocol` 2.4.0, stateless streamable HTTP: 47 tools as `AIFunction`, read in 0.3 s, a light turned on then back off, recovery after a cut; `HassTurnOff` also unlocks locks, and labels do not go through MCP. | [e14-mcp-home-assistant](../../../spikes/e14-mcp-home-assistant/RESULTS.md) |

## Decisions proposed after E11 to E14

**Accepted by Benoit on 2026-11-20** ("I agree with your recommendations", in answer to the five questions
asked in the conversation). His choices follow the list; where they change a line, they win. They are written
into the work item 2 ADRs (named at the end of each line).

1. **Fan** (E11): WPF rendering in a second transparent window, **created at start-up and kept hidden**
   (never destroyed); 400 ms opening and 300 ms closing animation at 30 frames per second; the orb is put back
   on top whenever the fan window is activated. ADR 0010 holds as is: no superseding ADR. If the panels must be
   Razor views, the same hidden window with `BlazorWebView` also holds (21 ms, 0.6 % closed). (Work item 14.)
2. **Confirming Claude Code writes** (E12): Jarvis exposes a local HTTP MCP server
   (`ModelContextProtocol.AspNetCore`) with an `approve` tool passed through `--permission-prompt-tool`; it is
   **the only decision point**; the `PermissionRequest` hook only moves the task to `waiting_approval`.
   (ADR 0021)
3. **Hooks of sessions launched by Jarvis** (E12): in Jarvis's file passed with `--settings`, of type `http`
   with `allowedEnvVars: ["RELAIS_TASK_ID"]`, except `SessionStart` as a `curl` command. A task is finished
   only at a `result` with no background task; a `UserPromptSubmit` starting with `<task-notification>` is not
   a human request. (ADR 0021)
4. **Settings loaded** (E12): `--setting-sources ""` (nothing from the user or the repository) and the folder's
   instructions through `--append-system-prompt-file CLAUDE.md`; the `project` source (full instructions, but
   repository hooks run without asking) only for folders the user marks as trusted. Full command line:
   [E12, section (d)](../../../spikes/e12-claude-code-permissions/RESULTS.md#d-ligne-de-commande-recommandée-pour-jarvis)
   (French). (ADR 0021)
5. **Tool language** (E13): the ADR 0007 rule is confirmed (names in English, descriptions in the assistant's
   language); `hand_off_to_claude(kind: question|task, reason)` in both languages; `kind` picks the target,
   never a permission. (ADRs 0017 and 0020)
6. **MCP client** (E14): `ModelContextProtocol` 2.4.0 package, streamable HTTP transport (`/api/mcp`) with an
   SSE fallback, token in the `Authorization` header, tools passed as is as `AIFunction`; on a network error,
   retry without rebuilding a session. (ADR 0018)
7. **Security home automation** (E14): the class is decided **after resolving the target entity**, never from
   the tool name: domain `lock`, `alarm_control_panel`, `valve`, `siren`, or `cover` of class `garage`, `door`
   or `gate` = critical; scripts (`script__…`, `intent_script__…`) sensitive by default; an adjustable list of
   "security" entities and scripts (MCP gives neither labels nor zones). (ADR 0018)

### Benoit's answers of 2026-11-20

1. **Fan panels: Razor**, in a `BlazorWebView` window created at start-up and kept hidden, like the rest of
   the interface (point 1: pure WPF rendering is not kept). (Work item 14.)
2. **Claude Code writes: a single decision point**, Jarvis's permission tool, which asks by voice or next to
   the orb (point 2). (ADR 0021)
3. **Repository settings: `CLAUDE.md` alone**, without settings or hooks; the full settings only for folders
   the user marks as "trusted" (point 4). (ADR 0021)
4. **Security home automation: from the targeted device**, plus a list the user completes (point 7).
   (ADR 0018)
5. **Home Assistant scripts: no confirmation by default.** The user marks as "security" those that require
   the click. Point 7 said "sensitive by default": that is not kept. (ADR 0018)

What remains after these spikes: Benoit's judgement of the fan's smoothness and of the single-click comfort,
voice confirmation with a real delay (Claude Code's timeout on the MCP call is not measured), and the 47 Home
Assistant tools given to the local model.
