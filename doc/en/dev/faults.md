# Faults

Work item `stage1/15-faults` (US1.17, [stage 1 plan](plans/stage-1.md#15-faults-stage115-faults),
[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md), sections 1 and 3). Français :
[faults.md](../../fr/dev/faults.md).

## What is built

Each slice raises and clears its own fault causes; the new `Assistant/Faults` slice names them, says what to do, opens
the right setting, and raises the one cause no slice owns: an unusable settings file. The "fault" condition of ADR 0005
did not change: one fault at a time, the last raised, cleared when its cause goes away or a turn succeeds.

### Kernel

| Item | Role |
| --- | --- |
| `FaultOrigin` (`Kernel/Assistant`) | The part of Jarvis that found the fault: microphone, settings, wake word, transcription, language model, voice. One cause comes from several places (Whisper, Piper or Ollama without a model all give `ModelMissing`): the origin picks the right text and setting. |
| `AssistantFault.Origin`, `AssistantFault.Matches` | The origin, optional (without it, the usual origin of the cause applies). `Matches` says whether a cleared cause designates this fault. |
| `FaultResolved(Cause, Origin)` | The origin is optional: when given, it clears only the fault of that origin (Whisper found again does not clear a missing voice). |
| `SettingsWindowRequested(PageKey)` | The settings window opens on the requested page, or switches to it when already open. |
| `FaultWindowRequested` (`Messages/Desktop`) | The "Solve the problem…" entry of the tray menu. |

The transition table did not change: no new state, so no new ADR (ADR 0005, "Consequences").

### Slice `Features/Assistant/Faults`

- **`FaultAdvice`**: for each cause and origin, the title, the explanation, the corrective action and the settings page
  that fixes the fault.

| Cause (origin) | Title | Page opened |
| --- | --- | --- |
| `MicrophoneMissing` | No microphone | Audio (`Audio.Devices`) |
| `ModelUnreachable` (language model) | Language model unreachable | Language models (`Llm.Providers`) |
| `ModelMissing` (language model) | Language model missing | Language models |
| `ModelMissing` (transcription) | Transcription model missing | Voice models (`Setup.Models`) |
| `ModelMissing` (voice) | Voice missing | Voice models |
| `ModelMissing` (wake word) | Wake word missing | Wake word (`Voice.WakeWord`) |
| `VoiceMissing` | Silent voice | Voices (`Voice.TestVoice`) |
| `ConfigurationUnreadable` | Unreadable settings (unreadable, newer version, failed migration) | none: the text names `config.json` and says what to do |

- **`FaultWindow`** ("Jarvis problem" window): the current fault, its explanation, "What to do:", the "Open the “…”
  page" button and the technical detail. It follows the state: when the cause goes away, it says "The problem is solved".
- **`FaultWindowOpener`**: opens the window on `FaultWindowRequested`, and when an activation is refused for want of a
  microphone (`ActivationRefused` with `MicrophoneMissing`): a click on the orb or a hotkey that does nothing says why.
  A muted microphone opens nothing: it is a choice, and the orb shows it.
- **`ConfigurationFaultReporter`**: at start, when `config.json` cannot be saved (unreadable, written by a newer
  version, failed migration), raises `ConfigurationUnreadable` with the reason.

### Who raises and who clears each cause

| Cause | Raised by | Cleared by |
| --- | --- | --- |
| No microphone | `Audio/Devices` (`MicrophoneService`) | the same, as soon as a microphone answers |
| Model unreachable | `Conversation/Dialogue` (failed turn); `Llm/Providers` after **two** failed loads in a row (`LanguageModelFaultWatcher`) | a successful load (`LanguageModelFaultWatcher`), a successful turn |
| Language model missing | the same, **at the first** failed load | the same |
| Whisper missing | `Voice/Transcription` (failed turn) | Whisper loaded (`TranscriptionFaultWatcher`), a successful turn |
| Silent or missing voice | `Voice/Speech` (failed turn) | a voice heard to the end on the "Voices" page (`VoiceTrial`), a successful turn |
| Unreadable settings | `Assistant/Faults` at start | a start on a readable file, a successful turn |

Missing wake word models are still not a fault (work item 9 choice): the "Wake word" page says so. The `FaultAdvice`
row is there if a slice ever raises it.

### Hosts

- **Windows**: the "Solve the problem…" tray menu entry shows only during a fault, at the top of the menu.
- **Web**: one button per simulated cause (model server stopped, language model removed, Whisper file removed, voice
  broken, unreadable `config.json`), the "Solve the problem (tray menu)" button, and `#fault`, which shows the current
  cause and origin. The simulated connector has `ForcedFailure`, the simulated transcription `Missing`, the in-memory
  settings `Simulate`.

## Provisional choices of this work item

To be confirmed by Benoit; each changes in one line:

1. **Where the fault is read**: a "Jarvis problem" window, opened by the tray menu entry (visible only during a fault)
   and by itself when a click or the hotkey is refused for want of a microphone. Nothing opens by itself for the other
   causes: the red orb and its hover text are enough. No notification balloon.
2. **Server unreachable at start**: the fault is raised only after two failed loads in a row (at least 15 s), so the orb
   does not turn red at every logon while Docker starts Ollama. A missing model is raised at once.
3. **The "Voices" page clears the voice fault** when a voice is heard there to the end.
4. **Unreadable settings**: no button (no page fixes a file Jarvis refuses to overwrite); the text says to fix or rename
   `config.json` then restart. No "Start again from default settings" button: that would delete a file, Benoit's call.
5. **Origin in the kernel** (`FaultOrigin`) rather than page keys in the messages: slices only say what they are,
   `Assistant/Faults` decides the text and the page.
6. **Texts**: those of `FaultWindow.resx` and `FaultWindow.fr.resx`; the action for an unreachable model points to the
   "Test the connection" button of the language models page, which names the precise cause.

## Tests

- Unit: `FaultsTests` (three texts for each cause and origin in both languages, every named page exists, the right page
  per cause, window opened by the entry and by a refusal for want of a microphone, requested page selected in the
  settings window, unreadable or newer `config.json` raised at start), `LanguageModelFaultWatcherTests`,
  `TranscriptionFaultWatcherTests`, `VoiceTrialFaultTests`, and the state machine (an origin clears only its own).
- Playwright (`FaultsTests`): each cause simulated in the Web host, its title, its action, the button that opens the
  right page, then the fault cleared when the cause goes away or a turn succeeds.
- Screenshots (French) in the project files, folder `stage1-15/`.

## Windows checks

To do at the next session with Benoit:

1. Unplug the microphone: the orb turns red, the "Solve the problem…" entry shows at the top of the tray menu;
   `Ctrl+Alt+J` opens the "Jarvis problem" window; "Open the “Audio” page" opens the settings on Audio; plug it back:
   the window says "The problem is solved" and the entry goes away.
2. Stop the Ollama container, restart Jarvis: the orb turns red only after about 15 s; restart Ollama: the orb is blue
   again at the next load attempt (15 s to 5 min depending on the number of failures) or at the next turn.
3. Log on to Windows with Docker starting Ollama: the orb must not turn red.
4. Write an invalid `config.json` (keep a copy), restart: "Unreadable settings" fault; put the file back.
