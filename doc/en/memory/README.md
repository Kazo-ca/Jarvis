# Project memory

Resume point for any agent. Update it in the same change as what it describes.

## State

- **Current series:** `stage1` (stage 1), branch `stage1/base`, taken from `main` on November 19, 2026.
  Work item 1 (`stage1/1-solution`, solution skeleton, architecture tests, continuous integration)
  merged and checked on Windows. Work item 2 (`stage1/2-kernel`, message bus, state machine, sound level,
  see [dev/kernel.md](../dev/kernel.md)) delivered in its PR; next work item:
  `stage1/3-settings-storage-vault`. Benoit merges `main`; an agent merges its own PRs
  into a `<series>/base` branch once checks are green (rule restated by Benoit on November 19, 2026).
- **Series `spikes` (finished, merged into `main`):** spikes E1 to E10. Done on November 18, 2026: E2 to E9. E1 dropped (no 3CX). Stage 5 was rewritten on November 19, 2026: calls between Jarvis and the Android app, both ways (US5.1 to US5.9 of the design document), no ADR for now; it depends on E10. Waiting: E10
  (Benoit's Android phone). Summary: [dev/spikes.md](../dev/spikes.md).
- **Next free ADR number:** 0016. **ADR 0015 (work tracking source: GitHub Issues or Azure Boards; `#<id>` for GitHub, `AB#<id>` for Azure DevOps in commits and PRs): accepted** by Benoit on November 19, 2026 (work item `stage1/work-tracking-adr`), with confirmation before any creation or change, feature for v2 with Relais. **ADR 0014 (Apache 2.0 license, rule on Kazo Bar, removal of `jarvis.py`): accepted** by Benoit on
  November 19, 2026 (work item `stage1/license-kazo-bar-android-calls`). Kazo Bar is never referenced (no project, no package); its code may
  be copied here and then evolves separately. `jarvis.py` is no longer used. Making the repository public is Benoit's to do. **ADR 0013 (preparing the Relais module, v2): accepted** by Benoit on
  November 19, 2026 (work item `stage1/relais-architecture-adr`); nothing is built before v2, except the
  generic host in work item `stage1/4-hosts`. The same day Benoit chose
  a tunnel for the whole Relais bus (closed by default, no route without a token). ADR 0012 (script is allowed in markup, style stays forbidden):
  accepted by Benoit on November 19, 2026, partly supersedes ADR 0002, section 7. ADRs 0001 to 0009 (stage 1) were **accepted** by Benoit on
  November 19, 2026 (work item `spikes/11-stage-1-adrs`). They are no longer edited: a new ADR supersedes them.
- **ADR 0010 (task fan and click on the orb): accepted** by Benoit on November 19, 2026 (work item
  `spikes/12-task-fan-adr`). It is only built at stage 2. Benoit chose the "fan" mockup, on the computer
  only, with the orb in any corner of the screen: amber decision, dimmed desktop, animation at opening and
  closing only. Benoit's answer of November 19, 2026 about the click: **the double click opens the fan,
  the single click starts listening** (US1.6 and ADR 0005 do not change).
- **Spike to run at the start of stage 2:** E11, cost and smoothness of the transparent fan window
  (ADR 0010).
- **ADR 0011 (colors and drawing of the orb): accepted** by Benoit on November 19, 2026 (work item
  `spikes/14-orb-look-adr`). It carries the validated catalog: two versions
  (high quality by default, light as a setting), seven looks, a progress ring in place of the satellites,
  an amber badge for decisions. During a long task, the orb turns slowly as in the prototype:
  Benoit's choice on the card of November 19, 2026, cost of 6 to 18 % of one core accepted.
- **To measure at stage 1 (ADR 0011):** the cost of both orb versions on the real host, idle and animated.
  If the high-quality one clearly exceeds 14 to 18 % of one core, the light one becomes the default.
- **Decisions taken but not built:** the whole design document.
- **Decisions from the spikes (taken on November 18, 2026, carried into ADRs 0003 to 0009):** see
  [dev/spikes.md](../dev/spikes.md#decisions-taken-on-november-18-2026). Still idle orb, `qwen3:14b`
  without thinking, Piper by default, E10 postponed until before stage 4.
- **Next step:** work item `stage1/3-settings-storage-vault` of the [stage 1 series plan](../dev/plans/stage-1.md),
  **validated** by Benoit on November 19, 2026 (17 work items `stage1/<n>-<topic>`). Build and test:
  [dev/build.md](../dev/build.md).
- **Benoit's answers of November 19, 2026 on the plan:** GitHub Actions continuous integration on Linux
  (Windows tests on his PC); a written request gets a written answer only; the Windows checks are done by an
  agent on his PC through Remote Control, by him only for his voice and ears (work items 9, 10, 12, 13
  and 17).
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
  [dev/ui/catalog.md](../dev/ui/catalog.md), prototypes in `design/prototypes/`. ADR 0011 builds on
  this catalog.
- **Licenses to settle before a public distribution:** espeak-ng (GPLv3, in sherpa-onnx 1.13.8),
  openWakeWord models (CC BY-NC-SA 4.0), the license of each voice (ADR 0006).
- **Public repository:** Benoit confirmed on November 19, 2026 that `Kazo-ca/Jarvis` is public ([ADR 0014](../dev/adr/0014-license-kazo-bar-rule-and-jarvis-py.md)). The
  version check (US1.18) therefore needs no token; the ADR 0001 consequence about it no longer applies.
- **Known test failures:** none.
- **Windows check of work item 1: done** on November 19, 2026 by an agent on Benoit's PC (SDK 10.0.402):
  build without warnings, unit tests 29/29, Playwright 1/1, Web host HTTP 200, Windows host opened,
  responsive and closed without error. **To handle in work item 4:** started on a locked session, the
  Windows host dies (see [pitfalls](../dev/pitfalls.md)).
- **Choice of work item 2 that ADR 0005 does not settle:** an interruption while the microphone is muted or
  missing stops Jarvis without starting to listen ([dev/kernel.md](../dev/kernel.md#case-adr-0005-does-not-cover)).

## Benoit's development environment

- GPU: NVIDIA RTX 3060 12 GB. With a 14B model and Whisper turbo, about 600 MB remain free.
- Ollama runs in Docker on port **11435** (not 11434). Models: `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` and `qwen2.5:14b` (added for E7).
- llama-server on `127.0.0.1:8080`.
- Secrets, models and throwaway artifacts go to `C:\jarvis\temp` (models in `C:\jarvis\temp\models`),
  never into the repository.
