# ADR 0001: .NET 10 target and list of projects

## Status

Proposed on 2026-11-18, waiting for Benoit's approval (work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 1, 3, 34 and 36 of the [design](../plans/design.md); spikes E4, E5 and E6
  ([summary](../spikes.md)).
- **Completed by:** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (dependency rules) and
  [ADR 0003](0003-hosts-and-native-services.md) (hosts).

## Context

- Kazo.Jarvis is a standalone C# application, with no Python at run time (decision 1).
- The spikes ran on the .NET SDK 10.0.402. The voice chain (E4) and the orb window (E6) work on it.
- Kazo Bar is the model for the hosts. Its Windows host requires the `net10.0-windows10.0.19041.0` target:
  with plain `net10.0-windows`, `WebView2CompositionControl` kills the process at start-up.
- The native libraries of the voice chain (Whisper.net with CUDA, sherpa-onnx, ONNX Runtime) were only
  tried on x64.
- One wrong package version can break everything: sherpa-onnx 2.0 removed espeak-ng, and the French voices
  stop working ([pitfalls](../pitfalls.md)).

## Decision

### 1. Platform

- **.NET 10** (long-term support release), latest C# version, nullable reference types enabled. The SDK is
  pinned by a `global.json` file (`rollForward: latestFeature`).
- **Windows 10 version 2004 (build 19041) or later, x64 only.** No x86, no ARM64. The spikes only ran on
  Windows 11.
- **Android** for the mobile app, at stage 4. No iOS.
- **No Python and no Node.js at run time.** The user's Python scripts (stage 3) use the user's own
  interpreter: Kazo.Jarvis neither installs one nor depends on one.

### 2. Layout

```
Kazo.Jarvis.slnx
global.json
Directory.Build.props        single product version, shared compiler options
Directory.Packages.props     package versions for src/ and tests/
src/                         the product projects
tests/                       the test projects
spikes/                      throwaway experiments, outside the solution
build/                       publishing scripts
doc/
```

### 3. Projects

| Project | Type | Target | References | Created at |
| --- | --- | --- | --- | --- |
| `src/Kazo.Jarvis.Kernel` | Class library | `net10.0` | no project | Stage 1 |
| `src/Kazo.Jarvis.Features` | Razor class library | `net10.0` | Kernel | Stage 1 |
| `src/Kazo.Jarvis.Adapters` | Class library | `net10.0` | Kernel | Stage 1 |
| `src/Kazo.Jarvis.Windows` | WPF executable with `BlazorWebView` | `net10.0-windows10.0.19041.0`, `win-x64` | Kernel, Features, Adapters | Stage 1 |
| `src/Kazo.Jarvis.Web` | Blazor web app | `net10.0` | Kernel, Features | Stage 1 |
| `src/Kazo.Jarvis.Updater` | Windows executable | `net10.0-windows` | no project | Stage 1 (US1.18) |
| `src/Kazo.Jarvis.Mobile` | MAUI Blazor Hybrid | `net10.0-android` | Kernel, Features | Stage 4 |
| `tests/Kazo.Jarvis.Tests.Unit` | NUnit | `net10.0` | Kernel, Features, Adapters | Stage 1 |
| `tests/Kazo.Jarvis.Tests.E2E` | NUnit and Playwright | `net10.0` | Web | Stage 1 |

- The role of each project and the direction of dependencies are set by ADR 0002. The hosts are described
  in ADR 0003.
- `Kazo.Jarvis.Mobile` is not created before stage 4: spike E10 is not done.
- `Kazo.Jarvis.Updater` only replaces the files and restarts the application. The version check and the
  download happen in the application (a slice and its adapter). The updater therefore references no other
  project and stays small.
- The experiments in `spikes/` are never referenced by a project of `src/` or `tests/`.

### 4. Build rules

- `Directory.Build.props` holds the single product version, compared with the versions published on GitHub.
- The package versions of `src/` and `tests/` are centralised in `Directory.Packages.props`. Spikes keep
  their own versions.
- Starting versions, the ones the spikes proved:

| Package | Version | Note |
| --- | --- | --- |
| `Whisper.net`, `Whisper.net.Runtime`, `Whisper.net.Runtime.Cuda` | 1.9.1 | E4 |
| `org.k2fsa.sherpa.onnx` | 1.13.8 | Do not move to 2.x without a new ADR (espeak-ng removed) |
| `Microsoft.ML.OnnxRuntime` | 1.32.0 | E4, wake word |
| `NAudio` | 3.3.0 | `Span<float>` interfaces |
| `Microsoft.AspNetCore.Components.WebView.Wpf` | 10.0.110 | Version checked in Kazo Bar |

- Tests: NUnit, as in Kazo Bar. The Playwright tests target the Web host.
- Publishing: self-contained (`--self-contained`), `win-x64`, the application and its updater in the same
  folder, as Kazo Bar does.

## Consequences

- Everything written for stage 1 builds with one SDK, on Benoit's PC and anywhere else.
- `Kazo.Jarvis.Kernel`, `Features`, `Adapters`, `Web` and the tests stay on `net10.0`: they compile and are
  tested without Windows. Only the Windows host and the updater need Windows.
- A laptop without an NVIDIA card or an ARM64 machine is not a target. The fallback without a graphics card
  is described in [ADR 0006](0006-voice-chain.md).
- Raising a package version of the voice chain is a decision, not a routine update: the measurements of
  spike E4 must be redone.
- While the repository is private, the version check on GitHub (US1.18) needs a token kept in the vault.
  Assumption 7 of the design (public repository) is still to be confirmed by Benoit.
