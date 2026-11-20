# Wake word

Work item `stage1/9-wake-word` (US1.5, ADR [0006](adr/0006-voice-chain.md), section 2): "Hey Jarvis" wakes
Jarvis, with a threshold to set and the live score in the settings.
Français : [../../fr/dev/wake-word.md](../../fr/dev/wake-word.md).

## What is built

```
microphone ─► MicrophoneFeed (kernel) ─► WakeWordListener (slice) ─► IWakeWordDetector (host) ─► score
                                                 │ score ≥ threshold
                                                 └─► ActivationRequested(WakeWord) ─► state machine (listening)
```

### Kernel (`Kernel/Voice`)

| Type | Role |
| --- | --- |
| `IWakeWordEngine` | Native contract: loads the models and gives a detector. Throws `WakeWordModelMissingException` (with the list of files) when a model is missing. |
| `IWakeWordDetector` | Scores the stream frame by frame (`FrameLength` = 1,280 samples, 80 ms at 16 kHz): a 0 to 1 score for "hey jarvis" ending now. `Reset` forgets what was heard. One detector per stream, not thread safe. |

### Adapter (`Adapters/Voice`)

- **`OnnxWakeWordEngine`**: the three openWakeWord 0.5.1 models (`melspectrogram.onnx`,
  `embedding_model.onnx`, `hey_jarvis_v0.1.onnx`) on ONNX Runtime 1.32 on the processor, one thread each.
  Folder: `%USERPROFILE%\.kazo\jarvis\models\wake-word` (ADR 0004, section 5).
- **`OnnxWakeWordDetector`**: streaming port of the openWakeWord behavior (not its code), taken from spike
  E4: each 80 ms frame adds its mel frames (with 480 samples of context), one 96-value embedding of the last
  76 mel frames, then the classifier scores the last 16 embeddings (about 1.3 s). At start and after
  `Reset`, the buffers are filled as in openWakeWord (mel frames of ones, embeddings of quiet noise with a
  fixed seed) and the first five scores are 0.
- Measured in the CI container (4 threads, no GPU): **3.4 % of one core** for 60 s of sound. Spike E4
  measured 1.3 to 2 % on Windows.

### Slice `Features/Voice/WakeWord`

- **`WakeWordListener`** (background service):
  - subscribes to the microphone stream; the capture thread only copies the blocks into a queue, the models
    run on the service's own task; blocks (50 ms on Windows) are cut again into 80 ms frames;
  - publishes `ActivationRequested(WakeWord)` when the score reaches the threshold;
  - **one detection per activation**: after a detection, the score must fall below the threshold before
    another one counts;
  - **silent while listening** and while the microphone is muted: no frame is scored, and the detector
    starts afresh after (the end of the request cannot wake it). It scores while thinking and speaking:
    "Hey Jarvis" then interrupts Jarvis (US1.12, ADR 0005);
  - **models missing**: not a fault of the assistant (the hotkey and the click work, US1.6), a warning in the
    log, the "models missing" state on the page, and another try every 30 s;
  - exposes the state, the latest score and the last detection for the page.
- **"Wake word" settings page** (`Voice.WakeWord`, after "Audio"): live score with the threshold, best score
  of the last 3 seconds, threshold slider (0.05 to 0.95 in steps of 0.05, applied at once), last detection
  (time and score), state (active, paused, microphone muted, models missing with the expected files, failed).
- Settings: section `Voice.WakeWord` of `config.json` (`threshold`, 0.5 by default; a value out of range is
  brought back within it).

### Web host

`SimulatedWakeWordEngine`: a 0.02 score on every frame of the simulated microphone; the "Say “Hey Jarvis”"
button of the home page makes the next frames rise (0.2, 0.6, 0.93, 0.97, 0.7, 0.3).

## Provisional choices of this work item

ADR 0006 does not settle them; to be confirmed by Benoit:

1. **Models missing = no fault**: the orb does not turn to error, the page says so, another try every 30 s.
   The first-run wizard (work item 14) will download them.
2. Folder `models\wake-word\` with the openWakeWord file names.
3. One detection per crossing of the threshold, with no extra delay.
4. The wake word stays active while thinking and speaking (to interrupt), paused only while listening and
   while the microphone is muted.
5. Slider from 0.05 to 0.95 in steps of 0.05.
6. Best score of the last 3 seconds shown, easier to read than the live score to set the threshold.
7. A detector that fails while running stops until the next start ("failed" state, `Error` log) rather than
   failing every 80 ms.
8. Queue of about 6 s: if the models fall behind, the oldest sound is dropped (a late wake word is worse than
   a missed one).

## Tests

- Unit: `WakeWordListenerTests` (threshold, one detection per activation, pause while listening and muted,
  blocks cut into frames, models missing then found, with the real state machine), `OnnxWakeWordEngineTests`
  (category `Models`: missing files named, silence, noise and a pure tone under 0.2, first five scores at 0,
  same result after `Reset`). The CI downloads the models and checks their hash
  ([build](build.md#models-for-the-tests)).
- Explicit test, run by hand: a Piper (`en_GB-alan-medium`) "Hey Jarvis." scores **0.999**; "Hey Travis,
  how are you today?" stays at **0.488**, just under 0.5 (0.127 in spike E4, which scored the whole clip at
  once). The clips are not in the repository (licence of the voice).
- Playwright (`WakeWordTests`): a simulated "Hey Jarvis" starts listening; the page shows the score, the
  state, the last detection and applies the threshold.

## Windows checks

To do at the next session with Benoit (his voice; the Web host does not prove them):

1. Copy the three models into `%USERPROFILE%\.kazo\jarvis\models\wake-word` (until work item 14), start
   Jarvis: the "Wake word" page shows "Jarvis is listening for “Hey Jarvis”".
2. **Threshold with Benoit's voice** (ADR 0006, section 2): say "Hey Jarvis" about ten times at a normal
   distance, then talk normally and let music or a video play for a few minutes. Note the best scores, choose
   the threshold, and **write it in the project memory**. If his pronunciation does not pass even with a
   lower threshold, stop and ask (training a model needs a new ADR).
3. "Hey Jarvis" while Jarvis speaks (after work item 12): Jarvis's own voice does not wake it, and Benoit's
   interrupts it.
4. Cost of the wake word measured on its own (method of spike E6), to note in the memory.
