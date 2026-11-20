# ADR 0016: a single native ONNX Runtime, the one from sherpa-onnx

## Status

Proposed on 2026-11-20 (work item `stage1/onnx-runtime-adr`). Waiting for Benoit's answer.

- **Partly supersedes:** [ADR 0001](0001-target-dotnet-10-and-projects.md), section 4, for the single
  `Microsoft.ML.OnnxRuntime` 1.32.0 line of the versions table. The rest of ADR 0001 does not change.
- **Describes code already merged:** work item `stage1/9-wake-word` had to deviate from ADR 0001 so that
  Piper kept loading. This ADR regularizes that deviation; it asks for no code change.

## Context

- ADR 0001 picks `Microsoft.ML.OnnxRuntime` 1.32.0 for the wake word and `org.k2fsa.sherpa.onnx` 1.13.8
  for Piper, Kokoro and Silero. Spike E4 proved each of them in its own program, never together.
- Both packages ship a native library with the same name (`onnxruntime.dll`, `libonnxruntime.so`): 1.32.0
  for Microsoft's package, 1.28.2 for sherpa-onnx. In one output folder, one overwrites the other.
- Seen in work item `stage1/9-wake-word` on Linux: with 1.32, `libsherpa-onnx-c-api.so` no longer finds
  the `OrtGetApiBase@VERS_1.28.2` symbol and does not load. Piper, Kokoro and Silero are lost
  ([pitfalls](../pitfalls.md)).
- The other way round, the .NET part of ONNX Runtime refuses a native engine older than itself (`GetApi`
  returns null): keeping 1.32 on the .NET side over the 1.28.2 engine does not work either.
- sherpa-onnx cannot move up: 2.x removed espeak-ng, which the French voices need (ADR 0001, ADR 0006).

## Decision

1. **A single native ONNX Runtime in the process: the one `org.k2fsa.sherpa.onnx` ships** (1.28.2 with
   version 1.13.8).
2. **The wake word references `Microsoft.ML.OnnxRuntime.Managed`**, the .NET part alone, with no native
   library. The `Microsoft.ML.OnnxRuntime` package is no longer referenced by any project in `src/` or
   `tests/`.
3. **Version: 1.28.0.** The rule is that the version of `Microsoft.ML.OnnxRuntime.Managed` stays **equal to
   or lower than** that of sherpa-onnx's native engine, within the same minor version.
4. **Both versions move together.** Changing the sherpa-onnx version means reading its native engine
   version again and adjusting `Microsoft.ML.OnnxRuntime.Managed` in the same change. The comment in
   `Directory.Packages.props` says so.
5. The line of the ADR 0001 table now reads:

| Package | Version | Note |
| --- | --- | --- |
| `Microsoft.ML.OnnxRuntime.Managed` | 1.28.0 | Wake word; native engine 1.28.2 provided by sherpa-onnx 1.13.8 |

## Options set aside

- **Keep `Microsoft.ML.OnnxRuntime` 1.32.0 and isolate the two native libraries** (separate folders, loaded
  by hand through `NativeLibrary`). Possible, but fragile: two engines in the same process, a different
  loading path on Windows and on Linux, and a second native library to publish.
- **Run the wake word through sherpa-onnx** (its keyword spotter). Those are not the openWakeWord models
  that ADR 0006 picks and spike E4 measured; everything would have to be measured again.
- **Put the wake word in a separate process.** Too heavy for a model of a few megabytes that listens all
  the time.

## Consequences

- Piper, Kokoro, Silero and the wake word run in the same process, on Windows as on Linux (continuous
  integration). Checked in work item 9: Piper then the wake word in the same process,
  "Hey Jarvis." spoken by Piper scores 0.993.
- The wake word does not get what ONNX Runtime 1.29 to 1.32 added. The three openWakeWord 0.5.1 models do
  not need it: they run on the processor with old operators.
- The wake word depends on the native library of another package. If sherpa-onnx ever leaves the project,
  `Microsoft.ML.OnnxRuntime` (with its native part) comes back, through a new ADR.
- **Still to check on Windows** (session with Benoit, [list](../wake-word.md#windows-checks)): that the
  published host holds a single `onnxruntime.dll` and that the wake word and Piper both load from it.
