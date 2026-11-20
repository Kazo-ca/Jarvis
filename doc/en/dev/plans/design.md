# Kazo.Jarvis: design document
*Translated from the [French original](../../../fr/dev/plans/design.md).*

2026-11-18 · Benoit Robin

## Purpose and scope

Kazo.Jarvis is a voice assistant for Windows, written in C# on .NET 10, that runs without Python. It takes the form of an animated orb inspired by the film, answers first with a local model, and hands over to Claude when the task is beyond it. An Android app lets you talk to it, write to it and call it away from the PC.

The functional reference is OpenJarvis. We take over its useful core and add the wake word, which it lacks.

| Taken from OpenJarvis | Added | Excluded from the first version |
| --- | --- | --- |
| Voice loop (recognition, synthesis) | "hey jarvis" wake word | The 35 messaging channels |
| LLM engines: Ollama, llama.cpp, cloud APIs | Animated orb with seven states | Benchmarks and evaluations |
| Tools: files, web, calculation, MCP client | Claude session tracking | Learning and optimization |
| Conversation memory | Satellite mobile app | Mining |
| Task scheduler | Voice calls in the Android app | Languages other than French and English (v2) |
| Gmail and Calendar connectors, morning summary | Event, webhook and script triggers | Microsoft connector (interface ready, not delivered) |
| Claude Code agent | Cloud spending cap | iOS |

The code lives in a new repository, `Kazo-ca/jarvis`. The `Kazo-ca/OpenJarvis` fork stays untouched and serves as a read-only behavior reference.

## Decisions made

Benoit answered 40 questions on November 18, 2026. The numbers refer to the questions in the discussion, and each line will become an ADR or part of an ADR.

| No. | Topic | Decision |
| --- | --- | --- |
| 1 | Scope | New standalone C# application, useful core only, no Python at runtime |
| 2 | Reference | OpenJarvis, with a wake word added |
| 3, 34 | Repository and name | `Kazo.Jarvis` in `Kazo-ca/jarvis` |
| 4 | Form on Windows | Transparent floating orb, always on top, notification icon, separate windows for settings and conversation |
| 5 | Orb states | Seven states (see the section on the orb) |
| 6 | Animation | Canvas or WebGL in the shared Razor library, reactive to volume |
| 7, 8 | Mobile | Voice satellite with text mode and notifications, MAUI Blazor Hybrid, Android first, local network and VPN |
| 9, 10 | Telephony | 3CX dropped: calls go through the Android app, both ways |
| 11 | Outgoing calls | Only for a task blocked on a decision or an urgent alert, with quiet hours |
| 12 | Callers | Paired phones only, PIN or fingerprint for sensitive actions |
| 13 | Blocks taken over | Voice, engines, tools, memory, scheduler, Gmail, Calendar, morning summary |
| 14 | Home Assistant | Through the generic MCP client |
| 15 | Triggering | Wake word, global keyboard shortcut, click on the orb |
| 16 | Recognition | Local Whisper through Whisper.net, adjustable model and device |
| 17 | Synthesis | Kokoro and Piper locally, optional cloud voices, test page |
| 18 | Interruption | By wake word, shortcut or click only |
| 19, 26 | Language | French and English required, chosen at first launch, changeable afterwards |
| 20 | Handoff | The local model answers first and calls a "hand over" tool, voice override possible, monthly cap |
| 21 | Target | Claude Code for files, code and multi-step tasks, API for hard questions |
| 22, 27 | Session tracking | All Claude Code sessions on the PC and the code sessions of Claude Desktop |
| 23 | Permissions | Allowed folders, read-only by default, confirmation before writing or running a command |
| 24, 29 | Autonomous mode | Scheduled tasks, delegated tasks and six triggers: Home Assistant, email, end of a Claude session, end of a GitHub Actions build, webhook, script |
| 25 | Mobile notifications | Connection kept open by an Android service, no external service |
| 28 | Accounts | Google first, interface ready for Microsoft |
| 30 | Listening on the phone | Press to talk by default, always-on listening as an option |
| 31 | Several devices | Only the device that picked up the request answers |
| 32 | Personality | The film's butler: polite, concise, dry humor, formal address and "Sir" (in French, "vouvoiement" and "Monsieur"), editable text |
| 33 | Delivery order | Five stages (see below) |
| 35 | Plan format | This document, in the style of Kazo Bar's `doc/Features.md` |
| 36 | Architecture | Same kinds of hosts as Kazo Bar, code copied when needed but never referenced ([ADR 0014](../adr/0014-license-kazo-bar-rule-and-jarvis-py.md)), code in vertical slices, small shared kernel |
| 37 | Method | Kazo Os's, in full, docs in French then in English |
| 38 | Webhook | Incoming, protected by a token, local network and VPN |
| 39 | Scripts | Trigger or action, in PowerShell, Python or cmd |
| 40 | Generated scripts | Approval required before the first run and after every change |

## Architecture

Kazo.Jarvis does not reference Kazo Bar: no link to its projects or its packages. We do as it does, with the same kinds of hosts and the native services behind interfaces, and we may copy its code into this repository where it fits. Copied code becomes Kazo.Jarvis code and evolves separately ([ADR 0014](../adr/0014-license-kazo-bar-rule-and-jarvis-py.md)). Everything else is organized by feature, following the vertical slice architecture (VSA).

*(architecture diagram: see the original document)*

The hosts contain only startup and platform-specific code. The phone displays the same slices as the PC, but its requests travel to the server built into the Windows host, where all the processing happens.

| Project | Contents | Model in Kazo Bar (copying allowed, no reference) |
| --- | --- | --- |
| `Kazo.Jarvis.Kernel` | Assistant state machine, messages between slices, native service contracts, configuration, storage, vault | `Core`, reduced to the strict common part |
| `Kazo.Jarvis.Features` | Razor library that holds all the slices | Replaces `UI`, `Plugins` and the logic of `Infrastructure` |
| `Kazo.Jarvis.Adapters` | Implementations of external technologies, loaded by the Windows host only | The real services of `Infrastructure` |
| `Kazo.Jarvis.Windows` | WPF host with `BlazorWebView`, transparent window, notification area, audio, global shortcut, built-in server | `Windows` |
| `Kazo.Jarvis.Web` | Test host with simulated services, target of the Playwright tests | `Web` |
| `Kazo.Jarvis.Mobile` | MAUI Blazor Hybrid host for Android | The former `Maui` host, kept for Android only |
| `Kazo.Jarvis.Updater` | Update program | `Updater` and `Updates` |

A slice is a folder that contains everything needed for a feature:

```
Features/
  Voice/
    WakeWord/
      WakeWordSettings.razor        (+ .razor.css, .razor.js)
      DetectWakeWord.cs             request, processing, result
      WakeWordOptions.cs            the slice's configuration model
      WakeWordTests.cs              in the tests project, same path
    TestVoice/
      TestVoicePage.razor
      PlaySample.cs
  Handoff/
    EscalateToClaude/
    SpendCap/
```

Dependency rules, to be fixed in ADR 0002:

- A slice never references another slice. They talk to each other through messages defined in the kernel.
- A slice depends on the kernel and on contracts, never on a concrete adapter.
- We prefer to duplicate a little code between two slices rather than create a shared abstraction too early. What is used by three or more slices moves up into the kernel.
- The kernel contains no business logic, no UI component and no native dependency.
- A component's CSS and JavaScript live in files next to it, never in the markup, as in Kazo Os.

## The orb and its seven states

The orb has seven states, and each must be recognizable out of the corner of the eye without reading any text. The light reacts to the real volume: that of the microphone while listening, that of the synthesized voice while speaking.

| State | When | Look |
| --- | --- | --- |
| Idle | Waiting for the wake word | Cyan, still, low brightness |
| Listening | Wake word picked up, shortcut or click | Cyan, gentle rotation; the voice raises the light |
| Thinking | Transcription, then waiting for the model | Violet, fast rotation |
| Speaking | Synthesis in progress | Cyan; the light follows the voice, or the rings pulse at random |
| Long task | Claude or a long task is working in the background | Green, slow rotation, progress ring |
| Mic muted | Deliberate mute | Gray, still, label "microphone muted" |
| Error | Microphone, model or network unreachable | Red, a flash then still, cause shown on hover |

Common rules:

- The progress ring of a long task stays visible over the other states. Jarvis can listen or speak while Claude works.
- The colors and the drawing are set by [ADR 0011](../adr/0011-orb-colors-and-drawing.md), from the [design catalog](../ui/catalog.md). The orb exists in two versions: high quality and light.
- A "reduced motion" option removes the rotations and the pulses.
- The same component is used on Windows, in the Web test host and on mobile.

## Stage 1: orb, voice, local model and settings

At the end of this stage, you say "hey jarvis" in front of the PC, ask a question, and the local model answers aloud in French or English. Nothing leaves the machine.

| Story | As a user, I want | Acceptance criterion |
| --- | --- | --- |
| US1.1 | to see a floating orb, transparent and always on top | Clicks around the orb reach the application underneath. The orb can be moved and its position is remembered. |
| US1.2 | an icon in the notification area | Left click: show or hide the orb. Right click: Settings, Conversation, Mute microphone, Quit. |
| US1.3 | the orb to show the seven states | Each state can be triggered from a demo page of the Web host, without a microphone or a model. |
| US1.4 | to choose my language at first launch | The setup wizard offers French or English, then the microphone, the audio output and the model. The choice applies to the interface, to recognition and to the voice. |
| US1.5 | to wake Jarvis with "hey jarvis" | Continuous local detection. Sensitivity is adjustable. An indicator shows the detection score to make tuning easier. |
| US1.6 | to also activate it with a global shortcut or a click | The shortcut is configurable and works when another application has focus. |
| US1.7 | my sentence to be transcribed locally | End of sentence detected by silence. Whisper model and device (processor or graphics card) adjustable. The transcription is shown in the conversation. |
| US1.8 | to configure one or more LLM providers | Types: Ollama, llama.cpp or any OpenAI-compatible server, Claude, ChatGPT. Free address, port and model (Ollama on 11435 must work). "Test the connection" button. |
| US1.9 | to designate the default local model | The answer arrives as a stream and synthesis starts at the first complete sentence. |
| US1.10 | to hear the answer in a chosen voice | Kokoro and Piper locally. Cloud voices listed only if a key is provided. |
| US1.11 | to test the voices | The page lists voices by engine and by language, plays an editable test sentence, adjusts the speed and sets the default voice for each language. |
| US1.12 | to interrupt Jarvis | The wake word, the shortcut or a click cuts the speech and goes back to listening. |
| US1.13 | to mute the microphone | The "mic muted" state is visible and no sound is picked up. |
| US1.14 | to reread the conversation | Separate window with the history, a text input area, and the model that answered each turn. |
| US1.15 | to adjust the personality | The default system message is the butler, in French and English. It is editable and can be reset. |
| US1.16 | my keys to be protected | API keys go in the Windows Credential Manager or an encrypted file, never in the configuration file. |
| US1.17 | to understand a failure | The "error" state names the cause (microphone missing, model unreachable, voice missing) and suggests the corrective action. |
| US1.18 | to receive updates | Kazo.Jarvis's own mechanism, modeled on Kazo Bar's: GitHub version check and separate update program. |

Out of scope for this stage: tools, MCP, Claude, mobile, telephony.

## Stage 2: tools, MCP, handoff and Claude sessions

At the end of this stage, Jarvis acts: it controls the home through Home Assistant, hands heavy tasks to Claude and reports on the state of the PC's Claude sessions.

| Story | As a user, I want | Acceptance criterion |
| --- | --- | --- |
| US2.1 | the local model to use tools | Basic tools: file reading, web search, HTTP request, calculation, date and time. Each tool call appears in the conversation. |
| US2.2 | to plug in MCP servers | Added by address or by local command, list of exposed tools, tool-by-tool activation. |
| US2.3 | to control Home Assistant by voice | The Home Assistant MCP server is configured like the others. "Turn on the living room" works in French and English. |
| US2.4 | Jarvis to remember | Persistent conversation memory, with automatic summary of older exchanges. A page lets me see and erase what it retains. |
| US2.5 | the local model to hand over on its own | It has a "hand over" tool with a reason. The orb and the conversation show who is answering. |
| US2.6 | to force the handoff by voice | "Ask Claude" or "demande à Claude" sends the request directly to the configured target. |
| US2.7 | to choose the targets | One target for tasks with files or steps (Claude Code) and one for hard questions (Claude API or ChatGPT). |
| US2.8 | to cap cloud spending | Adjustable monthly amount. The counter is visible. Once the cap is reached, Jarvis says so and stays local. |
| US2.9 | to launch a Claude Code task by voice | Jarvis chooses or asks for the folder from the allowed list, starts the session and switches to the "delegated task" state. |
| US2.10 | to keep control of writes | Read-only by default. Any write or command asks for confirmation, by voice or on the orb, with a summary of the action. |
| US2.11 | to see all the Claude Code sessions on the PC | List of sessions with folder, state (running, waiting, finished) and last activity, including those started by hand. |
| US2.12 | to be notified by voice | Announcement when a session ends or waits for a permission. Announcements are adjustable per folder and respect quiet hours. |
| US2.13 | to ask for a summary | "Where is Claude on Kazo Bar?" produces a summary made by the local model from the session. |
| US2.14 | to also follow Claude Desktop | Its code sessions appear in the same list. Depends on technical spike E3. |
| US2.15 | to answer a permission request | From the orb or by voice, for sessions started by Jarvis. For the others, Jarvis only announces. |

Out of scope for this stage: scheduled tasks, triggers, email.

## Stage 3: autonomous mode, triggers and email

At the end of this stage, Jarvis works without being spoken to: at a fixed time, on an event, or on a long task entrusted to it before leaving.

An autonomous task is described in natural language. It has a trigger, an instruction, a target (local model or Claude) and a reporting rule.

| Story | As a user, I want | Acceptance criterion |
| --- | --- | --- |
| US3.1 | to create a scheduled task | By voice or in a dedicated page. Simple schedule (every day at 7 a.m.) or advanced expression. Tasks missed while the PC was off are flagged, not replayed in a burst. |
| US3.2 | to entrust a long task and leave | The task continues in the background, its state is visible, and it can be cancelled. |
| US3.3 | to trigger on a Home Assistant event | Choice of an entity and a condition. Depends on technical spike E9. |
| US3.4 | to trigger on an email | Rule on the sender, the subject or the label. |
| US3.5 | to trigger at the end of a Claude session | Per folder or for all sessions. |
| US3.6 | to trigger at the end of a GitHub Actions build | Per repository, with a filter on the result (success, failure). |
| US3.7 | to trigger by incoming webhook | Each webhook has its own address and token. The received body is passed to the task as data. Listens on the local network and the VPN only. |
| US3.8 | to use a script as a trigger | Jarvis runs it at a set interval. The exit code decides whether there is an event, and the standard output is passed to the task. |
| US3.9 | to use a script as an action | PowerShell, Python or cmd, with the output shown live and a stop button. |
| US3.10 | to have Jarvis or Claude write a script | The generated script stays "pending" until I approve it, on screen or by voice after hearing a summary. Any modification puts the script back on hold. My own scripts are approved automatically. |
| US3.11 | to connect my Google account | OAuth sign-in, tokens in the vault. Gmail read-only and Calendar read-only to start. |
| US3.12 | to receive the morning summary | Scheduled task provided by default: important emails, today's agenda, weather, read aloud. The content is adjustable. |
| US3.13 | to be notified according to urgency | Each task has a level: silent, notification, voice announcement, call (stage 5). Quiet hours apply to everything except the urgent level. |
| US3.14 | to consult the log | Each run keeps its trigger, its duration, its result, its cloud cost and the actions taken. |
| US3.15 | autonomous tasks to stay within their limits | An autonomous task can never approve a write or a script itself. If it needs one, it goes on hold and notifies me. |

Sending emails and modifying the calendar are excluded from this stage.

## Stage 4: Android mobile app

At the end of this stage, the phone is a satellite of the PC installation: you speak, write and receive notifications on it. All the processing stays on the PC.

| Story | As a user, I want | Acceptance criterion |
| --- | --- | --- |
| US4.1 | to enable the PC server | A setting opens listening on the local network. It is off by default. |
| US4.2 | to pair my phone | The PC displays a QR code. The phone reads it and receives a token specific to that device. |
| US4.3 | to manage paired devices | List on the PC with the last connection, and revocation in one click. |
| US4.4 | to see the orb on the phone | Same component and same states as on Windows, synchronized in real time. |
| US4.5 | to talk to Jarvis from the phone | Tap the orb, the audio goes to the PC, the answer comes back and plays on the phone. |
| US4.6 | to write instead of speaking | Text mode with the same conversation as on the PC. |
| US4.7 | to receive notifications | Through a connection kept open by an Android service. A permanent notification shows that the service is running. |
| US4.8 | to answer from a notification | Approve or deny a permission request, or open the conversation. |
| US4.9 | to enable always-on listening | Option off by default, with an "only while charging" choice. Depends on technical spike E10. |
| US4.10 | only the right device to answer | The device that picked up the request speaks. The others show the state silently. |
| US4.11 | to use it away from home | Through a VPN such as Tailscale. The app accepts a VPN address and exposes nothing on the Internet. |
| US4.12 | to know that the PC is unreachable | Clear "error" state, automatic retry, no loss of the text being typed. |

The full settings stay on the PC. The phone only adjusts what concerns it: server, microphone, always-on listening, notifications.

## Stage 5: calls between Jarvis and the Android app

> **Rewritten on November 19, 2026** at Benoit's request. 3CX telephony has been dropped since November 18, 2026 and spike E1 is no longer needed. The Android app plays the role of the phone, with the same controls as those planned for 3CX.

At the end of this stage, you call Jarvis from the Android app to talk, and Jarvis calls the app when an autonomous task needs a decision.

A call goes through the same connection as the rest of the app, on the local network or the VPN. So there is no phone number, no phone system and no external service. In return, Jarvis cannot call a phone that cannot reach the PC.

| Story | As a user, I want | Acceptance criterion |
| --- | --- | --- |
| US5.1 | to call Jarvis | A call button in the app opens a continuous conversation, with no wake word and no press for each sentence. It picks up and greets. |
| US5.2 | a smooth conversation on the phone | End of sentence detected by silence, short response delay, a button to cut Jarvis's speech. Earpiece, speaker and Bluetooth, screen off possible. |
| US5.3 | Jarvis to call me | The phone rings as for an incoming call, full screen even when locked, with Accept and Decline. Only for a task blocked on a decision or an urgent-level alert. Jarvis explains the situation and waits for the answer. |
| US5.4 | quiet hours | No call during the set range, except urgent level. The request becomes a notification. |
| US5.5 | a fallback if I do not answer | Adjustable number of attempts and delay, then a notification. Same fallback if the phone is unreachable. The task stays pending. |
| US5.6 | to protect sensitive actions | During a call, a PIN or the phone's fingerprint is required before security home automation, writes and code execution. Three failures end the call. |
| US5.7 | to see the call on the orb | The orb on the PC and the one on the phone show that a call is in progress. The transcript is added to the conversation. |
| US5.8 | to consult the call log | Date, direction, device, duration, outcome (answered, missed, declined) and actions requested, on the PC and in the app. |
| US5.9 | only my devices to be able to call | Only paired phones (US4.2) can call Jarvis or be called. |

This stage depends on technical spike E10: ringing a locked phone without an external notification service is the most uncertain point. Its ADRs are written at the start of its series, and will have to say how protecting sensitive actions during a call fits with [ADR 0009](../adr/0009-permission-and-confirmation-model.md).

## Security and privacy

Jarvis listens all the time, controls the home and can run code. These nine rules apply to all stages.

1. **Local by default.** The wake word, recognition and synthesis run on the PC. Audio leaves the machine only if a cloud voice or model is explicitly chosen.
2. **Nothing is recorded before the wake word.** The listening buffer stays in memory and is never written to disk.
3. **Secrets kept out of the configuration.** API keys, OAuth tokens and device tokens go in the vault, as in Kazo Bar.
4. **External content is data, not an order.** An email, a webhook body, a web page or a script's output cannot give instructions to Jarvis. This is the main defense against prompt injection.
5. **Confirmation before what cannot be undone.** Writing files, commands, generated scripts and security home automation require approval, and an autonomous task cannot grant it to itself.
6. **Allowed folders.** Claude Code and the file tools see only the folders on the list.
7. **Network closed by default.** The server for mobile and webhooks listens only if enabled, on the local network and the VPN, with one token per device or per webhook.
8. **Capped spending.** The monthly cap blocks the cloud once reached.
9. **Everything is logged.** Tool calls, handoffs, autonomous runs and calls can be viewed and erased.

A voice confirmation can be picked up by mistake or spoken by someone else. The riskiest actions therefore require a click on the orb, the paired phone or the PIN.

## Technical spikes to do before coding

Ten points rest on assumptions I could not verify. Each is settled by a small throwaway spike, stored in `spikes/` as in Kazo Bar, with a written result.

| Spike | Blocks | What it must prove |
| --- | --- | --- |
| E1 | Stage 5 | Dropped with 3CX telephony. |
| E2 | Stage 2 | The PC's Claude Code sessions can be listed and their state (running, waiting for permission, finished) known from their files and from hooks, without disturbing them. |
| E3 | US2.14 | Claude Desktop's code sessions are readable the same way. Otherwise, we limit ourselves to "busy or waiting". |
| E4 | Stage 1 | The voice chain holds up in C#: "hey jarvis" ONNX model, Whisper.net, Kokoro and Piper. Measure the delay between the end of the sentence and the first sound, with a target under 2 seconds. |
| E5 | Stage 1 | Whisper and the local model coexist on the 12 GB RTX 3060, or Whisper moves to the processor with an acceptable delay. |
| E6 | Stage 1 | The Canvas or WebGL animation runs in the transparent WPF window without notable processor cost when idle. Target: less than 2% of one core. |
| E7 | Stage 2 | The local model (qwen2.5-coder 14B) calls tools reliably in French, including "hand over". Otherwise, choose another default local model. |
| E8 | Stage 2 | The best way to drive Claude Code from C#: command line in non-interactive mode, or a bridge to the Claude Agent SDK as OpenJarvis does. |
| E9 | US3.3 | Home Assistant events arrive through MCP. Otherwise, go through its events API for triggers only. |
| E10 | Stage 4 | Audio transport between phone and PC (WebRTC or WebSocket), battery cost of always-on listening, and an incoming call from Jarvis that rings the locked phone through the kept-alive connection alone. Also blocks stage 5. |

E4, E5 and E6 go first, because stage 1 depends on them. E10 is done before stage 4.

## Working method and first ADRs

The `Kazo-ca/jarvis` repository takes over Kazo Os's method in full. Here is what must exist before the first line of code.

| Item | Role |
| --- | --- |
| `AGENTS.md`, imported by `CLAUDE.md` | Rules for any agent: French with Benoit, multiple-choice questions with a recommendation, VSA, CSS and JS files next to their component, English file names |
| `doc/fr/` and `doc/en/` mirrored | French first, English translated in the same change, one index `README.md` per folder |
| `doc/fr/dev/adr/` | One numbered ADR per decision, written and merged before the code it describes. An accepted ADR is not modified, a new one replaces it. |
| `doc/fr/memory/` | Resume point: state of the series, next free ADR number, decisions made but not built, known test failures |
| `doc/fr/features.md` | Product overview, updated by every PR to `main` |
| `doc/fr/dev/plans/` | This document, then one plan per series |
| `doc/fr/dev/pitfalls.md` | Pitfalls encountered and their solution |
| `TODO` tickets | Everything that is "to add to the list", with a "Dependencies" section |
| Branches | `<series>/base` from `main`, one `<series>/<n>-<topic>` branch per work item. Only Benoit merges into `main`. |
| Tests | Unit tests per slice, Playwright tests against the Web host. No test disabled to get green. |

First ADRs to write, in this order:

1. [ADR 0001](../adr/0001-target-dotnet-10-and-projects.md): .NET 10 target and list of projects.
2. [ADR 0002](../adr/0002-vertical-slices-kernel-and-dependency-rules.md): vertical slices, shared kernel and dependency rules.
3. [ADR 0003](../adr/0003-hosts-and-native-services.md): hosts (Windows WPF, Web test, Android) and native services.
4. [ADR 0004](../adr/0004-configuration-storage-and-secret-vault.md): configuration, storage and secrets vault.
5. [ADR 0005](../adr/0005-assistant-state-machine-and-orb-contract.md): assistant state machine and orb contract.
6. [ADR 0006](../adr/0006-voice-chain.md): voice chain (wake word, recognition, synthesis), after E4 and E5.
7. [ADR 0007](../adr/0007-llm-providers-and-tool-format.md): LLM providers and tool format.
8. [ADR 0008](../adr/0008-languages-and-localization.md): languages and localization.
9. [ADR 0009](../adr/0009-permission-and-confirmation-model.md): permission and confirmation model.

The ADRs for stages 2 to 5 are written at the start of each series, once the corresponding technical spikes are finished.

## Assumptions to confirm

I made nine detail decisions without asking you. Check the ones you agree with, and comment on those you want to change.

- [x] One user per installation. No accounts and no recognition of each person's voice.
- [x] Conversation history and memory are stored locally in `%USERPROFILE%\.kazo\jarvis`, with no time limit, with a button to erase everything. (Location corrected by Benoit on November 19, 2026: the user's folder, not `%APPDATA%`. See [ADR 0004](../adr/0004-configuration-storage-and-secret-vault.md).)
- [x] Jarvis starts with Windows by default, and the option can be turned off in the settings.
- [x] Kazo.Jarvis does not reference Kazo Bar and adds no module to it. Its code may be copied into the Jarvis repository ([ADR 0014](../adr/0014-license-kazo-bar-rule-and-jarvis-py.md)).
- [x] Gmail and Calendar are read-only in the first version. Sending an email or creating an event will come later.
- [x] The phone does not change the PC's settings. It only changes its own.
- [x] The repository is public and under the same license as OpenJarvis, Apache 2.0 ([ADR 0014](../adr/0014-license-kazo-bar-rule-and-jarvis-py.md)). Corrected by Benoit on November 19, 2026.
- [x] `jarvis.py` is replaced by Kazo.Jarvis and is no longer used ([ADR 0014](../adr/0014-license-kazo-bar-rule-and-jarvis-py.md)). Corrected by Benoit on November 19, 2026.
- [x] The wake word stays "hey jarvis" in both languages. A custom wake word would require training a model and waits for v2.

Next action: create the `Kazo-ca/jarvis` repository, put this plan in it along with `AGENTS.md`, then launch spikes E4, E5 and E6.
