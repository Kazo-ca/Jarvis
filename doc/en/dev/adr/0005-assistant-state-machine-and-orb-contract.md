# ADR 0005: assistant state machine and orb contract

## Status

Proposed on 2026-11-18, waiting for Benoit's approval (work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 5, 6, 15, 18 and 31 of the [design](../plans/design.md) and its section "The orb
  and its seven states"; stories US1.1, US1.3, US1.6, US1.12, US1.13 and US1.17; spike E6
  ([results](../../../../spikes/e6-orb-window/RESULTS.md)); decision 1 of the
  [spikes](../spikes.md#decisions-taken-on-november-18-2026) (still orb when idle).
- **Completes:** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (the state machine is in
  the kernel) and [ADR 0003](0003-hosts-and-native-services.md) (orb window).
- **Completed by:** [ADR 0006](0006-voice-chain.md) (which produces the voice events).

## Context

- The orb has seven states, and each one must be recognizable at a glance. The "delegated task" state
  overlays the others: Jarvis can listen or speak while Claude works.
- Slices do not reference each other (ADR 0002). So there must be a single place that says which state the
  assistant is in, and a single way to make it change.
- Spike E6 missed its target: no setup stays under 2 % of one core with an animated orb when idle. A still
  frame costs 1.3 %. At 30 frames per second, the orb costs 14 to 18 % of one core.
- A `requestAnimationFrame` loop wakes the engine 60 times per second even without drawing
  ([pitfalls](../pitfalls.md)).
- The same orb component is used on Windows, in the Web host and on mobile.

## Decision

### 1. One phase and three conditions

The assistant state is made of one exclusive **phase** and of **conditions** added to it.

| Phase | When |
| --- | --- |
| Idle (`Idle`) | Waits for an activation |
| Listening (`Listening`) | Captures the request |
| Thinking (`Thinking`) | Transcribes, then waits for the model |
| Speaking (`Speaking`) | Says the answer |

| Condition | When |
| --- | --- |
| Microphone muted (`MicrophoneMuted`) | Deliberate mute (US1.13) |
| Fault (`Fault`) | Microphone, model or voice unusable; carries the cause and the corrective action (US1.17) |
| Delegated tasks (`DelegatedTasks`) | Number of background tasks (stage 2) |

### 2. From the seven states of the design to the display

The orb shows a single **look**, computed by a pure kernel function, the same for every host:

1. if the phase is Listening, Thinking or Speaking: the look of that phase;
2. otherwise, if there is a fault: the "error" look;
3. otherwise, if the microphone is muted: the "microphone muted" look;
4. otherwise: the "idle" look.

The satellites of delegated tasks are drawn over the look, whatever it is. This gives back the seven states
of the design: six exclusive looks and one overlay.

### 3. Transitions

Each conversation turn has an identifier. Every message of the turn carries it.

| Phase | Event | New phase | Effect |
| --- | --- | --- | --- |
| Idle | Activation (wake word, hotkey, click) | Listening | New turn. Refused when the microphone is muted or missing. |
| Idle | Text submitted in the conversation | Thinking | New turn, without the microphone. |
| Listening | End of utterance | Thinking | |
| Listening | Nothing heard before the timeout | Idle | Turn dropped. |
| Listening | Hotkey or click | Idle | Listening cancelled. |
| Listening | Microphone muted | Idle | Turn cancelled. |
| Thinking | First sentence ready to be said | Speaking | |
| Thinking | Answer finished without speech | Idle | |
| Thinking, Speaking | Activation (wake word, hotkey, click) | Listening | Interruption (US1.12): the current turn is cancelled, speech stops at once, a new turn starts. |
| Speaking | End of speech | Idle | |
| Listening, Thinking, Speaking | Turn failed | Idle | The "fault" condition is set with its cause. |

- While listening, the wake word is ignored.
- Any (phase, event) pair missing from the table is ignored and logged.
- A message carrying the identifier of a cancelled turn is ignored.
- The "fault" condition is cleared when its cause disappears or when a turn succeeds.

### 4. Rules of the machine

- The machine is in the kernel. It is a pure function: (state, event) gives (new state, effects). It does
  no input or output and has no timer: a timeout is an event published by a slice.
- **A single writer.** Events are processed one at a time, in arrival order.
- **Nobody sets the state directly.** A slice publishes an event; the machine decides. Every change is
  published by the `AssistantStateChanged` message.
- It is tested without a microphone, a model or a user interface.

### 5. Orb contract

- **A single Razor component**, in the orb slice, for the three hosts. It draws with Canvas 2D, in a
  `.razor.js` file next to the component. The JavaScript only draws: no state rule is written there.
- **Component inputs:** the look, the number of delegated tasks, the cause of the fault (shown on hover),
  the sound level, the "reduced motion" option and the animation rule given by the host.
- **Sound level:** a value from 0 to 1, the microphone's while listening and the synthesized voice's while
  speaking. It comes through a dedicated contract, not through the bus, at most 30 times per second.
- **Component outputs:** click (activation, US1.6) and start of a move (US1.1).
- **Demo page** (US1.3): it drives the component directly through its inputs, without a microphone or a
  model.

### 6. Animation rule

| Situation | Rule |
| --- | --- |
| Idle, microphone muted, error on Windows | **Still frame.** One draw when the look changes, then no timer and no `requestAnimationFrame`. |
| Listening, thinking, speaking | Full animation, at most 30 frames per second. |
| Animation under 10 frames per second | With a timer, never with `requestAnimationFrame`. |
| Delegated task satellites, when idle on Windows | Drawn still. Their position moves in steps, at most one frame every 5 seconds to start with; the interval is tuned by measurement. |
| Reduced motion | No animation: looks differ by hue and shape. |

- The "still frame when idle" rule is a parameter the host gives to the component. The Windows host turns
  it on. The Web host offers both, for the demo. The choice for mobile will be made at stage 4.
- **Idle budget on Windows:** under 2 % of one core for the display (WPF process and WebView2 processes),
  measured with the method of spike E6 on the real host before the orb slice is declared done. The wake
  word has its own cost, measured separately (ADR 0006).
- Animated phases last a few seconds. Their cost (14 to 18 % of one core at 30 frames per second) is
  accepted.

### 7. What this ADR does not decide

- **The colors and the drawing of the orb.** The design refers them to a dedicated ADR, to be written from
  a mockup before the visual work of the orb slice.
- **Whether the answer to a typed request is also said aloud.** The table allows both; the plan of the
  conversation series settles it.
- **Several devices** (decision 31). The state is computed on the PC. How it is sent to the phones and how
  the device that speaks is chosen are set by the mobile ADR, at stage 4.

## Consequences

- The animated idle orb of the design ("slow rings, calm breathing") is replaced on Windows by a still
  frame. This is the decision taken after spike E6.
- Adding a state requires a new ADR: the transition table and the look function are the contract between
  all slices.
- The choreography of a turn goes through messages: the wake word slice knows neither the transcription nor
  the model.
- **Risk to check at stage 1:** while speaking, the wake word must be recognized over Jarvis's own voice
  coming out of the speakers. This is not measured. If detection is not reliable, interruption while
  speaking will only work with the hotkey and the click, and a new ADR will say so.
- Native GPU rendering (option 3 of spike E6) is set aside: it would need a second orb component, which
  mobile and the Web host would not share.
