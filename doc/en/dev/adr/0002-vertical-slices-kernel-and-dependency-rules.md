# ADR 0002: vertical slices, shared kernel and dependency rules

## Status

Accepted by Benoit on 2026-11-19 (proposed on 2026-11-18, work item `spikes/11-stage-1-adrs`).

- **Sources:** decision 36 of the [design](../plans/design.md) and its "Architecture" section; section I of
  `AGENTS.md`.
- **Completes:** [ADR 0001](0001-target-dotnet-10-and-projects.md) (list of projects).
- **Completed by:** [ADR 0003](0003-hosts-and-native-services.md) (hosts and native contracts),
  [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (state machine).

## Context

- Benoit asked for a Vertical Slice Architecture (VSA): code is organized by feature, not by technical
  layer.
- Kazo Bar organizes its code by layer (`Core`, `Infrastructure`, `UI`, `Plugins`). We keep its hosts and
  its native services behind interfaces, not that split.
- The design gives four dependency rules and refers them to this ADR.
- A rule nobody checks ends up forgotten. Kazo Os fails the build when a component holds style or script in
  its markup (its ADR 0007).

## Decision

### 1. Direction of dependencies

```
Hosts (Windows, Web, Mobile)  ──►  Features  ──►  Kernel
Windows host                  ──►  Adapters  ──►  Kernel
```

- `Features` never references `Adapters` or a host.
- `Adapters` never references `Features`.
- `Kernel` references no project.
- Only the Windows host loads `Adapters`. The Web host provides simulated services.

### 2. Anatomy of a slice

A slice is a folder `Features/<Area>/<Slice>/` that holds everything one user-visible feature needs,
usually one user story or a small group of related stories.

| Element | Example | Rule |
| --- | --- | --- |
| Razor components | `WakeWordSettings.razor` | CSS and JavaScript in `.razor.css` and `.razor.js`, next to it |
| Request, handler, result | `DetectWakeWord.cs` | The logic of the feature lives here |
| Options | `WakeWordOptions.cs` | The slice's configuration section ([ADR 0004](0004-configuration-storage-and-secret-vault.md)) |
| Texts | `WakeWord.resx`, `WakeWord.fr.resx` | [ADR 0008](0008-languages-and-localization.md) |
| Registration | one extension method per slice | The only entry point known from outside |
| Tests | `tests/Kazo.Jarvis.Tests.Unit/Features/<Area>/<Slice>/` | Same path as the slice |

- The namespace follows the folder: `Kazo.Jarvis.Features.<Area>.<Slice>`.
- An area (`Voice`, `Orb`, `Handoff`…) is only a folder for grouping. It holds no code shared between its
  slices.

### 3. Dependency rules

1. **A slice never references another slice.** Slices talk through messages defined in the kernel.
2. **A slice depends on the kernel and on contracts, never on a concrete adapter.**
3. **Prefer a little duplication between two slices over an early shared abstraction.** What serves three
   slices or more moves to the kernel.
4. **The kernel holds no business logic, no UI component and no native dependency.**

Clarification added by this ADR: a UI component cannot move to the kernel. A presentational component used
by three slices or more (a secret field, a settings page frame) goes to `Features/Shared/`. That folder is
not a slice: it holds no business logic, depends only on the kernel, and any slice may use it.

### 4. Contents of the kernel

| Allowed | Forbidden |
| --- | --- |
| Messages between slices | Logic of a feature |
| Assistant state machine (ADR 0005) | Razor component, CSS, JavaScript |
| Contracts of native and external services (ADR 0003) | Type of a concrete technology (WPF, Whisper, Ollama, NAudio) |
| Configuration, storage and vault contracts (ADR 0004) | Access to the disk, the network or hardware |
| Small pure types: identifiers, results, errors | Any package other than the `Microsoft.Extensions.*` abstractions |

The state machine is the only coordination rule admitted in the kernel: it is a pure transition table, with
no input or output.

### 5. Messages between slices

- Two kinds of messages, all immutable records with no behavior:
  - an **event** describes a past fact (`WakeWordDetected`); it has zero, one or several subscribers;
  - a **request** expects an answer (`IRequest<TResult>`); it has exactly one handler.
- The kernel provides a small in-memory bus, typed and asynchronous. **No external mediator library** is
  used: the need fits in a few dozen lines, like Kazo Bar's `EventAggregator`.
- A failing subscriber does not stop the others from receiving the event. The error is logged.
- Messages are grouped by area in the kernel (`Kernel/Messages/<Area>/`).
- High-rate streams (audio samples, sound level) do not go through the bus. They have their own contract
  (ADR 0005 and [ADR 0006](0006-voice-chain.md)).
- A message carries no UI type and no adapter type.

### 6. Composition by the hosts

- Each slice exposes one registration method. `Features` exposes one method that calls them all.
- A host first registers the implementations of the contracts (real adapters or simulated services), then
  the slices. It only holds start-up and platform code.

### 7. Checked by tests

The rules are checked by architecture tests in `Kazo.Jarvis.Tests.Unit`, written with the first projects:

- no type of a slice uses a type of another slice (`Features.Shared` excepted);
- `Features` references neither `Adapters` nor a host; `Adapters` does not reference `Features`; `Kernel`
  references no project and no forbidden package;
- `Kernel` holds no Razor component;
- no `.razor` file contains `<style>`, `<script>` or `style="…"`.

These tests are part of the merge bar. None is disabled to get green.

### 8. Slice tests

- Each slice has its unit tests, with simulated contracts. No real microphone, model or network.
- Visible flows are tested by Playwright against the Web host.

## Consequences

- Removing a feature means removing its folder, its registration and its tests.
- The kernel grows with the messages. This is intended: the list of messages is the map of the exchanges
  between features.
- A little code will be duplicated between two slices. It is only factored out at the third use.
- A slice cannot call another one directly "to go faster". The architecture test fails, and the message has
  to be defined.
- An adapter is replaced without touching the slices, and a slice is tested without an adapter.
