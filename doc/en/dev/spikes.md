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
