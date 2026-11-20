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
  Windows, and for work item 7: the same measures for the high-quality version (ADR 0011, section 8),
  quality change without a restart, look at 160 pixels ([list](../dev/orb.md#windows-checks)), and for work item 8: real microphone
  and level, muted microphone (Windows no longer shows an application listening, mute kept after a restart),
  microphone unplugged while Jarvis runs, test sound on speakers and headset, cost of the open microphone
  while idle ([list](../dev/audio.md#windows-checks)), and for work item 10: Whisper and Silero models copied by hand,
  end of utterance with the real microphone and some noise (fallback to the energy threshold if Silero is not
  reliable), text in French and English, transcription time ([list](../dev/transcription.md#windows-checks)), and for work item 12: Benoit listening to the voices
  (perceived quality, speed, sound card delay, clean stop, [list](../dev/speech.md#windows-checks)), and for work item 9: **wake word threshold tuned with
  Benoit's voice** (value to write here), "Hey Jarvis" while Jarvis speaks, cost of the wake word
  ([list](../dev/wake-word.md#windows-checks)), and for work item 13: **delay between the end of the sentence and the
  first sound** (target under 2 s, value to write here), "Hey Jarvis" while Jarvis speaks (otherwise an ADR limits
  the interruption to the hotkey and the click), conversation window and personality
  ([list](../dev/conversation.md#windows-checks)). Work item 16 (`stage1/16-updates`, updates: slice
  `Maintenance/Updates`, reading GitHub releases, `Kazo.Jarvis.Updater` program, `build/Publish-Release.ps1`
  script, see [dev/updates.md](../dev/updates.md)) delivered in its PR; its Windows check waits for the next
  session. Work item 5 (`stage1/5-shared-ui-language`, shared style, embedded fonts, `.resx` translations,
  language setting, settings window, secret field, see [dev/shared-ui.md](../dev/shared-ui.md)) merged (PR 22),
  without a Windows check (Benoit stopped the tests on November 20, 2026). Benoit merges `main`; an agent merges its own PRs
  into a `<series>/base` branch once checks are green (rule restated by Benoit on November 19, 2026).
- **Series `spikes` (finished, merged into `main`):** spikes E1 to E10. Done on November 18, 2026: E2 to E9. E1 dropped (no 3CX). Stage 5 was rewritten on November 19, 2026: calls between Jarvis and the Android app, both ways (US5.1 to US5.9 of the design document), no ADR for now; it depends on E10. Waiting: E10
  (Benoit's Android phone). Summary: [dev/spikes.md](../dev/spikes.md).
- **Next free ADR number:** 0017 (0017 to 0021 are reserved by the [stage 2 plan](../dev/plans/stage-2.md); the next one is 0022). **ADRs 0019 (conversation memory) and 0020 (hand-off, targets, spend cap): accepted** by Benoit on November 20, 2026 (work item `stage2/2-adrs-memory-and-hand-off`); written before spikes E11 to E14 because they do not depend on them. His choices: memory on request and automatic; exchange old after 2 hours of silence, adjustable; memory not sent to the cloud, but a session started by Jarvis gets a summary of the previous exchanges and the full initial request; nothing on the orb when Claude answers; cap of US$10; setting "Claude Code is paid by subscription" checked by default. **ADR 0016 (a single native ONNX Runtime, the one from sherpa-onnx; `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 for the wake word, supersedes one line of ADR 0001): accepted** by Benoit on November 20, 2026 (work item `stage1/onnx-runtime-adr`). **ADR 0015 (work tracking source: GitHub Issues or Azure Boards; `#<id>` for GitHub, `AB#<id>` for Azure DevOps in commits and PRs): accepted** by Benoit on November 19, 2026 (work item `stage1/work-tracking-adr`), with confirmation before any creation or change, feature for v2 with Relais. **ADR 0014 (Apache 2.0 license, rule on Kazo Bar, removal of `jarvis.py`): accepted** by Benoit on
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
- **Work item 7** (`stage1/7-orb-high-quality`, orb high-quality version: plasma sphere and binary clouds in
  a canvas capped at 30 images per second, five gyroscope rings in SVG and CSS 3D turned by the browser,
  "orb quality" setting in section `Assistant.Orb` and "Orb" settings page, applied without a restart, see
  [dev/orb.md](../dev/orb.md)) delivered in its PR on November 20, 2026, without a Windows check. **The
  measure of ADR 0011 (section 8) is still to be done on Windows**: it decides whether the high quality stays
  the default. Indicative measure in a container without a graphics card: about 21 % of the main thread while
  animated against under 1 % for the light version, 0 % when idle.
- **Work item 11** (`stage1/11-llm-providers`, language models: kernel contracts `ILlmConnector` and
  `IAssistantModel` on `Microsoft.Extensions.AI.Abstractions` 10.11.1, `ollama`, `openai-compatible`/`openai`
  and `anthropic` adapters written on `HttpClient`, slice `Llm/Providers` with the "Language models" page,
  default model preloaded at start, see [dev/llm.md](../dev/llm.md)) delivered in its PR on 2026-11-20,
  without a Windows check. **Pending Windows tests** ([list](../dev/llm.md#checks-on-windows)): Ollama on
  11435 and `llama-server` on 8080, preload at start, named causes, measure of a 7 to 8 billion parameter
  fallback model. **Provisional choices** ([detail](../dev/llm.md#provisional-choices-of-this-work-item)):
  default port 11434 (Benoit sets 11435 once), `claude-opus-5-5` for a new Anthropic provider, Anthropic
  adapter on `HttpClient` rather than the SDK, answers of 16,000 tokens at most, 20 s connection test.
- **Work item 8** (`stage1/8-audio-devices`, audio devices: contracts `IAudioCapture` and `IAudioPlayback`,
  kernel `MicrophoneFeed` (microphone stream and 30 s in-memory buffer, for the wake word and transcription),
  slice `Audio/Devices` (microphone, mute US1.13, "microphone missing" fault, output, "Audio" page), WASAPI
  through NAudio 3.3 in the Windows host, see [dev/audio.md](../dev/audio.md)) delivered in its PR on
  November 20, 2026, without a Windows check. **Provisional choices** (details in
  [dev/audio.md](../dev/audio.md#provisional-choices-of-this-work-item), to be confirmed by Benoit): mute
  survives a restart; chosen microphone missing = the Windows default, with no fault; muting clears the
  "microphone missing" fault; 30 s buffer; level in decibels from −60 to 0; conversion to 16 kHz done by
  Jarvis.
- **Work item 9** (`stage1/9-wake-word`, wake word: kernel contracts `IWakeWordEngine` and
  `IWakeWordDetector`, ONNX Runtime adapter of the three openWakeWord 0.5.1 models in `models\wake-word`, slice
  `Voice/WakeWord` (listens to the microphone stream, threshold 0.5 to set, one detection per activation,
  silent while listening, "Wake word" page with the live score), see [dev/wake-word.md](../dev/wake-word.md))
  delivered in its PR on November 20, 2026, without a Windows check. Piper "Hey Jarvis.": 0.999; "Hey
  Travis…": 0.488. **Provisional choices** ([details](../dev/wake-word.md#provisional-choices-of-this-work-item),
  to be confirmed by Benoit): models missing = no fault (the page says so, another try every 30 s); active
  while thinking and speaking, paused while listening; slider from 0.05 to 0.95; best score of the last 3
  seconds shown; `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 on sherpa-onnx's native engine instead of
  `Microsoft.ML.OnnxRuntime` 1.32.0 from ADR 0001 (two native `onnxruntime` broke Piper, see the
  [pitfalls](../dev/pitfalls.md)): **[ADR 0016](../dev/adr/0016-onnx-runtime-managed-on-sherpa-native.md), accepted, replaces that line of ADR 0001**.
- **Work item 10** (`stage1/10-transcription`, end of utterance and Whisper: kernel contracts
  `ISpeechRecognition` and `IVoiceActivityDetection`, message `UtteranceTranscribed` for the conversation,
  slice `Voice/Transcription` (listening to a spoken turn, end of utterance by Silero with an energy-threshold
  fallback, Whisper loaded once, "Transcription" page with the last request heard), adapters Whisper.net 1.9.1
  and Silero through sherpa-onnx 1.13.8, see [dev/transcription.md](../dev/transcription.md)) delivered in its
  PR on November 20, 2026, without a Windows check. Silero tried in the container on a real recording: with
  500 ms the utterance ends at the first natural pause (to watch with Benoit's voice). **Provisional choices**
  ([details](../dev/transcription.md#provisional-choices-of-this-work-item)): 8 s wait for speech, utterance
  cut at 25 s, 150 ms of voice to start, 300 ms kept before speech, silent fallback to the energy threshold, no
  fault at start when Whisper is missing (only `TurnFailed` at the end of a turn), empty text published as is
  (work item 13 ends the turn), models in `models\whisper\` and `models\vad\`.
- **Work item 12** (`stage1/12-speech`, speech: kernel contracts `ISpeechEngine` and `IVoiceSelection`,
  messages `SentenceToSpeak` and `AnswerSentencesEnded` for the conversation, slice `Voice/Speech` (sentences
  played in order, clean stop on interruption, voice level to the orb), slice `Voice/TestVoice` ("Voices"
  page), Piper and Kokoro through sherpa-onnx 1.13.8, ElevenLabs and OpenAI, see
  [dev/speech.md](../dev/speech.md)) delivered in its PR on November 20, 2026. Piper tried for real on Linux;
  **listening to the voices waits for Benoit** ([list](../dev/speech.md#windows-checks): perceived quality,
  speed, sound card delay, clean stop). **Provisional choices** ([detail](../dev/speech.md#provisional-choices-of-this-work-item)):
  the voice slice publishes `FirstSentenceReady` at the first sound; one voice per turn; a failing cloud voice
  fails the turn; chosen voice missing = local voice of the language; speed per language; separate OpenAI
  voice key.
- **Work item 13** (`stage1/13-conversation`, the full turn: slice `Conversation/Dialogue` (engine of the turn by
  messages, sentence splitting, system message with the personality and fixed rules, history in half of the
  context, collection `Conversation.Turns`, conversation window with "Erase everything", "Personality" page), the
  voice also stops on `TurnFailed`, rank of the settings pages fixed (wake word 25, transcription 30, voices 35),
  see [dev/conversation.md](../dev/conversation.md)) delivered in its PR on 20 November 2026, without a Windows
  check. The kernel did not change. **Provisional choices**
  ([detail](../dev/conversation.md#provisional-choices-of-this-work-item), for Benoit to confirm): end of sentence
  and abbreviations, Markdown not said, history of 20 turns at most without age limit, turn stored when it ends,
  personalities of both languages on one page, typed request sent only when idle.
- **Work item 14** (`stage1/14-first-run`, first run: kernel contracts `IModelInstaller` and `IGraphicsCardProbe`,
  messages `WhisperModelChosen` and `FirstRunWizardRequested`, adapters `HttpModelInstaller` (size and checksum
  checked, `.tar.bz2` unpacked with SharpZipLib) and `NvidiaSmiGraphicsCardProbe`, slice `Setup/Models` ("Voice
  models" page, advice from the graphics card), slice `Setup/FirstRun` (wizard: language, audio, voice models,
  language model; section `Setup.FirstRun`), Ollama search on 11434 then 11435 in the "Language models" page,
  "Setup wizard…" tray menu entry, see [dev/first-run.md](../dev/first-run.md)) delivered in its PR on 20 November
  2026, without a Windows check. Real download tried in the container (GitHub); **Whisper (Hugging Face) never
  really downloaded**. **Provisional choices** ([detail](../dev/first-run.md#provisional-choices-of-this-work-item),
  to be confirmed by Benoit): 10 GB for the full chain, `small` below, `base` on the processor without NVIDIA,
  `qwen3:8b` advised as fallback; SHA-1 for Whisper; dependency SharpZipLib 1.4.2; no "Voices" step; both Piper
  voices checked by default; automatic Ollama search while the providers are the default ones. The Web host now
  simulates an existing profile.
- **Work item 15** (`stage1/15-faults`, faults: kernel `FaultOrigin` and `AssistantFault.Origin`, `FaultResolved` with
  an origin, `SettingsWindowRequested(PageKey)`, `FaultWindowRequested`; slice `Assistant/Faults` (title, explanation,
  corrective action and page of each cause, "Jarvis problem" window, unreadable settings raised at start); language
  model fault raised and cleared outside a turn, Whisper found again and a voice heard clear theirs; "Solve the
  problem…" tray entry during a fault, see [dev/faults.md](../dev/faults.md)) delivered in its PR on 20 November 2026,
  without Windows checks. The transition table did not change. **Provisional choices**
  ([detail](../dev/faults.md#provisional-choices-of-this-work-item), to be confirmed by Benoit): window opened by the
  menu entry, and by itself only on a refusal for want of a microphone; unreachable server raised after two failed
  loads in a row; the "Voices" page clears the voice fault; no button for unreadable settings; origin in the kernel.
- **Stage 2 plan: validated** by Benoit on November 20, 2026 (work item `stage1/stage-2-plan`, [plan](../dev/plans/stage-2.md)): 15 work items `stage2/<n>-<topic>`, first spikes E11 to E14 on his PC then ADRs 0017 to 0021 (numbers reserved by the plan). His answers: `stage1/base` merged into `main` without waiting for the acceptance (he allowed the agent to merge that PR), `stage2/base` starts from `main`; web search through an API with a key (Brave Search) behind a contract; spikes, then ADRs, then tools, the fan towards the end.
- **Next step:** series `stage2`, branch `stage2/base` started from `main`, work item `stage2/1-spikes` (spikes E11 to E14 on Benoit's PC, through remote control). `stage1/base` was merged into `main` on November 20, 2026 **without acceptance** (Benoit's decision): work item `stage1/17-acceptance` remains (acceptance on Benoit's PC, with him), and its fixes will go through a small separate series. The pending Windows tests (work items 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 and 15, [list](../dev/hosts.md#windows-checks)) are done at the next session with Benoit. Plan: the [stage 1 series plan](../dev/plans/stage-1.md),
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
  [dev/orb.md](../dev/orb.md#provisional-choices), for Benoit to confirm): error = flash then
  still image (not the catalog's speed 0.25); reduced motion = no rotation or pulse, listening and speaking
  told from idle by light only; unknown progress = still dashed green circle; orb body at 80 % of the window;
  "99+" badge. The click is still caught by the WPF window, which hands it to the slice's `OrbActivation`.
- **Provisional choices of work item 7** (details in [dev/orb.md](../dev/orb.md#provisional-choices), for
  Benoit to confirm): the prototype's proportions kept (sphere at 30 % of the side, so about 48 pixels in the
  160-pixel window, cloud digits reduced to a texture); each look's light shared by both versions; error and
  reduced motion as in the light version; gyroscope rocking as a 25 s back-and-forth; halo in CSS; "Orb" page
  after "Language" in the settings.
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
