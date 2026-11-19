# Project memory

Resume point for any agent. Update it in the same change as what it describes.

## State

- **Current series:** `spikes` (technical spikes E1 to E10), branch `spikes/base`.
  Done on November 18, 2026: E2 to E9. E1 dropped (no 3CX, stage 5 removed). Waiting: E10
  (Benoit's Android phone). Summary: [dev/spikes.md](../dev/spikes.md).
- **Next free ADR number:** 0011. ADRs 0001 to 0009 (stage 1) were **accepted** by Benoit on
  November 19, 2026 (work item `spikes/11-stage-1-adrs`). They are no longer edited: a new ADR supersedes them.
- **ADR 0010 (task fan and click on the orb): accepted** by Benoit on November 19, 2026 (work item
  `spikes/12-task-fan-adr`). It is only built at stage 2. Benoit chose the "fan" mockup, on the computer
  only, with the orb in any corner of the screen: amber decision, dimmed desktop, animation at opening and
  closing only. Benoit's answer of November 19, 2026 about the click: **the double click opens the fan,
  the single click starts listening** (US1.6 and ADR 0005 do not change).
- **Spike to run at the start of stage 2:** E11, cost and smoothness of the transparent fan window
  (ADR 0010).
- **ADR to come during stage 1:** the orb, from a mockup (referred by ADR 0005): colors and drawing of the
  six looks, values of the cyan and the amber, signal of a pending decision when the fan is closed.
- **Decisions taken but not built:** the whole design document.
- **Decisions from the spikes (taken on November 18, 2026, carried into ADRs 0003 to 0009):** see
  [dev/spikes.md](../dev/spikes.md#decisions-taken-on-november-18-2026). Still idle orb, `qwen3:14b`
  without thinking, Piper by default, E10 postponed until before stage 4.
- **Next step:** write the plan of the stage 1 series, then code. Benoit decides when `spikes/base` goes to
  `main`.
- **Benoit's answers of November 19, 2026 (written in the ADRs):** SQLite database for the conversation,
  memory and log (ADR 0004); `Microsoft.Extensions.AI` abstractions (ADR 0007); a click required for security
  home automation only (ADR 0009); assumptions 1, 2, 3, 7 and 9 of the design accepted; data goes to
  `%USERPROFILE%\.kazo\jarvis`, not `%APPDATA%` (the folder name was chosen by the agent; changing it
  needs a new ADR).
- **To check at the start of stage 1** (the spikes did not): wake word with Benoit's voice and while Jarvis
  speaks, real microphone, end of utterance through Silero, click-through around the orb, idle cost of the
  orb on the real host, window reload on a language change.
- **UI designs validated on November 19, 2026:** high-quality orb (plasma, binary clouds, gyroscope rings),
  light orb (still sphere, three open rings), full-screen "command post". Described in
  [dev/ui/catalog.md](../dev/ui/catalog.md), prototypes in `design/prototypes/`. The upcoming orb ADR builds on
  this catalog.
- **Licenses to settle before a public distribution:** espeak-ng (GPLv3, in sherpa-onnx 1.13.8),
  openWakeWord models (CC BY-NC-SA 4.0), the license of each voice (ADR 0006).
- **Known test failures:** none (no tests yet).

## Benoit's development environment

- GPU: NVIDIA RTX 3060 12 GB. With a 14B model and Whisper turbo, about 600 MB remain free.
- Ollama runs in Docker on port **11435** (not 11434). Models: `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` and `qwen2.5:14b` (added for E7).
- llama-server on `127.0.0.1:8080`.
- Secrets, models and throwaway artifacts go to `C:\jarvis\temp` (models in `C:\jarvis\temp\models`),
  never into the repository.
