# Project memory

Resume point for any agent. Update it in the same change as what it describes.

## State

- **Current series:** `stage1` (stage 1), branch `stage1/base`, taken from `main` on November 19, 2026.
  Work item 1 (`stage1/1-solution`, solution skeleton, architecture tests, continuous integration)
  merged and checked on Windows. Work item 2 (`stage1/2-kernel`, message bus, state machine, sound level,
  see [dev/kernel.md](../dev/kernel.md)) merged (PR 17). Work item 3
  (`stage1/3-settings-storage-vault`, `config.json` settings, SQLite database, secret vault, see
  [dev/storage.md](../dev/storage.md)) merged (PR 18). Work item 4 (`stage1/4-hosts`, Windows host on the .NET
  generic host, provisional orb, tray icon, global hotkey, start with Windows, dialogs, wait for the unlock,
  see [dev/hosts.md](../dev/hosts.md)) merged (PR 20). Windows check done on November 20, 2026 (build without
  warnings, tests 286/286 and E2E 11/11, clicks through the orb, second launch, hotkey without focus loss,
  still cost 0.8 % of a core). Three defects found, fixed by `stage1/4b-hosts-fixes`: lock not detected on
  Windows 11 (the lock screen is on the `Default` desktop), × 1.5 drift of the position with screens of
  different scales, no state change in the log. Benoit stopped the tests on November 20, 2026: **pending
  Windows tests** (to group for a next session with him): start on a locked session (hide the camera:
  Windows Hello unlocks by itself), position after a restart with screens of different scales, line
  "ActivationRequested: Idle -> Listening" in the log after `Ctrl+Alt+J`, Quit within 10 s, orb size after
  the screens reconnect (240 × 240 seen once on the 100 % screen, right after a restart), an end-to-end update between two test versions (work item 16,
  [list](../dev/updates.md#windows-check)), and for work item 5: settings window texts reloaded on a language
  change in `BlazorWebView`, tray menu translated, fonts shown offline ([list](../dev/hosts.md#windows-checks),
  item 8), and for work item 6: cost of the light orb when idle, muted and in error under 2 % of a core
  (mandatory), cost of the animated phases and of the long task, click and drag, animations turned off in
  Windows ([list](../dev/orb.md#windows-checks)). Work item 16 (`stage1/16-updates`, updates: slice
  `Maintenance/Updates`, reading GitHub releases, `Kazo.Jarvis.Updater` program, `build/Publish-Release.ps1`
  script, see [dev/updates.md](../dev/updates.md)) delivered in its PR; its Windows check waits for the next
  session. Work item 5 (`stage1/5-shared-ui-language`, shared style, embedded fonts, `.resx` translations,
  language setting, settings window, secret field, see [dev/shared-ui.md](../dev/shared-ui.md)) merged (PR 22),
  without a Windows check (Benoit stopped the tests on November 20, 2026). Benoit merges `main`; an agent merges its own PRs
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
- **Work item 6** (`stage1/6-orb`, orb light version: slice `Assistant/Orb`, `Orb` component and `OrbView`
  fed by the state machine, demonstration page `/orb` of the Web host, see [dev/orb.md](../dev/orb.md))
  delivered in its PR on November 20, 2026, without a Windows check.
- **Work item 11** (`stage1/11-llm-providers`, language models: kernel contracts `ILlmConnector` and
  `IAssistantModel` on `Microsoft.Extensions.AI.Abstractions` 10.11.1, `ollama`, `openai-compatible`/`openai`
  and `anthropic` adapters written on `HttpClient`, slice `Llm/Providers` with the "Language models" page,
  default model preloaded at start, see [dev/llm.md](../dev/llm.md)) delivered in its PR on 2026-11-20,
  without a Windows check. **Pending Windows tests** ([list](../dev/llm.md#checks-on-windows)): Ollama on
  11435 and `llama-server` on 8080, preload at start, named causes, measure of a 7 to 8 billion parameter
  fallback model. **Provisional choices** ([detail](../dev/llm.md#provisional-choices-of-this-work-item)):
  default port 11434 (Benoit sets 11435 once), `claude-opus-5-5` for a new Anthropic provider, Anthropic
  adapter on `HttpClient` rather than the SDK, answers of 16,000 tokens at most, 20 s connection test.
- **Next step:** work item `stage1/7-orb-high-quality`, and `stage1/8-audio-devices` and
  `stage1/11-llm-providers`, which depend on work item 5 only. The pending Windows tests (work items 4, 5
  and 6, [list](../dev/hosts.md#windows-checks)) are done at the next session with Benoit. Plan: the [stage 1 series plan](../dev/plans/stage-1.md),
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
  orb on the real host, window reload on a language change (proven in the Web host in work item 5, still to see on Windows).
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
  responsive and closed without error. Started on a locked session, the Windows host died: fixed in work
  item 4 (see [pitfalls](../dev/pitfalls.md)).
- **Provisional choices of work item 6 that ADRs 0005 and 0011 do not settle** (details in
  [dev/orb.md](../dev/orb.md#provisional-choices-of-this-work-item), for Benoit to confirm): error = flash then
  still image (not the catalog's speed 0.25); reduced motion = no rotation or pulse, listening and speaking
  told from idle by light only; unknown progress = still dashed green circle; orb body at 80 % of the window;
  "99+" badge; no "version" input yet (work item 7). The click is still caught by the WPF window, which hands
  it to the slice's `OrbActivation`.
- **Choices of work item 5 that ADRs 0008 and 0011 do not settle** (details in [dev/shared-ui.md](../dev/shared-ui.md)):
  `IStringLocalizer` reads the assistant language at each lookup, never the thread culture (formats keep
  those of Windows); a window is built again whole on a language change (`LanguageScope`), the title bar keeps
  the language it was opened in; setting in the `Settings.Language` section, absent until the user chooses
  (the Windows language applies); settings pages register through the kernel `SettingsPage`; fonts Rajdhani
  500/600, Inter 400/600, JetBrains Mono 400/500 as Latin `woff2` (`@fontsource` 5.3.0, OFL 1.1 license
  checked); the secret field never reads the value back.
- **Choices of work item 16 that ADR 0001 does not settle** (details in [dev/updates.md](../dev/updates.md)):
  automatic check **on by default** (Benoit's choice on November 20, 2026; the one exception to "no outgoing
  connection by default" of work item 17), a minute after start, then every 6 hours; manual check through
  "Updates…" in the tray. Pre-releases offered while the version is 0.x; the updater starts
  from the unpacked new version, without a window; a development build never installs.
- **Choices of work item 4 that ADRs 0003 and 0013 do not settle** (details in [dev/hosts.md](../dev/hosts.md)):
  default hotkey `Ctrl+Alt+J` (section `Desktop.Hotkey`); no contract for the tray icon, its Settings and
  Conversation entries publish an event and stay greyed out without a slice handling it; the click on the orb
  is caught by the WPF window until work item 6; orb position in the `Desktop.OrbWindow` section; technical
  log in daily text files kept 14 days; WebView2 profile in `%USERPROFILE%\.kazo\jarvis\webview`; a
  development build does not register the start with Windows (unless `KAZO_JARVIS_REGISTER_STARTUP=1`).
- **Choices of work item 3 that ADR 0004 does not settle** (details in [dev/storage.md](../dev/storage.md)):
  `config.json` sections at the top level next to `schemaVersion`; the choice of vault in the
  `Secrets.Vault` section; Credential Manager secrets named `Kazo.Jarvis/<reference>`, 2,560 bytes at most;
  `secrets.json` encrypted value by value; a newer database makes storage unavailable without touching it;
  `JarvisPaths` stays in `Adapters` (to move to the kernel if a slice needs the models folder, work items 9
  and 14).
- **Windows check of work items 2 and 3: done** on November 19, 2026 by an agent on Benoit's PC (SDK
  10.0.402): build without warnings, unit tests 252/252 including the 5 `Windows` tests (Credential Manager,
  DPAPI, vault registration), E2E 5/5, no test credential left, Windows host started without a crash and the
  `%USERPROFILE%\.kazo\jarvis` folders created. `C:\jarvis\OpenJarvis` is a clone of OpenJarvis, not of
  Jarvis: the agent cloned the branch into `C:\jarvis\temp\jarvis-check`.
- **Choice of work item 2 that ADR 0005 does not settle:** an interruption while the microphone is muted or
  missing stops Jarvis without starting to listen ([dev/kernel.md](../dev/kernel.md#case-adr-0005-does-not-cover)).

## Benoit's development environment

- GPU: NVIDIA RTX 3060 12 GB. With a 14B model and Whisper turbo, about 600 MB remain free.
- Ollama runs in Docker on port **11435** (not 11434). Models: `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` and `qwen2.5:14b` (added for E7).
- llama-server on `127.0.0.1:8080`.
- Secrets, models and throwaway artifacts go to `C:\jarvis\temp` (models in `C:\jarvis\temp\models`),
  never into the repository.
