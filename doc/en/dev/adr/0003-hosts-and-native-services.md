# ADR 0003: hosts (Windows WPF, Web test, Android) and native services

## Status

Proposed on 2026-11-18, waiting for Benoit's approval (work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 4, 7, 8, 15 and 36 of the [design](../plans/design.md); stories US1.1, US1.2, US1.3
  and US1.6; spike E6 ([results](../../../../spikes/e6-orb-window/RESULTS.md)); Kazo Bar's Windows host.
- **Completes:** [ADR 0001](0001-target-dotnet-10-and-projects.md) and
  [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md).
- **Completed by:** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (orb),
  [ADR 0006](0006-voice-chain.md) (voice).

## Context

- On Windows, Jarvis is a floating, transparent, always-on-top orb, with a tray icon. Settings and the
  conversation have their own windows (decision 4).
- Kazo Bar established the setup: a transparent WPF window hosting `BlazorWebView`, rendered by
  `WebView2CompositionControl`. Pixels with alpha zero let clicks through to the application below. Its
  dialog windows each have their own web view, but share the same service provider.
- Spike E6 showed that an animated transparent WPF window is expensive: every changed frame is copied by
  the processor. A still frame costs 1.3 % of one core.
- The phone is a satellite: it shows the same slices, and all processing stays on the PC (decisions 7
  and 8). Spike E10 is not done.

## Decision

### 1. Three hosts, the same slices

| Host | Technology | Role | Services | Created at |
| --- | --- | --- | --- | --- |
| `Kazo.Jarvis.Windows` | WPF and `BlazorWebView` | The product on the PC | Real: its own and those of `Adapters` | Stage 1 |
| `Kazo.Jarvis.Web` | Blazor web app | Test host, Playwright target | Simulated | Stage 1 |
| `Kazo.Jarvis.Mobile` | MAUI Blazor Hybrid, Android | Satellite of the PC | Its own, plus a relay to the PC | Stage 4 |

A host only holds start-up, service composition and platform code. No feature is written there.

### 2. Windows host

- **One process, one instance per Windows session.** A second launch shows the orb of the instance already
  open, then exits.
- **Orb window:** a transparent, borderless, always-on-top WPF window, absent from the taskbar, that does
  not take focus when shown. It hosts `BlazorWebView` (`WebView2CompositionControl`). Clicks around the orb
  reach the application below through the alpha-zero pixels. The window can be moved, and its position is
  remembered (US1.1).
- **Settings and Conversation windows:** normal, resizable windows, each with its own web view, all built on
  the same service provider. A change made in one window is seen by the others at once. A slice opens a
  window through a dialog contract, as in Kazo Bar. One window per key.
- **Tray icon** (US1.2), **global hotkey** (US1.6), **audio capture and playback**, **start with Windows**:
  services of the host.
- **Still orb when idle** (E6): the animation rule is given by the host to the orb component (ADR 0005).
- **Audio processing and models run off the UI thread.**
- **Embedded server** (for mobile and webhooks): it belongs to this host and stays closed by default. It is
  not built at stage 1. Its ADR is written with the first stage that needs it.
- **WebView2:** the WebView2 runtime is required. If it is missing, the host says so at start-up with the
  install link, instead of showing an empty window.

### 3. Web test host

- It references `Kernel` and `Features`, never `Adapters`.
- Every contract is simulated and deterministic there: wake word triggered by a button, transcription and
  model answer written in advance, silent voice with a simulated sound level.
- It holds the demo page that triggers the seven orb states without a microphone or a model (US1.3).
- Dialogs are shown over the page there, since it cannot open windows.
- It is the target of the Playwright tests.

### 4. Mobile host

- MAUI Blazor Hybrid, Android first. Created at stage 4, after spike E10.
- It shows the same slices. Their requests travel to the embedded server of the Windows host.
- Its native services (microphone, speaker, Android foreground service) and the transport are set by a
  stage 4 ADR.

### 5. Native services behind contracts

- Each native service has a contract in the kernel. The contract names no platform type (no `Window`, no
  window handle, no NAudio type).
- Each contract has a real implementation and a simulated implementation in the Web host.
- Split between the Windows host and `Adapters`:

| Goes to `Kazo.Jarvis.Windows` | Goes to `Kazo.Jarvis.Adapters` |
| --- | --- |
| What needs WPF, Win32 or the window | What wraps an external technology and can be tested without a window |
| Orb window, dialog windows, tray icon, global hotkey, audio devices, start with Windows, embedded server | Wake word, recognition, synthesis, model clients, configuration file, storage, vault, then MCP, Claude Code, Home Assistant and Google |

- Native contracts of stage 1:

| Contract | Used to |
| --- | --- |
| Orb window | Show, hide, move, remember the position |
| Dialogs | Open a component in its own window (or over the page in the Web host) |
| Tray icon | Icon, menu, left click |
| Global hotkey | Register a hotkey and receive its trigger |
| Audio capture | List microphones, open a stream, mute the microphone |
| Audio playback | List outputs, play, stop at once |
| Start with Windows | Turn on, turn off, read the state |

- A contract is added when a slice needs it, not in advance.

## Consequences

- The Web host proves the slices without hardware. What it cannot prove is checked by hand on Windows, and
  the list of those checks is part of each series plan: click-through around the orb, global hotkey while
  another application has focus, real microphone and audio output.
- Each native contract has two implementations to keep current from stage 1, and a third at stage 4.
- Click-through around the orb relies on what Kazo Bar established. Spike E6 did not measure it again: it is
  the first check of the orb slice.
- Starting with Windows follows assumption 3 of the design, which Benoit still has to confirm.
- Kazo Bar's MAUI host for Windows is not carried over: MAUI is only used for Android here.
