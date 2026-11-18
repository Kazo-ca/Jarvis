# Project memory

Resume point for any agent. Update it in the same change as what it describes.

## State

- **Current series:** `spikes` (technical spikes E1 to E10), branch `spikes/base`.
  Done on November 18, 2026: E2 to E9. E1 dropped (no 3CX, stage 5 removed). Waiting: E10
  (Benoit's Android phone). Summary: [dev/spikes.md](../dev/spikes.md).
- **Next free ADR number:** 0001.
- **Decisions taken but not built:** the whole design document.
- **Decisions from the spikes (taken on November 18, 2026, to write in the ADRs):** see
  [dev/spikes.md](../dev/spikes.md#decisions-taken-on-november-18-2026). Still idle orb, `qwen3:14b`
  without thinking, Piper by default, E10 postponed until before stage 4.
- **Next step:** write ADRs 0001 to 0009 of stage 1 (listed in the design), before any code.
- **Known test failures:** none (no tests yet).

## Benoit's development environment

- GPU: NVIDIA RTX 3060 12 GB. With a 14B model and Whisper turbo, about 600 MB remain free.
- Ollama runs in Docker on port **11435** (not 11434). Models: `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` and `qwen2.5:14b` (added for E7).
- llama-server on `127.0.0.1:8080`.
- Secrets, models and throwaway artifacts go to `C:\jarvis\temp` (models in `C:\jarvis\temp\models`),
  never into the repository.
