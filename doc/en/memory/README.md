# Project memory

Resume point for any agent. Update it in the same change as what it describes.

## State

- **Current series:** `spikes` (technical spikes E1 to E10), branch `spikes/base`.
  Done on November 18, 2026: E2 to E9. E1 dropped (no 3CX, stage 5 removed). Waiting: E10
  (Benoit's Android phone). Summary: [dev/spikes.md](../dev/spikes.md).
- **Next free ADR number:** 0010. ADRs 0001 to 0009 (stage 1) are written and **proposed** on
  November 18, 2026 (work item `spikes/11-stage-1-adrs`); they wait for Benoit's approval.
- **ADR to come during stage 1:** colors and drawing of the orb, from a mockup (referred by ADR 0005).
- **Decisions taken but not built:** the whole design document.
- **Decisions from the spikes (taken on November 18, 2026, carried into ADRs 0003 to 0009):** see
  [dev/spikes.md](../dev/spikes.md#decisions-taken-on-november-18-2026). Still idle orb, `qwen3:14b`
  without thinking, Piper by default, E10 postponed until before stage 4.
- **Next step:** get Benoit's approval on ADRs 0001 to 0009, set their status to "Accepted", then write the
  plan of the stage 1 series. Still no code.
- **Questions put to Benoit on November 18, 2026 (the ADRs keep the recommended option):**
  1. Storage: SQLite database for the conversation, memory and log, or JSON files like Kazo Bar (ADR 0004).
  2. LLM provider contract: `Microsoft.Extensions.AI` abstractions or an in-house contract (ADR 0007).
  3. Actions that require a click, voice not being enough: security home automation only (ADR 0009).
  4. Assumptions 1, 2, 3, 7 and 9 of the design, on which the ADRs rely.
- **To check at the start of stage 1** (the spikes did not): wake word with Benoit's voice and while Jarvis
  speaks, real microphone, end of utterance through Silero, click-through around the orb, idle cost of the
  orb on the real host, window reload on a language change.
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
