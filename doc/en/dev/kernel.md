# The kernel: bus, state machine, sound level

2026-11-19 · Work item `stage1/2-kernel`. Built from [ADR 0002](adr/0002-vertical-slices-kernel-and-dependency-rules.md)
(section 5, messages between slices), [ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md)
(state machine) and [ADR 0011](adr/0011-orb-colors-and-drawing.md) (section 4, look function).

This document says how a slice uses the kernel. The rules themselves are in the ADRs.

## Contents

```
src/Kazo.Jarvis.Kernel/
  KernelServiceCollectionExtensions.cs   AddJarvisKernel(), called by AddJarvisFeatures()
  Messaging/                             IEvent, IRequest<T>, IMessageBus, handlers, in-memory bus
  Assistant/                             state, phase, conditions, turn, look function, machine, single writer
  Messages/Assistant/                    input messages of the machine and the messages it publishes
  Audio/                                 sound level and microphone stream (outside the bus), capture and playback contracts (see audio.md)
  Messages/Audio/                        MicrophoneMuteRequested
  Speech/                                voice engine contracts, voice choice, voice keys (see speech.md)
  Messages/Speech/                       SentenceToSpeak, AnswerSentencesEnded
  Settings/                              settings contracts (see storage.md), pages of the settings window
  Localization/                          assistant language, language reader, Windows language (see shared-ui.md)
  Messages/Localization/                 AssistantLanguageChanged
  Storage/                               document store contract (see storage.md)
  Secrets/                               secret vault contract (see storage.md)
  Tools/                                 tool, tool registry, switches (see tools.md)
  Conversation/                          what the memory recalls to the model (see conversation-memory.md), hand-off (see hand-off.md)
  Messages/Conversation/                 TurnRecorded, ExchangesLeftHistory
  Tools/Servers/                         tool server: connection, tools, call (see mcp.md), classification per type of server (see home-assistant.md)
  Files/                                 local files, read only (see basic-tools.md)
  Tasks/                                 shape of a task, project and topic keys, board of the tasks (see tasks-and-sessions.md)
  ClaudeCode/                            registry of the running Claude Code sessions (see tasks-and-sessions.md)
  Web/                                   reading browser, search by API, allowed addresses (see basic-tools.md)
  Permissions/                           action classes, permission decision point (see tools.md)
```

A host does not call `AddJarvisKernel()` itself: `AddJarvisFeatures()` does. Calling it again has no effect.

## The bus

- **Event** (`IEvent`): a past fact, zero, one or several subscribers. **Request** (`IRequest<TResult>`):
  exactly one handler, otherwise `SendAsync` throws `InvalidOperationException`.
- Two ways to subscribe:
  - an `IEventHandler<TEvent>` registered in the container, as a singleton, by the slice's registration
    method (the normal case);
  - `IMessageBus.Subscribe<TEvent>(…)`, for a subscription that lives with a Razor component; dispose it in
    `Dispose`.
- A subscriber also receives the events of derived types: subscribing to `AssistantInput` receives every
  input of the machine, subscribing to `IEvent` receives everything.
- **Order:** the container's handlers, then the subscriptions, in the order they were made, one after the
  other. `PublishAsync` completes when all of them are done.
- **A failing subscriber** is logged (`Error` level) and does not stop the others from receiving the event.
  The exception of a request handler, on the other hand, reaches the caller.
- The bus also works in a host without logging (the Windows host has none yet): it then takes a silent log.
- The messages of a slice are grouped by area: `Kernel/Messages/<Area>/`.

## The state machine

- `AssistantState`: the phase (`Idle`, `Listening`, `Thinking`, `Speaking`), the current turn, the last turn,
  and the conditions (microphone muted, fault, number of delegated tasks). Immutable.
- `AssistantStateMachine.Apply(state, input)`: the transition table of ADR 0005, a pure function. It returns
  the new state, the effects to publish, or the reason the input is ignored.
- `AssistantStateKeeper`: the **single writer**. It receives every `AssistantInput` from the bus, applies them
  one at a time in arrival order, then publishes `AssistantStateChanged` and the effects (`TurnStarted`,
  `TurnCancelled`, `ActivationRefused`). An ignored input is logged (`Debug` level) and publishes nothing.
- `IAssistantStateReader.Current`: the current state, for a component that starts after the last change.
- **Nobody sets the state.** A slice publishes an input and waits for `AssistantStateChanged`.

| Input published by a slice | When |
| --- | --- |
| `ActivationRequested(source)` | Wake word, hotkey or click |
| `TextSubmitted(text)` | Text sent from the conversation window |
| `UtteranceEnded(turn)` / `NothingHeard(turn)` | End of the utterance / nothing heard before the timeout |
| `FirstSentenceReady(turn)` / `AnswerFinishedWithoutSpeech(turn)` / `SpeechEnded(turn)` | Steps of the answer |
| `TurnFailed(turn, fault)` | The turn cannot finish |
| `MicrophoneMuteChanged(muted)` | Microphone muted or unmuted |
| `FaultRaised(fault)` / `FaultResolved(cause)` | Fault found outside a turn / cause gone |
| `DelegatedTaskCountChanged(count)` | Stage 2 |

- **Turns.** Each turn has a number (`TurnId`) that follows the previous one. The messages of a turn carry it;
  a message from a cancelled or finished turn is ignored.
- **No re-entry.** An input published by a subscriber while another one is processed (a slice reacting to
  `TurnStarted`, for instance) waits its turn: it is processed after the effects of the first one.
  `PublishAsync` of an input can therefore return before the input is applied.
- **Faults.** The kernel only names the cause (`FaultCause`); the text, the corrective action and the link to
  the right setting come from the slices (work item `stage1/15-faults`). `MicrophoneMissing` refuses
  activations, like the muted microphone.
- **Look.** `AssistantLooks.Of(state)` gives one of the eight looks (ADR 0011, section 4; "interaction required" comes from ADR 0023 and wins over all the others);
  `AssistantLooks.ShowsProgressRing(state)` says whether the progress ring is drawn over it.

### Case ADR 0005 does not cover

An interruption (wake word, hotkey or click while Jarvis thinks or speaks) while the microphone is muted or
missing: the turn is still cancelled and speech stops (it was asked for), but listening does not start. The
machine publishes `TurnCancelled` then `ActivationRefused`, and goes back to idle.

## The sound level

Outside the bus (ADR 0002, section 5), in `Kernel/Audio/`:

- `ISoundLevelSink.Report(origin, value)`: called by the audio slices (microphone in work item 8, voice in
  work item 12). The value is clamped between 0 and 1.
- `ISoundLevelSource.Subscribe(…)`: read by the orb. At most 30 values per second **for each origin**; extra
  values are dropped, except silence (0), which always passes so the orb does not stay lit.
- The orb chooses the origin from the look: the microphone while listening, the voice while speaking.
