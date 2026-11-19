# Stage 1 series plan: orb, voice, local model and settings

2026-11-19 · **Validated by Benoit on 2026-11-19** (work item `spikes/15-stage-1-plan`, answers written by
`spikes/17-stage-1-answers`). Builds on ADR 0011 (colors and drawing of the orb), accepted the same day.

## Goal

At the end of this series, you say "hey jarvis" in front of the PC, ask a question, and the local model
answers out loud in French or English. Nothing leaves the machine. These are stories US1.1 to US1.18 of the
[design document](design.md#stage-1-orb-voice-local-model-and-settings).

Everything built here is already decided by the accepted ADRs 0001 to 0009 and by ADR 0011 (colors and
drawing of the orb). This plan takes no architecture decision: it says **in which order** to build, **in how many pieces**,
and **how to know a piece is done**. ADR 0010 (task fan) is not built at stage 1.

## Branches and merging

- Series `stage1`, base branch `stage1/base`, started from `main` once `spikes/base` is merged into
  `main` (answer 1 below).
- One work item per `stage1/<n>-<topic>` branch, one PR per work item to `stage1/base`.
- An agent merges its own PR into `stage1/base` once the build and tests are green (AGENTS.md, IV.5). Only
  Benoit merges `stage1/base` into `main`, at the end of the series.
- Work items follow the order of the table. Those the table marks as independent can move in parallel, each
  in its own branch.

## What every work item must deliver

A work item is only done when all of this is true:

1. `dotnet build` and `dotnet test` are green on Benoit's PC, including the architecture tests of ADR 0002
   and the translation key test of ADR 0008. No disabled test.
2. Every slice has its unit tests, with simulated contracts (no real microphone, model or network).
3. Every visible flow has its Playwright test against the Web host.
4. Every native contract added has its real implementation and its simulated implementation in the Web host
   (ADR 0003). A contract is only added by the work item that needs it.
5. Every visible text exists in English (`.resx`) and French (`.fr.resx`).
6. The work item's **Windows checks** are done on Benoit's PC and their result is written in the PR (what
   was checked, by whom, what failed).
7. The docs it touches are up to date in both languages, so is the project memory, and any pitfall met goes
   into `pitfalls.md`.
8. No code from `spikes/` is referenced. We take the behavior the spikes proved and rewrite the code cleanly
   in its slice.

## Overview

| # | Work item (branch `stage1/…`) | Stories | ADR | Depends on | Size |
| --- | --- | --- | --- | --- | --- |
| 1 | `1-solution`: solution skeleton | — | 0001, 0002 | — | M |
| 2 | `2-kernel`: message bus and state machine | — | 0002, 0005 | 1 | M |
| 3 | `3-settings-storage-vault`: configuration, SQLite database, vault | US1.16 | 0004 | 2 | L |
| 4 | `4-hosts`: Windows host, Web host, notification area, hotkey | US1.1, US1.2, US1.6 | 0003 | 3 | L |
| 5 | `5-shared-ui-language`: common style, translations, language setting | US1.4 (language) | 0008, 0011 | 4 | M |
| 6 | `6-orb`: orb slice, light version, demo page | US1.3, US1.6 (click) | 0005, 0011 | 5 | L |
| 7 | `7-orb-high-quality`: high-quality version and measurements | US1.3 | 0011 | 6 | M |
| 8 | `8-audio-devices`: microphone, audio output, microphone mute | US1.13 | 0003, 0006 | 5 | M |
| 9 | `9-wake-word`: wake word and score indicator | US1.5 | 0006 | 8 | M |
| 10 | `10-transcription`: end of utterance and Whisper | US1.7 | 0006, 0008 | 8 | M |
| 11 | `11-llm-providers`: providers and default local model | US1.8, US1.9 (model) | 0007 | 5 | L |
| 12 | `12-speech`: synthesis and voice test page | US1.10, US1.11 | 0006, 0009 | 8 | L |
| 13 | `13-conversation`: the full turn, interruption, conversation window, personality | US1.9, US1.12, US1.14, US1.15 | 0005, 0007, 0008 | 6, 9, 10, 11, 12 | L |
| 14 | `14-first-run`: first-run wizard and model download | US1.4 | 0004, 0006, 0008 | 13 | M |
| 15 | `15-faults`: named faults and corrective actions | US1.17 | 0005 | 14 | S |
| 16 | `16-updates`: updates and publishing | US1.18 | 0001 | 4 | M |
| 17 | `17-acceptance`: stage acceptance on Benoit's PC | all | — | all | S |

Sizes: S, a few hours; M, a day; L, more than a day. These are orders of magnitude.

Independent once their dependencies are done: 6 and 7 (the orb) on one side, 8 to 12 (voice and model) on
the other, and 16 (updates) at any time after 4.

```
1 ─ 2 ─ 3 ─ 4 ─ 5 ─┬─ 6 ─ 7 ─────────────────┐
                   ├─ 8 ─┬─ 9 ───────────────┤
                   │     ├─ 10 ──────────────┤
                   │     └─ 12 ──────────────┼─ 13 ─ 14 ─ 15 ─ 17
                   └─ 11 ────────────────────┘
               4 ─ 16 ───────────────────────────────────────┘
```

## Work items in detail

### 1. Solution skeleton (`stage1/1-solution`)

- `Kazo.Jarvis.slnx`, `global.json` (SDK 10.0.402, `rollForward: latestFeature`), `Directory.Packages.props`
  with the versions of ADR 0001; `Directory.Build.props` already exists.
- The stage 1 projects of ADR 0001: `Kernel`, `Features`, `Adapters`, `Windows`, `Web`, `Tests.Unit`,
  `Tests.E2E`. `Updater` waits for work item 16, `Mobile` for stage 4.
- The Windows host opens an empty window, the Web host serves an empty page, Playwright opens that page.
- The **architecture tests** of ADR 0002, section 7, all written here: they are green on an almost empty
  repository and will stay so.
- GitHub Actions continuous integration on Linux (answer 2): build of every project, non-Windows unit tests,
  Playwright against the Web host.
- **Windows check:** both hosts start, the tests pass.

### 2. Kernel (`stage1/2-kernel`)

- The in-memory bus of ADR 0002, section 5: events and requests, typed, asynchronous; a failing subscriber
  does not stop the others and the error is logged.
- The state machine of ADR 0005: phase, conditions, turn identifier, transition table, single writer,
  `AssistantStateChanged` message, and the look function as ADR 0011 completes it ("long task" look).
- The sound level contract (0 to 1, at most 30 times per second), outside the bus.
- **Tests:** every row of the transition table, the ignored pairs, a message from a cancelled turn, the look
  function for every combination of phase and conditions.

### 3. Configuration, storage and vault (`stage1/3-settings-storage-vault`)

- Root `%USERPROFILE%\.kazo\jarvis` and its subfolders (ADR 0004, section 1); a temporary folder for tests.
- Configuration: kernel contract, `config.json` file in `Adapters`: `schemaVersion`, one section per slice,
  unknown sections kept, atomic write, copy to `backups\` before a migration, unreadable or newer file never
  overwritten (the application starts on defaults and says so).
- Document store: kernel contract, SQLite in `Adapters`, in-memory store for the Web host, versioned schema,
  "erase everything" button (the button itself comes with the conversation window, in work item 13).
- Vault (US1.16): kernel contract, Windows Credential Manager (default) and a DPAPI-encrypted
  `secrets.json`, switching vaults moves the secrets, in-memory vault for the Web host. No value in a log or
  an error message.
- **Tests:** on a temporary folder; the Windows vault tests are marked Windows and run on Benoit's PC.

### 4. Hosts (`stage1/4-hosts`)

- Windows host (ADR 0003, section 2): single instance; transparent orb window, borderless, topmost, out of
  the taskbar, not taking focus, movable, position remembered (US1.1), with a placeholder orb (a disc);
  dialog contract (one window per key, same service provider); clear message if WebView2 is missing.
- Notification area (US1.2): left click shows or hides the orb; menu Settings, Conversation, Mute microphone
  (inactive until work item 8), Quit.
- Global hotkey (US1.6): contract, hotkey setting, activation event to the state machine.
- Start with Windows: on by default, a setting to turn it off (assumption 3).
- Web host: simulated services, dialogs over the page, a button that simulates the hotkey.
- **Windows checks:** clicks around the orb reach the application below; the hotkey works while another
  application has the focus; a second launch shows the already open orb; the position comes back after a
  restart; cost of the still window (method of spike E6).

### 5. Common style and language (`stage1/5-shared-ui-language`)

- `Features/Shared/`: colors and embedded fonts of ADR 0011 (section 2); settings page frame; secret
  field (used by the providers, the cloud voices and later the connectors).
- Translations (ADR 0008): `.resx` and `IStringLocalizer`, English neutral, key test.
- `Settings/Language` slice: `fr` or `en` setting, proposed from the Windows language, applied without a
  restart.
- **Windows check:** open windows reload their texts on a language change in `BlazorWebView`. If this is not
  reliable, a new ADR says the interface changes at the next start (ADR 0008).

### 6. Orb, light version (`stage1/6-orb`)

- `Orb` slice: one component for the three hosts, with the inputs and outputs of ADR 0005 and the quality of
  ADR 0011.
- Light version first: the simplest to port, and the fallback. The seven looks of ADR 0011, fades, still
  image in the looks that require it, reduced motion, error cause on hover, progress ring and amber
  decision badge (with simulated data).
- A click on the orb publishes the activation (US1.6). No double click and no delay: the fan does not exist
  yet (ADR 0010, section 3).
- Demo page in the Web host (US1.3): the seven states, simulated tasks, the badge, the optional animated
  idle.
- **Playwright tests:** each look (the component exposes its current look in an attribute), reduced motion,
  the click.
- **Windows check:** idle, microphone muted and error under 2 % of one core with the light version
  (ADR 0005, mandatory); cost of the animated phases recorded.

### 7. Orb, high-quality version (`stage1/7-orb-high-quality`)

- Port of the high-quality prototype (mode 5): plasma sphere and binary clouds in Canvas 2D, gyroscope rings
  in SVG and CSS 3D, paths generated once.
- "Orb quality" setting (high by default, light), changed without a restart.
- **Windows check:** the measurements of ADR 0011 (section 8), for both versions, written in the project
  memory. If the high quality stutters or clearly costs more, the default is discussed again with Benoit.

### 8. Audio devices (`stage1/8-audio-devices`)

- Capture and playback contracts (ADR 0003); implemented in the Windows host with NAudio 3.3 (`Span<float>`,
  see the pitfalls); capture at 16 kHz mono, ring buffer in memory, never written to disk.
- Choice of microphone and output in the settings.
- Microphone mute (US1.13): the stream is closed, the "microphone muted" condition is set, the notification
  area menu item becomes active.
- Microphone sound level to the orb.
- **Windows checks:** real microphone; with the microphone muted, Windows no longer shows an application
  listening; microphone unplugged while Jarvis runs.

### 9. Wake word (`stage1/9-wake-word`)

- `Voice/WakeWord` slice (US1.5): the three openWakeWord models through ONNX Runtime, continuous detection,
  adjustable threshold (0.5 to start), live score indicator in the settings, one detection per activation,
  silent while listening.
- **Windows checks:** with Benoit's voice, the chosen threshold is written in the project memory (ADR 0006,
  section 2); cost of the wake word measured separately. If his pronunciation does not pass even with a lower
  threshold, we stop and ask (training a model needs a new ADR).

### 10. Transcription (`stage1/10-transcription`)

- `Voice/Transcription` slice (US1.7): end of utterance through Silero (sherpa-onnx), 500 ms of silence, a
  no-speech timeout that goes back to idle; Whisper.net with CUDA; adjustable model and device
  (`large-v3-turbo` on the graphics card by default, `small`, `base` on the processor); language set by the
  setting; model loaded once.
- The transcript is published for the conversation.
- **Windows checks:** end of utterance with a real microphone and some noise. If Silero is not reliable,
  fall back to an energy threshold (ADR 0006, section 3), and the PR says so.

### 11. LLM providers (`stage1/11-llm-providers`)

- `Llm/Providers` slice (US1.8): list of providers (name, type, address, model, options, key reference);
  `ollama`, `openai-compatible`/`openai` and `anthropic` adapters on `IChatClient`; "Test connection" button
  that names the cause.
- Default local model (ADR 0007, section 5): `qwen3:14b`, thinking off, context 4096, temperature 0.3,
  preloaded at start-up and kept in memory; "the model is loading" message if Ollama released it.
- Cloud providers are configured and tested; they do not answer at stage 1.
- The tolerant tool-call parser waits for stage 2, with the first tool.
- **Windows checks:** Ollama on port 11435 and `llama-server` on 8080; measurement of a 7 to 8 billion
  parameter fallback model, to be offered by the first-run wizard (ADR 0007).

### 12. Synthesis and voice test page (`stage1/12-speech`)

- `Voice/Speech` slice (US1.10): Piper by default (`fr_FR-tom-medium`, `en_GB-alan-medium`), Kokoro as an
  option, sherpa-onnx **1.13.8**; queue of sentences played in order; immediate stop and emptied queue on
  interruption; voice sound level to the orb.
- ElevenLabs and OpenAI cloud voices behind the same contract, listed only if a key is in the vault, with
  the warning that the text leaves the machine (ADR 0009, section 9).
- `Voice/TestVoice` slice (US1.11): voices by engine and language, editable test sentence, speed, default
  voice for each language.
- **Windows checks:** perceived quality of the voices on the real speaker; sound card delay.

### 13. Conversation (`stage1/13-conversation`)

- `Conversation` slice: the full turn through messages, with no slice referencing another: activation,
  listening, transcription, streamed model answer, sentence splitting, synthesis from the first sentence
  (US1.9).
- Interruption (US1.12) by the wake word, the hotkey or the click: generation cancelled, speech cut at once,
  new turn.
- Conversation window (US1.14): history in the database, input field, the model that answered each turn,
  "erase everything" button (ADR 0004).
- What is recorded for every answer: provider, model, tokens, duration (ADR 0007, section 7).
- Personality (US1.15): butler system message in French and English, editable and resettable per language,
  plus the fixed technical rules (ADR 0007, section 6).
- A written request gets a written answer only; Jarvis only speaks when spoken to (answer 3, the point ADR
  0005 refers to this plan).
- **Playwright test:** a full simulated turn in the Web host, with an interruption.
- **Windows checks:** delay between the end of the utterance and the first sound, with the real microphone
  and speaker (target under 2 s, warm); wake word while Jarvis speaks. If it is not recognized reliably,
  interruption while speaking goes through the hotkey and the click only, and a new ADR says so (ADR 0005).

### 14. First run (`stage1/14-first-run`)

- `Setup/FirstRun` slice (US1.4), started when `config.json` does not exist: language, microphone, audio
  output, provider and local model (with the connection test), download of the models into `models\` with
  their size shown and their hash checked (ADR 0006, section 7).
- Fallbacks offered when graphics memory is short or without an NVIDIA card: Whisper `small` or `base`, the
  fallback local model measured in work item 11.
- **Playwright test:** the whole wizard in the Web host, in French and in English.
- **Windows check:** first run on an empty profile (`.kazo\jarvis` folder renamed).

### 15. Faults (`stage1/15-faults`)

- Each slice already raises its fault causes as it is built. This work item goes over all of them: missing
  microphone, unreachable model, missing model, missing voice, unreadable configuration; each cause has its
  text, its corrective action and its link to the right setting (US1.17); the condition clears when the cause
  goes away or a turn succeeds.
- **Playwright tests:** each cause simulated in the Web host.

### 16. Updates (`stage1/16-updates`)

- `Updates` slice (US1.18): compares the version with the releases published on GitHub (token in the vault
  while the repository is private), download, start of the updater.
- `Kazo.Jarvis.Updater` project: replaces the files and restarts the application, like Kazo Bar.
- Publishing script in `build/`: self-contained, `win-x64`, the application and the updater in the same
  folder (ADR 0001).
- **Windows check:** an end-to-end update between two test versions.

### 17. Stage acceptance (`stage1/17-acceptance`)

- On Benoit's PC, the stage goal in French then in English: "hey jarvis", a question, the answer out loud.
- Check that no connection leaves the machine with the default configuration.
- Table of the final measurements in the project memory: delay, processor while idle and in an animated
  phase for both orbs, graphics memory, wake word threshold.
- `features.md` moves stage 1 to "Available", the project memory says the series is over.
- Benoit merges `stage1/base` into `main`.

## Windows checks at a glance

The checks the Web host cannot do, by work item. They take over the list "To check at the start of stage 1"
of the project memory.

| Check | Work item |
| --- | --- |
| Clicks around the orb reach the application below | 4 |
| Global hotkey while another application has the focus | 4 |
| Windows reload on a language change | 5 |
| Idle orb under 2 % of one core, both versions | 6, 7 |
| Real microphone, real mute | 8 |
| Wake word with Benoit's voice, threshold chosen | 9 |
| End of utterance through Silero with a real microphone | 10 |
| Ollama on 11435, fallback model measured | 11 |
| Voice quality, sound card delay | 12 |
| End-to-end delay under 2 s; wake word while speaking | 13 |
| First run on an empty profile | 14 |
| End-to-end update | 16 |
| No outgoing connection by default | 17 |

## Benoit's answers

Questions asked in the conversation on 2026-11-19. Benoit answered the same day: "1. Crée la PR. Pour le
reste je suis d'accord avec tes recommandations." (1. Create the PR. For the rest I agree with your
recommendations.)

1. **Series base.** `spikes/base` goes into `main` through a PR that Benoit merges; `stage1/base` then starts
   from `main`, as AGENTS.md wants.
2. **Continuous integration.** GitHub Actions on Linux: build of every project, non-Windows unit tests,
   Playwright against the Web host. The tests marked Windows and the checks stay on Benoit's PC. Set up in
   work item 1.
3. **Answer to a written request.** Written only. Jarvis only answers out loud when the request was spoken.
4. **Who does the Windows checks.** An agent on Benoit's PC, through Remote Control. Benoit only for what
   needs his voice or his ears (work items 9, 10, 12, 13 and 17).

## Out of this series

Tools, MCP, handoff, Claude, Claude Code sessions, task fan (ADR 0010) and spike E11: stage 2. Scheduled
tasks, triggers, Google: stage 3. Mobile app: stage 4. Full-screen "command post": not planned yet.
