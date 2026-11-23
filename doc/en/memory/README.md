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
- **Next free ADR number:** 0028 (0026 is taken by the projects view, 0027 by the gestures on the orb). **ADR 0026 (view of projects, subjects and their threads, completes 0013 and 0025): accepted** by Benoit on 22 November 2026 (work item `stage2/adr-0026-projects-view`), from six answers of Benoit about the mockups: four levels of work (Relais), two threads (subject and task); strip of done / in progress / planned tasks; answering a task in the subject thread with "T3>" in the mIRC way; one thread per project; built-in Général project, and Jarvis offers to make a project of a Général subject; project rail on the left; opened by the double click on the orb (ADR 0027). His last three points taken as recommended (section 11: the subject thread shows only what is addressed to the user, subjects created by Jarvis and by the user, project offer from 4 tasks or 30 messages in a week). Built after ADR 0025. **ADR 0025 (one agent per kind of task, its instructions and its memories, completes 0017 and 0019): accepted** by Benoit on 22 November 2026 (proposed on 21 November, work item `stage2/adr-0025-task-agents`), from five answers of Benoit at the stage 2 acceptance: built after the acceptance; the user can choose the agent in the conversation; agents as Markdown files in a folder; starting agents (Home automation, Web, Files, Claude Code, Memory, Project, Jarvis); a memory per agent and one per project, a project being a local folder created in the interface of Jarvis. Completed the same evening: answers to the four points (the hand-over also names the project; project memory as a Markdown file in its folder; hand-off as a tool; by voice the agent is not named), **Jarvis agent** that always gets the voice request and decides to hand over, saying so, **"Call" mode** ("Call" button or "we need to talk about such a project", the agent stays until the user asks to go back to Jarvis, "goodbye" ends it), request to assign a folder without a project to a project. His last three answers ("ok" of 22 November, the recommendations): **the Jarvis agent does the sorting itself**, with no silent sorting call before it (one call to the model for a simple request); **one "Project" agent**, loaded with the folder and the memory of the project asked for, no agent file per project; **during a call, Jarvis listens without "Hey Jarvis"** until "goodbye". Nothing is built: it is the first work item after the acceptance, and the projects view (ADR 0026) depends on it. **ADR 0024 (announcement turn, completes 0005): accepted** by Benoit on 21 November 2026 (work item `stage2/13-announcement-turn-adr`), his answer "Ok" taking the recommendation: an announcement nobody asked for (end of a Claude Code session, wait for a permission) is said in a turn of its own, opened only at rest, cut by an activation, written in the conversation and never sent to the model. **ADR 0023 (confirmation request within a turn, completes 0005): accepted** by Benoit on 21 November 2026 (work item `stage2/confirmation-adr`), with three answers that differ from the recommendations: the orb has a generic **"interaction required"** look (question, approval, confirmation or manual action); **no timeout**, "he never decides in place of the user"; for a typed request, Jarvis only says "task X of project Y needs your interaction", and listens to no answer. **ADR 0022 (web search in a browser internal to Jarvis, partly supersedes 0017): accepted** by Benoit on November 20, 2026 (work item `stage2/in-app-browser-adr`): window hidden during a search with a "See the page" button, cookies kept, sign-in by the user behind a warning. Benoit corrected the reading of ADR 0017: he wanted a browser **internal to the application** (WebView2 in a Jarvis window, separate data folder, no Playwright), not the installed Edge or Chrome, "which can conflict with what the user is doing". **Never drive the user's browser.** What ADR 0017 says about the driven browser, the usual profile and spike E15 on Edge no longer holds; spike E15 is redefined by ADR 0022 (WebView2). **Spike E15: done and passed** on November 20, 2026 (work item `stage2/e15-webview-search`): WPF window never shown with the WebView2 controller kept visible, no foreground grab, DuckDuckGo HTML by default (fallback Startpage then Bing), local addresses blocked with DNS resolution, browser closed after a time without use ([summary](../dev/spikes.md#after-e15-november-20-2026)). **ADRs 0017 (tools, classes, decision point), 0018 (MCP client, Home Assistant) and 0021 (Claude Code sessions): accepted** by Benoit on November 20, 2026 (work item `stage2/2-adrs`). His choices while they were written: web search through a **driven browser** (installed Edge or Chrome, visible window, separate profile, read-only; usual profile possible behind a warning), Brave Search as soon as a key is supplied; no confirmation when launching a Claude Code session; no quiet hours by default, and Jarvis offers 8 hours of silence when the user says they are going to sleep. (The browser search of ADR 0017 is superseded by ADR 0022, see above.) They take up his five answers after the spikes: fan panels in Razor in a window kept hidden, a single decision point for Claude Code writes, `CLAUDE.md` alone except for a "trusted" folder, security home automation from the targeted device plus a list, Home Assistant scripts without confirmation except those marked as "security". **ADRs 0019 (conversation memory) and 0020 (hand-off, targets, spend cap): accepted** by Benoit on November 20, 2026 (work item `stage2/2-adrs-memory-and-hand-off`); written before spikes E11 to E14 because they do not depend on them. His choices: memory on request and automatic; exchange old after 2 hours of silence, adjustable; memory not sent to the cloud, but a session started by Jarvis gets a summary of the previous exchanges and the full initial request; nothing on the orb when Claude answers; cap of US$10; setting "Claude Code is paid by subscription" checked by default. **ADR 0016 (a single native ONNX Runtime, the one from sherpa-onnx; `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 for the wake word, supersedes one line of ADR 0001): accepted** by Benoit on November 20, 2026 (work item `stage1/onnx-runtime-adr`). **ADR 0015 (work tracking source: GitHub Issues or Azure Boards; `#<id>` for GitHub, `AB#<id>` for Azure DevOps in commits and PRs): accepted** by Benoit on November 19, 2026 (work item `stage1/work-tracking-adr`), with confirmation before any creation or change, feature for v2 with Relais. **ADR 0014 (Apache 2.0 license, rule on Kazo Bar, removal of `jarvis.py`): accepted** by Benoit on
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
- **Spikes E11 to E14: done** on November 20, 2026 on Benoit's PC (work item `stage2/1-spikes`), all
  passed: fan in WPF, window kept hidden (E11); `claude -p` permissions through a local MCP tool and hooks to
  `/hook/…` (E12, Claude Code 2.1.328); tools in English 117 of 117 (E13); `ModelContextProtocol` 2.4.0 MCP
  client against Home Assistant (E14). Summary and **decisions accepted by Benoit on November 20, 2026**:
  [dev/spikes.md](../dev/spikes.md#decisions-proposed-after-e11-to-e14). Still for Benoit to judge: smoothness of
  the animation and comfort of the delayed single click.
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
  fallback model. **Provisional choices** ([detail](../dev/llm.md#provisional-choices-of-these-work-items)):
  default port 11434 (Benoit sets 11435 once), `claude-opus-5-5` for a new Anthropic provider, Anthropic
  adapter on `HttpClient` rather than the SDK, answers of 16,000 tokens at most, 20 s connection test.
- **Work item 8** (`stage1/8-audio-devices`, audio devices: contracts `IAudioCapture` and `IAudioPlayback`,
  kernel `MicrophoneFeed` (microphone stream and 30 s in-memory buffer, for the wake word and transcription),
  slice `Audio/Devices` (microphone, mute US1.13, "microphone missing" fault, output, "Audio" page), WASAPI
  through NAudio 3.3 in the Windows host, see [dev/audio.md](../dev/audio.md)) delivered in its PR on
  November 20, 2026, without a Windows check. **Provisional choices** (details in
  [dev/audio.md](../dev/audio.md#provisional-choices-of-these-work-items), to be confirmed by Benoit): mute
  survives a restart; chosen microphone missing = the Windows default, with no fault; muting clears the
  "microphone missing" fault; 30 s buffer; level in decibels from −60 to 0; conversion to 16 kHz done by
  Jarvis.
- **Work item 9** (`stage1/9-wake-word`, wake word: kernel contracts `IWakeWordEngine` and
  `IWakeWordDetector`, ONNX Runtime adapter of the three openWakeWord 0.5.1 models in `models\wake-word`, slice
  `Voice/WakeWord` (listens to the microphone stream, threshold 0.5 to set, one detection per activation,
  silent while listening, "Wake word" page with the live score), see [dev/wake-word.md](../dev/wake-word.md))
  delivered in its PR on November 20, 2026, without a Windows check. Piper "Hey Jarvis.": 0.999; "Hey
  Travis…": 0.488. **Provisional choices** ([details](../dev/wake-word.md#provisional-choices-of-these-work-items),
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
  ([details](../dev/transcription.md#provisional-choices-of-these-work-items)): 8 s wait for speech, utterance
  cut at 25 s, 150 ms of voice to start, 300 ms kept before speech, silent fallback to the energy threshold, no
  fault at start when Whisper is missing (only `TurnFailed` at the end of a turn), empty text published as is
  (work item 13 ends the turn), models in `models\whisper\` and `models\vad\`.
- **Work item 12** (`stage1/12-speech`, speech: kernel contracts `ISpeechEngine` and `IVoiceSelection`,
  messages `SentenceToSpeak` and `AnswerSentencesEnded` for the conversation, slice `Voice/Speech` (sentences
  played in order, clean stop on interruption, voice level to the orb), slice `Voice/TestVoice` ("Voices"
  page), Piper and Kokoro through sherpa-onnx 1.13.8, ElevenLabs and OpenAI, see
  [dev/speech.md](../dev/speech.md)) delivered in its PR on November 20, 2026. Piper tried for real on Linux;
  **listening to the voices waits for Benoit** ([list](../dev/speech.md#windows-checks): perceived quality,
  speed, sound card delay, clean stop). **Provisional choices** ([detail](../dev/speech.md#provisional-choices-of-these-work-items)):
  the voice slice publishes `FirstSentenceReady` at the first sound; one voice per turn; a failing cloud voice
  fails the turn; chosen voice missing = local voice of the language; speed per language; separate OpenAI
  voice key.
- **Work item 13** (`stage1/13-conversation`, the full turn: slice `Conversation/Dialogue` (engine of the turn by
  messages, sentence splitting, system message with the personality and fixed rules, history in half of the
  context, collection `Conversation.Turns`, conversation window with "Erase everything", "Personality" page), the
  voice also stops on `TurnFailed`, rank of the settings pages fixed (wake word 25, transcription 30, voices 35),
  see [dev/conversation.md](../dev/conversation.md)) delivered in its PR on 20 November 2026, without a Windows
  check. The kernel did not change. **Provisional choices**
  ([detail](../dev/conversation.md#provisional-choices-of-these-work-items), for Benoit to confirm): end of sentence
  and abbreviations, Markdown not said, history of 20 turns at most without age limit, turn stored when it ends,
  personalities of both languages on one page, typed request sent only when idle.
- **Work item 14** (`stage1/14-first-run`, first run: kernel contracts `IModelInstaller` and `IGraphicsCardProbe`,
  messages `WhisperModelChosen` and `FirstRunWizardRequested`, adapters `HttpModelInstaller` (size and checksum
  checked, `.tar.bz2` unpacked with SharpZipLib) and `NvidiaSmiGraphicsCardProbe`, slice `Setup/Models` ("Voice
  models" page, advice from the graphics card), slice `Setup/FirstRun` (wizard: language, audio, voice models,
  language model; section `Setup.FirstRun`), Ollama search on 11434 then 11435 in the "Language models" page,
  "Setup wizard…" tray menu entry, see [dev/first-run.md](../dev/first-run.md)) delivered in its PR on 20 November
  2026, without a Windows check. Real download tried in the container (GitHub); **Whisper (Hugging Face) never
  really downloaded**. **Provisional choices** ([detail](../dev/first-run.md#provisional-choices-of-these-work-items),
  to be confirmed by Benoit): 15 GB for the full chain (10 GB before the acceptance of stage 2), Whisper turbo and `qwen3:8b` from 10 to 15 GB, `small` below, `base` on the processor without NVIDIA,
  `qwen3:8b` advised as fallback; SHA-1 for Whisper; dependency SharpZipLib 1.4.2; no "Voices" step; both Piper
  voices checked by default; automatic Ollama search while the providers are the default ones. The Web host now
  simulates an existing profile.
- **Work item 15** (`stage1/15-faults`, faults: kernel `FaultOrigin` and `AssistantFault.Origin`, `FaultResolved` with
  an origin, `SettingsWindowRequested(PageKey)`, `FaultWindowRequested`; slice `Assistant/Faults` (title, explanation,
  corrective action and page of each cause, "Jarvis problem" window, unreadable settings raised at start); language
  model fault raised and cleared outside a turn, Whisper found again and a voice heard clear theirs; "Solve the
  problem…" tray entry during a fault, see [dev/faults.md](../dev/faults.md)) delivered in its PR on 20 November 2026,
  without Windows checks. The transition table did not change. **Provisional choices**
  ([detail](../dev/faults.md#provisional-choices-of-these-work-items), to be confirmed by Benoit): window opened by the
  menu entry, and by itself only on a refusal for want of a microphone; unreachable server raised after two failed
  loads in a row; the "Voices" page clears the voice fault; no button for unreadable settings; origin in the kernel.
- **Stage 2 plan: validated** by Benoit on November 20, 2026 (work item `stage1/stage-2-plan`, [plan](../dev/plans/stage-2.md)): 15 work items `stage2/<n>-<topic>`, first spikes E11 to E14 on his PC then ADRs 0017 to 0021 (numbers reserved by the plan). His answers: `stage1/base` merged into `main` without waiting for the acceptance (he allowed the agent to merge that PR), `stage2/base` starts from `main`; web search through an API with a key (Brave Search) behind a contract; spikes, then ADRs, then tools, the fan towards the end.
- **Work item `stage2/3-tool-loop`** (tool loop, see [dev/tools.md](../dev/tools.md)): kernel contracts
  `ITool`, `IToolRegistry`, `ToolActivation` (section `Tools.Activation`), `IPermissionDecision` and
  `ActionClass`; slice `Permissions/Decision` (a single decision point, denied by default, log in the
  collection `Permissions.Decisions`); slices `Tools/Clock` (`get_datetime`) and `Tools/Calculator`
  (`calculate`); the three adapters send the tools and read the calls, with the tolerant parser for the
  local adapters; the conversation runs the loop (8 calls per turn, 30 s per call, result cut at 2,000
  characters) and shows each call in the turn. "Sensitive" and "Critical" go through the confirmation request of work item 4. The "Tools" page is done in work item 5. **Not verified with a real model** nor
  against the real OpenAI and Anthropic APIs. **Pending Windows tests**
  ([list](../dev/tools.md#checks-on-windows)).
- **Work item `stage2/4-confirmation`** (confirmation request, see
  [dev/confirmation.md](../dev/confirmation.md)), following the accepted ADR 0023: condition
  `AssistantState.Interaction` (`PendingInteraction`, kind question, approval, confirmation or manual
  action) and "interaction required" look (amber) in the kernel, contract `IConfirmation`,
  `ITool.Summarize`; slice `Permissions/Confirmation` (`ConfirmationDesk`, one request at a time, window,
  **no timeout**, closed word list per language); the decision point asks for the confirmation, checks the
  channel again ("Critical": click only) and logs the channel; "Permission log" page; `Voice/Speech` says
  the question (spoken turn) or the announcement (typed turn) without the "Speaking" phase,
  `Voice/Transcription` opens a single listening. Web host: dummy tools `write_note` ("Sensitive") and
  `erase_notes` ("Critical"). **No real "Sensitive" or "Critical" tool exists yet.** Provisional choices to
  be confirmed by Benoit: closing the window is a refusal; the announcement of a conversation says "Your
  request needs your interaction", the form "task X of project Y" waits for the sessions and the tasks; the
  word list follows the language; the window is not placed near the orb yet. **Windows checks pending**
  ([list](../dev/confirmation.md#windows-checks)).
- **Work item `stage2/5-basic-tools`** (basic tools, see [dev/basic-tools.md](../dev/basic-tools.md)),
  following ADRs 0017 and 0022: kernel contracts `ILocalFiles`, `IReadingBrowser`, `IWebSearchApi`,
  `IWebAddressPolicy` and `ITool.Discloses`; slice `Tools/Files` (`list_directory`, `read_file`, "Allowed
  folders" page, no folder allowed at the start, links and junctions resolved before the comparison); slice
  `Tools/Web` (`web_search` and `fetch_url`, **off at the start**, local addresses refused, Brave Search when
  a key is in the vault, "Web search" page); slice `Tools/Catalog` ("Tools" page, one switch per tool);
  browser of Jarvis on WebView2 in the Windows host (`Browser/`), "See the page" button in the conversation
  window. **Nothing has run on Windows**: the browser compiles, its scripts are tried in Chromium.
  Provisional choices to be confirmed by Benoit
  ([details](../dev/basic-tools.md#provisional-choices-of-these-work-items)): typed path with no folder
  picker, no "stricter" setting per tool, browser closed after 5 minutes, a single tab. **Windows checks
  pending** ([list](../dev/basic-tools.md#windows-checks)).
- **Work item `stage2/6-mcp-client`** (MCP client, see [dev/mcp.md](../dev/mcp.md)), following ADR 0018,
  sections 1 to 4: kernel contract `IToolServerConnector` (`Kernel/Tools/Servers`), adapter
  `McpToolServerConnector` on the package `ModelContextProtocol.Core` 2.4.0, slice `Mcp/Servers` (section
  `Mcp.Servers`, `McpServerHub`, "MCP servers" page): adding by address or by command, connection test with a
  named cause, new attempts from 5 s to 5 minutes, tools **off when found**, activation tied to the
  fingerprint, class "Read" or "Sensitive" raised by the user only, tokens in the vault. **No real MCP server
  was reached from Jarvis.** Provisional choices to be confirmed by Benoit
  ([details](../dev/mcp.md#provisional-choices-of-these-work-items)): a single "Token" field, no editing of a server on the page, no environment
  variables, package `Core`. **Windows checks pending** ([list](../dev/mcp.md#windows-checks)).
- **Work item `stage2/7-home-assistant`** (see [dev/home-assistant.md](../dev/home-assistant.md)), following
  ADR 0018, section 5: `ITool.ClassifyAsync` (the decision point only calls this one), kernel contract
  `IToolServerClassifier`, slice `Mcp/HomeAssistant` (`HomeAssistantClassifier`, section `Mcp.HomeAssistant`,
  "Home Assistant" page). A call is classed from the entities it aims at, read through `GetLiveContext`: lock,
  alarm, valve, siren, garage door, door, gate and marked entities give "Critical"; target not found = the
  highest class possible; script "Everyday effect" unless marked; without the list of the entities, the call
  is denied. **No call left Jarvis for the real Home Assistant.** Provisional choices to be confirmed by
  Benoit ([details](../dev/home-assistant.md#provisional-choices-of-these-work-items)): section
  `Mcp.HomeAssistant` and a page of its own, entity marked by its name, one list for all servers, floor
  treated as a target not found, list kept 5 s, thermostat not classed as security. **Windows checks pending**
  ([list](../dev/home-assistant.md#windows-checks)).
- **Work item `stage2/8-memory`** (see [dev/conversation-memory.md](../dev/conversation-memory.md)),
  following ADR 0019: kernel contract `IConversationMemory`, messages `TurnRecorded` and
  `ExchangesLeftHistory`, properties `ITool.ReadsOutsideContent` and `RefusedAfterOutsideContent`; slice
  `Conversation/Memory` (`MemoryBook`, `MemoryRecaller`, `MemoryWorker`, tools `remember_fact`, `forget_fact`,
  `search_memory`, section `Conversation.Memory`, "Memory" page). Facts of 200 characters and one summary of
  1,200, block of one eighth of the context after the fixed rules, extraction by the local model from the
  words of the user only, only when idle, silence of 2 hours that can be set. **No real model has extracted a
  fact or written a summary.** Provisional choices to be confirmed by Benoit
  ([details](../dev/conversation-memory.md#provisional-choices-of-these-work-items)): collections
  `Conversation.MemoryFacts` and `Conversation.MemorySummary`, summary limited to half of the budget, no age
  limit when the memory is off, `forget_fact` erases nothing when ambiguous, 500 facts at most. **Windows
  checks pending** ([list](../dev/conversation-memory.md#windows-checks)).
- **Work item `stage2/9-hand-off`** (see [dev/hand-off.md](../dev/hand-off.md)), per ADR 0020, sections 1 to
  5: kernel contracts `IHandOff` (`Kernel/Conversation`) and `ILlmProviderList` (`Kernel/Llm`); slice
  `Llm/HandOff` (`HandOffTargets`, `HandOffTool` `hand_off_to_claude`, `HandOffPhrases`, section `Llm.HandOff`,
  "Hand-off" page, order 11); in `Conversation/Dialogue`, the `RunAsync` loop serves the local model and the
  target, `ConversationTurn.HandOff` keeps who answered and why. No target at first; the tool is in the
  registry only while a target is set; the memory leaves only when the box is ticked; a target that fails is
  a cause told, never a fault. The target for tasks (Claude Code) waits for work item 12. **No call left for
  a real cloud API** (the spending cap came with work item 10). Provisional choices to be confirmed
  by Benoit ([details](../dev/hand-off.md#provisional-choices-of-these-work-items)): reason shown but not said,
  name said from the kind of the provider, formulas on "Claude" only, eight tool calls of its own for the
  target. **Windows checks pending** ([list](../dev/hand-off.md#windows-checks)).
- **Work item `stage2/10-spend-cap`** (see [dev/spend-cap.md](../dev/spend-cap.md)), per ADR 0020, sections
  6 and 7: kernel contract `ISpendCap` (`Kernel/Llm`) and message `ProviderAnswered`; causes
  `HandOffRefusal.NoPrice` and `CapReached`, `IHandOff.Notice`; slice `Llm/Spend` (`SpendLedger`,
  `KnownPrices`, sections `Llm.Spend` and `Llm.SpendCount`, collection `Llm.SpendEntries`, "Spending" page,
  order 12). Cap of 10 USD a month at first; a cloud provider without a price is never called; the cap is
  asked before each hand-off and before each later request of the target; at the cap the tool leaves the
  registry; Jarvis warns once at 80 % and once at the cap. **Differences from the ADR**: the collection has
  one dot in its name; the setting "Claude Code is paid by subscription" and `--max-budget-usd` wait for work
  item 12. **No call left for a real cloud API: the count was never compared with a bill.** Provisional
  choices to be confirmed by Benoit ([details](../dev/spend-cap.md#provisional-choices-of-these-work-items)):
  "Erase everything" does not put the count back to zero, reset button on the page, warning said after the
  answer of the target. **Windows checks pending** ([list](../dev/spend-cap.md#windows-checks)).
- **Work item `stage2/11-tasks-and-sessions`, first part** (see
  [dev/tasks-and-sessions.md](../dev/tasks-and-sessions.md)), per ADR 0013 (section 6) and 0021 (sections 6, 7
  and 10): shape of a task in the kernel (`WorkTask`, nine statuses, `TaskKeys`, `ITaskBoard`); slice
  `Tasks/Board` (collection `Tasks.Board`); contract `IClaudeSessionRegistry` and adapter
  `Adapters/ClaudeCode/ClaudeSessionRegistry`, which opens `sessions/*.json` only (a test proves that no
  `*.key` nor `ide/*.lock` is opened); slice `ClaudeCode/Sessions` (section `ClaudeCode.Sessions`, "Claude
  sessions" page, order 52). Provisional choices to be
  confirmed by Benoit ([details](../dev/tasks-and-sessions.md#provisional-choices-of-these-work-items)): reading
  of the registry on at first, every 5 seconds. **Windows checks pending**
  ([list](../dev/tasks-and-sessions.md#windows-checks)).
- **Work item `stage2/11-hook-server`, second part of work item 11** (see
  [dev/hook-server.md](../dev/hook-server.md)), per ADR 0021 (sections 3, 5 and 6): kernel contracts
  `ILocalServer`, `ILocalRoute`, `LocalServerOptions` (section `Local.Server`, port 47821), `HookPaths`,
  `IClaudeUserSettings`; adapter `Adapters/LocalServer/KestrelLocalServer` (Kestrel on `127.0.0.1` only,
  **no route without a token**: token of the run in memory, token of the user's hooks in the vault under
  `ClaudeCode.UserHookToken`, valid for `/hook/…` only); hook routes in `Tasks/Board` (`TaskHooks`), which
  create the tasks of source `hook` and move the board; slice `ClaudeCode/Hooks` ("Claude Code hooks" page,
  order 53): preview, installation and removal in `%USERPROFILE%\.claude\settings.json` **on the user's
  click only**, with a dated copy. `Kazo.Jarvis.Adapters` references `Microsoft.AspNetCore.App`.
  **Deviations from ADR 0021**: a page of its own instead of a button in "Claude sessions"; hooks installed
  as **background `curl.exe` commands** (`async`) and not as `http` hooks, because the check section 6 asks
  for could not be done without Windows. `/mcp` and `approve`: work item 12. Provisional choices to be
  confirmed by Benoit ([details](../dev/hook-server.md#provisional-choices-of-these-work-items)). **Windows
  checks pending, among them "Jarvis closed, a session does not slow down"**
  ([list](../dev/hook-server.md#windows-checks)).
- **Work item `stage2/12-claude-code-launch`, first part of work item 12** (see
  [dev/claude-code-launch.md](../dev/claude-code-launch.md)): `ActionRequest.Session` (`SessionAction`) so that
  a request of a Claude Code session goes through the decision point with a class given by code; `approve`
  tool on `/mcp` of the local server (`ModelContextProtocol.AspNetCore`, token of the run only) and contract
  `IClaudeApprovals`; contract `IClaudeCode` and adapter `ClaudeCodeProcess` (command line of ADR 0021, files
  under `.kazo\jarvis\claude\` without a secret, token and task through the environment). Deviations: no `SessionStart` hook for the sessions of Jarvis; no confirmation timeout
  (ADR 0023 prevails over ADR 0021). **Not verified: no real Claude Code started by this code; variables in
  the headers of the MCP file.**
- **Work item `stage2/12-claude-code-tasks`, second part of work item 12** (see
  [dev/claude-code-tasks.md](../dev/claude-code-tasks.md)): slice `ClaudeCode/Launch` (`ClaudeTaskRunner`,
  "Claude Code tasks" page, order 54). "Ask Claude Code to…" starts a session on an allowed folder (chosen by
  its name in the request, otherwise Jarvis asks which); task created before the launch; requests classified
  by `SessionRules` then given to the decision point; session stopped if a write runs without having asked;
  ends `done`, `blocked`, `abandoned`; `ClaudeTaskEnded` published. Kernel contracts: `IClaudeTasks`,
  `IAllowedFolders`, `ISpendCap.SessionBudget`. Setting "Claude Code is paid by subscription" (checked by
  default) in `Llm/Spend`. **Deviations: the summary at the end is not said aloud yet (work item 13);
  `kind: task` is not offered to the local model, only the formula starts a session.** Provisional choices to
  be confirmed by Benoit ([details](../dev/claude-code-tasks.md#provisional-choices-of-these-work-items)).
  **No real session started; Windows tests pending**
  ([list](../dev/claude-code-tasks.md#windows-checks)).
- **Work item `stage2/13-session-announcements`** (see
  [dev/session-announcements.md](../dev/session-announcements.md)): the announcement turn of ADR 0024 in the
  kernel (`AnnouncementSubmitted`, `TurnInput.Announcement`, `AnnouncementToSay`) and in the conversation
  (`AnnouncementDesk`: written at once, said at rest, one at a time, never sent to the model); slice
  `ClaudeCode/Announcements` (`SessionAnnouncer` watches the board of the tasks, hence the same announcements
  for a session started by Jarvis or seen through its hooks; choice per folder; quiet hours, none by default;
  quiet period kept in memory only; tools `start_quiet_period` and `get_claude_sessions`; "Announcements"
  page, order 55). The end summary left aside in work item 12 is said here. **Deviations: the amber badge of
  the orb during the silence came with work item 14; "I am going to sleep" rests on the description of the
  tool, hence on the local model.** Provisional choices to be confirmed by Benoit
  ([details](../dev/session-announcements.md#provisional-choices-of-these-work-items)). **No announcement said
  by a real voice; Windows tests pending** ([list](../dev/session-announcements.md#windows-checks)).
- **Work item `stage2/14-task-fan`** (see [dev/task-fan.md](../dev/task-fan.md)): the fan of ADR 0010. Kernel:
  `TaskFanToggleRequested`, `IPointerTimings` (double-click delay given by the host), `ITaskFanSurface` and
  `FanAnchor`, `IOpenConfirmations` (the open confirmation request, implemented by the confirmation desk),
  `TaskId` on `ConfirmationRequest` and `SessionAction`. Orb: `OrbActivation` delays the single click by the
  double-click delay and publishes the double click; `OrbView` feeds the amber badge. Slice `Tasks/Fan`
  (`TaskFan`, `FanContent`, `FanLayout`, `TaskFanView`: an SVG whose origin is the orb, five panels at most,
  sub-tasks inside the panel, the request of a session in the panel of its task with Allow, Refuse and Details,
  stepped animation at 30 images per second at most). `Tasks/Board` now publishes `DelegatedTaskCountChanged`
  (`TaskCount`). Windows host: `FanWindow` created once after the orb and kept hidden, `SystemPointerTimings`.
  Web host: the orb is in a corner of the page. Provisional choices to be confirmed by Benoit
  ([details](../dev/task-fan.md#provisional-choices-of-these-work-items)). **Nothing ran on Windows; the measures
  of ADR 0010 are not redone on the real host; Windows tests pending**
  ([list](../dev/task-fan.md#windows-checks)).
- **Work item `stage2/17-agents`** (see [dev/agents.md](../dev/agents.md)): first part of ADR 0025, sections 1 to 4, 8 and 9. Kernel: `IAgentCatalog`, `IAgentFiles`, `AgentDefinition`, `ITool.Agent`, fault `AgentUnreadable`. Slice `Agents/Catalog`: one file `agents\<identity>.md` per agent, six starting agents written at the first launch and never written over a file of the user (fingerprints in `Agents.Files`), files read again when they change, tools `hand_to_agent` and `return_to_jarvis`, "Agents" page. Conversation: the Jarvis agent receives the request with its own tools only and one line per agent, answers itself (one call) or hands over; the agent named continues the turn; an agent gives the hand back; choice of the agent in the window for a typed request; name of the agent kept with the turn. **Nothing ran on Windows** ([list](../dev/agents.md#checks-on-windows)), and **the sorting by a real local model was never tried**: the tests use a simulated model. **Eight provisional choices** to be confirmed by Benoit ([detail](../dev/agents.md#provisional-choices-of-these-work-items)). What follows is in the notes of work items 18 and 19. The projects view (ADR 0026) comes after work item 19. The two application choices of ADR 0027 (left click while listening, no double-click delay while nothing answers) are confirmed by Benoit since 22 November 2026.
- **Work item `stage2/18-agent-memory`** (see [dev/agents.md](../dev/agents.md#the-memory-of-each-agent-work-item-18-adr-0025-section-5)): ADR 0025, sections 3 (end) and 5. Kernel: `ITool.ForEveryAgent`, `ITool.InvokeAsync(arguments, agent, …)`, `IConversationMemory.RecallForAgentAsync`. Memory: `MemoryFact.Agent`, tool `remember_agent_fact` given to every agent but the Jarvis agent, second block "[Agent memory]" sent only when this agent answers (one sixteenth of the context), list "who the fact is for" on the "Memory" page. MCP servers: `McpServerOptions.Agent` and an "Agent" list on the page of the server, read at each turn. **Nothing ran on Windows**, and the local model never called `remember_agent_fact`. Four more provisional choices (9 to 12 in [dev/agents.md](../dev/agents.md#provisional-choices-of-these-work-items)).
- **Work item `stage2/19-projects`** (see [dev/projects.md](../dev/projects.md)): ADR 0025, sections 6, 10, 11 and 13 (point 2). Kernel: `IProjectCatalog`, `ProjectDefinition`, `AgentIds.Project`, `AgentHand.Project`, `IAllowedFolders.AllowAsync`. Slice `Projects/Catalog`: projects kept in the settings (`Projects.List`), memory read from `JARVIS.md` at the root of the folder, "Projects" page (create, remove, allow the folder after a confirmation). Agents: seventh starting agent "Project", `project` argument of `hand_to_agent`. Conversation: project of the turn (chosen in the window, named in the sentence, or named when handing over), block "[Project]" given to the agent that answers but the Jarvis agent, name of the project kept with the turn. **Nothing ran on Windows.** Seven provisional choices ([detail](../dev/projects.md#provisional-choices-of-these-work-items)), among them the name `JARVIS.md` and the fact that Jarvis reads this memory without writing it. **Still to build for ADR 0025:** the request to assign to a project a folder that has none, the Claude Code session started in the folder of the project, writing the memory of a project, then work item 20, the "Call" mode.
- **Work item `stage2/19-project-sessions`** (see [dev/projects.md](../dev/projects.md)): ADR 0025, section 6. `ProjectCatalog.Unassigned` reads the task board (`ITaskBoard`): a folder where a session that has not ended works and that belongs to no project gives a request on the "Projects" page (assign to an existing project, `ProjectEntry.MoreFolders`, or create a project). A work handed to Claude Code during a turn about a project starts in the folder of the project (`ClaudeTaskRequest.Folder`); a folder that is not allowed is refused by its name (`FolderNotAllowed`). **Nothing ran on Windows.** Provisional choices 8 to 12 of [dev/projects.md](../dev/projects.md#provisional-choices-of-these-work-items), among them: the request shows only on the "Projects" page. **Still to build for ADR 0025:** writing the memory of a project, then work item 20, the "Appel" mode.
- **Work item `stage2/20-call-mode`** (see [dev/call-mode.md](../dev/call-mode.md)): ADR 0025, sections 12 and 13 (point 3). Kernel: `ActivationSource.Call` (starts listening at rest only, never cancels nor interrupts). Slice `Conversation/Dialogue`: `CallMode` (started by the "Call" button or a sentence like "On doit discuter de…", agent and project kept from one turn to the next, ended on "goodbye" said by Jarvis without the model) and `CallListener` (listening started again after a spoken turn that got its answer, three silences in a row at most). **Nothing ran on Windows, and listening without "Hey Jarvis" was never tried with a real microphone.** Seven provisional choices ([detail](../dev/call-mode.md#provisional-choices-of-this-work-item)). **Still to build for ADR 0025:** Jarvis writing the memory of a project; then the projects view (ADR 0026).
- **Answers of Benoit of 2026-11-23** ("ok" to the recommendations, thread "Construire les agents par type de tâche"): the sentence said when handing over stays fixed; the memory tools stay in the Memory agent; the starting agent files follow the language of the assistant; long runs go to the GitHub checks of a pushed branch; the memory file of a project is named `JARVIS.md`; Jarvis writes in it on request, with a confirmation; the request for a folder with no project also shows in the task fan.
- **Work item `stage2/21-project-memory-writing`** (see [dev/projects.md](../dev/projects.md)): tool `remember_project_fact` (slice `Projects/Catalog`, "Project" agent, class "Sensitive" so a confirmation every time) that adds a line to `JARVIS.md`; `ILocalFiles.AppendTextAsync` in the kernel, the only write Jarvis does in a folder of the user; `SecretShapes` moved to the kernel (`Kernel/Secrets`), because the conversation memory and the project memory both refuse a secret. **Nothing ran on Windows.**
- **Work item `stage2/22-unassigned-folder-in-fan`** (see [dev/task-fan.md](../dev/task-fan.md)): the panel of a session whose folder belongs to no project shows the button "Folder with no project: choose", which opens the "Projects" page; `IProjectCatalog.IsWithoutProject` and `IProjectCatalog.SettingsPageKey` in the kernel. The panel does not turn amber. **Nothing ran on Windows.**
- **Work item `stage2/23-session-reads-project-memory`** (see [dev/projects.md](../dev/projects.md)): the first message of a Claude Code session started by Jarvis in a folder of a project begins with one line that points it to `JARVIS.md` (`IProjectCatalog.MemoryFileOf` in the kernel, `ClaudeTaskRunner.FirstMessage`). **Never tried with the real Claude Code.** Left of ADR 0025: the calls in the Android app (stage 5).
- **`stage2/base` merged into `main` on 23 November 2026** (PR 77), at Benoit's request: everything up to work item 23. The `stage2` series goes on on `stage2/base`.
- **Work item `stage2/24-projects-view`** (see [dev/projects-view.md](../dev/projects-view.md)): first work item of ADR 0026. Slice `Projects/View`: `ProjectsOverview` (projects, subjects, numbered tasks, computed thread, from `ITaskBoard` and `IProjectCatalog`), `ProjectsViewOpener` which answers the double click on the orb, component `ProjectsView`. Kernel: `IProjectCatalog.OwnerOf`, `DialogRequest.Maximized`. Read only; eight provisional choices. **Nothing ran on Windows.** Left of ADR 0026: writing in the thread of a subject ("T3>"), messages of the threads, "+ New subject", offer of a project from General.
- **Work item `stage2/26-subject-thread-writing`** (see [dev/projects-view.md](../dev/projects-view.md)): ADR 0026, section 4, written only. Kernel: `ThreadAddress`, `ThreadMessage`, `ISubjectThreads`; `TextSubmitted.Thread` and `TurnStarted.Thread`. A message written in the thread of a subject is an ordinary turn of the conversation, tagged (`ConversationTurn.Thread`); in a project of the catalog the "Projet" agent answers with no hand-over; `T3>` (`SubjectAnswer`) names the task. Benoit accepted the three recommendations on 23 November 2026 ("Ok"): ordinary tagged turn; `T3>` to a session started by Jarvis resumes it with the message; to a session started by hand, Jarvis says it is not its own and keeps the message. **Work item `stage2/27-answer-task-session`**: `IClaudeTasks.ContinueAsync`, `TaskChange.Reopen` (deviation from ADR 0013: a task that is over goes back to work, asked by `ClaudeTaskRunner` only), `claude --resume` never run for real. Work item `stage2/25-windows-trial`: `WindowsCompositionTests` builds the services of the Windows host together with fake native services. Nothing ran on Windows.
- **Benoit's request of 23 November 2026:** the README says the project is "under design", and screenshots are kept in `design/screenshots/` (script `capture.cjs`, taken on the Web host) for the README. **Take them again when a work item changes what they show.**
- **Next step:** series `stage2`, branch `stage2/base` started from `main`, work item 15 (`stage2/15-acceptance`, acceptance on Benoit's PC, with him) of the [plan](../dev/plans/stage-2.md); work item `stage2/14-task-fan` is done (work items `stage2/3-tool-loop`, `stage2/4-confirmation`, `stage2/5-basic-tools`, `stage2/6-mcp-client`, `stage2/7-home-assistant`, `stage2/8-memory`, `stage2/9-hand-off`, `stage2/10-spend-cap`, `stage2/11` (its two parts, `stage2/11-tasks-and-sessions` and `stage2/11-hook-server`) and `stage2/12` (its two parts, `stage2/12-claude-code-launch` and `stage2/12-claude-code-tasks`) and `stage2/13-session-announcements` are done; ADRs 0017 to 0022 are all accepted). Work item `stage2/1-spikes` (spikes E11 to E14) is done. `stage1/base` was merged into `main` on November 20, 2026 **without acceptance** (Benoit's decision): work item `stage1/17-acceptance` remains (acceptance on Benoit's PC, with him), and its fixes will go through a small separate series. The pending Windows tests (work items 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 and 15, [list](../dev/hosts.md#windows-checks)) are done at the next session with Benoit. Plan: the [stage 1 series plan](../dev/plans/stage-1.md),
- **Acceptance of stage 2 (work item 15), state on 22 November 2026:** started on 21 November on Benoit's PC, not finished. Checked on his PC: the unit tests, the orb and the fan (click delay, double click, Escape, clicks that pass through), the first-run wizard, Ollama found on 11435, the Home Assistant MCP server connected, the wake word, one voice command from end to end. Found and fixed: the background services that never started on Windows (dependency circle through factories, [pitfalls](../dev/pitfalls.md)), confirmed fixed on his PC; the settings window without scrolling and the entity name said by the model ("lumière articulée" for "Articulé", [detail](../dev/home-assistant.md)), fixed but **not yet seen again on his PC**; the model advice raised to 15 GB. On his 12 GB card, Benoit uses `qwen3:8b` with Whisper turbo. Noted, not fixed: Whisper heard "est un articulé" for "allume Articulé". **Left**: the "Articulé" light by voice, the question handed to Claude, the Claude Code task with an allowed write and the end announcement, in French then in English, the list of outgoing connections, the Windows checks of work items 4 to 14, then `features.md`. ADR 0025 (one agent per kind of task, memories per agent and per project, "Appel" mode) is **proposed** in its PR, with three points Benoit put off on 22 November; it is built after the acceptance.
- **ADR 0027 (gestures on the orb, replaces section 3 of 0010): accepted** by Benoit on 22 November 2026 (work item `stage2/16-orb-gestures-adr`), from his request: left click, Jarvis listens; right click, the task fan; double click, the projects view (`ProjectsViewRequested` message, which nothing answers yet). Two application choices, **confirmed by Benoit on 22 November 2026** ("ok"): a click while listening still cancels it; as long as no slice answers the double click, there is neither double click nor delay. Numbers 0025 (agents per kind of task) and 0026 (projects view) are taken by open PRs: **the next free ADR number is 0028.** Code done in work item `stage2/16-orb-gestures` (`OrbActivation.RightClickAsync`, `ProjectsViewRequested`, right click caught by the orb window), **not checked on Windows**. What the notes of earlier work items say above and below about the double click opening the fan no longer holds.
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
- **Known test failures:** none. The fan test that failed on CI only (`TaskFanTests`, the write allowed from its panel) is explained and fixed (work item `stage2/17-confirmation-survives-other-views`): the confirmation window refused its request when a component ended, and the server ended the pages of earlier tests at that moment; the refusal now comes from `IDialogService.Closed` ([pitfalls](../dev/pitfalls.md)).
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
