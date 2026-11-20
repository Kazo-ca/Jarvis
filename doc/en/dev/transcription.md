# Transcription

Work item `stage1/10-transcription` (US1.7, ADR [0006](adr/0006-voice-chain.md), sections 3 and 4): the end of
the utterance and recognition by Whisper. Français : [../../fr/dev/transcription.md](../../fr/dev/transcription.md).

## What is built

```
TurnStarted (voice) ─► UtteranceListener ─► voice detector (Silero or energy threshold)
                             │                    ─► UtteranceEndpointer: 500 ms of silence?
                             │
                             ├─► NothingHeard            (no speech before the timeout: back to idle)
                             └─► UtteranceEnded ─► RecognizerKeeper (Whisper) ─► UtteranceTranscribed ─► conversation (work item 13)
```

### Kernel (`Kernel/Voice`, `Kernel/Messages/Voice`)

| Type | Role |
| --- | --- |
| `ISpeechRecognition`, `ISpeechRecognizer` | Native contract: load a Whisper model (`RecognizerSpec`: model and device), then transcribe a 16 kHz mono utterance in the language of the assistant. The model says where it really runs (`Device`): the processor when CUDA could not load. |
| `IVoiceActivityDetection`, `IVoiceActivityDetector` | Native contract: say, frame by frame, whether someone speaks (Silero, 512 samples, 32 ms). |
| `SpeechRecognitionException` | Named cause: model missing (with the expected path), load refused, transcription failed. |
| `UtteranceTranscribed` | What the user said, for the conversation: turn, text, language, audio length, transcription time. Empty text when Whisper recognized no words. |

### Slice `Features/Voice/Transcription`

- **`UtteranceListener`** receives `TurnStarted` (spoken turns only) and subscribes to the microphone stream
  (`IMicrophoneSource`). The capture thread only copies the blocks into a channel; the detector and Whisper run
  on a pool thread.
  - **`UtteranceEndpointer`** counts in samples, not clock time: the same voice always gives the same decision.
    Speech starts after 150 ms of voice without a pause (a click or a cough does not count), the utterance ends
    after the configured silence (500 ms), it is cut after 25 s, and without speech after 8 s, `NothingHeard`
    goes back to idle.
  - The transcribed audio runs from 300 ms before the start of speech (detectors answer a little late) to the
    end of the silence. Nothing before the activation: "Hey Jarvis" is not transcribed.
  - Without any sample for 3 s (microphone unplugged), listening stops: what was said is transcribed, otherwise
    nothing was heard.
  - A `TurnCancelled` (hotkey, click, microphone muted, interruption during the transcription) stops everything
    and nothing is published.
- **Detector:** Silero by default. When the host provides none or the model is missing, the **energy
  threshold** (`EnergyVoiceActivityDetector`, the fallback of ADR 0006) takes over and the log says so once. It
  compares each 30 ms frame to the background noise, the lowest level of the last 3 seconds (−55 dB assumed at
  first), with a 12 dB margin and −50 dB at least.
- **`RecognizerKeeper`** loads the model at start, reloads it when the model or the device changes (the old one
  is freed first: the graphics card has only about 600 MiB to spare), and looks again for a missing model
  before each transcription: the file can be dropped in without a restart. One transcription at a time.
- **`TranscriptText`** removes Whisper's markers (`[BLANK_AUDIO]`, `(music)`) and the segments whose "no speech"
  probability is above 0.8 (Whisper readily invents a sentence on noise).
- **Failures:** Whisper model missing or refused → `TurnFailed` with cause `ModelMissing` and the expected path;
  the orb shows the error until the next successful turn.
- **Settings page "Transcription"** (`Voice.Transcription`, after the "Audio" page): model and device with the
  loading state, detector, end-of-utterance silence, wait for speech, and the **last request heard** with its
  measures (audio length, transcription time, detector). It is the way to try the microphone and the model
  before work item 13.
- Settings: section `Voice.Transcription` of `config.json` (`model`, `device`, `detector`,
  `silenceMilliseconds`, `noSpeechTimeoutSeconds`, `maxUtteranceSeconds`).

### Adapters (`Kazo.Jarvis.Adapters/Voice`)

- `WhisperSpeechRecognition`: Whisper.net 1.9.1, CUDA native library first, then the processor (as in spike
  E4). The `Whisper.net.Runtime.Cuda` package is added by the Windows host only; `Adapters` brings the processor
  one. The language is fixed when the Whisper processor is built, rebuilt when it changes.
- `SileroVoiceActivityDetection`: sherpa-onnx 1.13.8 (already pinned for speech). Silero follows the voice
  closely (`MinSilenceDuration` and `MinSpeechDuration` at 0.1 s); the slice counts the silence.

### Models

No model in the repository (ADR 0006, section 7). The first-run wizard (work item 14) will download them; until
then they are copied by hand into the models folder:

| File | Where | Source |
| --- | --- | --- |
| `ggml-large-v3-turbo-q5_0.bin`, `ggml-small.bin`, `ggml-base.bin` | `%USERPROFILE%\.kazo\jarvis\models\whisper\` | `ggerganov/whisper.cpp` on Hugging Face |
| `silero_vad.onnx` | `%USERPROFILE%\.kazo\jarvis\models\vad\` | `asr-models` release of `k2-fsa/sherpa-onnx` on GitHub |

### Web host

`SimulatedSpeechRecognition` ("What time is it?" or "Quelle heure est-il ?" depending on the language, in
200 ms) and `SimulatedVoiceActivity` in place of Silero: the simulated microphone plays a tone all the time, so
the "Say a phrase" button "speaks" for 1.2 s. The last transcription shows on the page.

## Provisional choices of this work item

ADR 0006 does not settle them; to be confirmed by Benoit:

1. **8 s wait for speech** after the activation, then back to idle.
2. **Longest utterance: 25 s**, cut then transcribed (the microphone buffer keeps 30 s).
3. **150 ms of voice** before speech counts as started.
4. **300 ms kept before speech**, nothing before the activation.
5. **Silero missing or unreadable: silent fallback to the energy threshold** (log only, no fault).
6. **Whisper model missing: no fault at start**, only at the end of a turn (`TurnFailed`, `ModelMissing`). The
   state shows on the page.
7. **Empty text published as is** when Whisper recognizes no words: the conversation (work item 13) then ends
   the turn without an answer. Until then Jarvis stays "thinking" after each utterance; the hotkey interrupts it.
8. Segments dropped above **0.8** "no speech" probability.
9. Models in `models\whisper\` and `models\vad\`.

## What was tried here

- Real Silero (sherpa-onnx 1.13.8 on Linux) on whisper.cpp's `jfk.wav` recording, padded with light noise:
  speech start found at 1.18 s, 0.17 ms per frame. **With 500 ms, the utterance ended at the speaker's first
  natural pause (3.9 s)**: to watch with Benoit's voice; the setting goes up to 3 s. The energy threshold did
  not find those pauses (the recording's noise is above its threshold): Silero stays the right default.
- Real Whisper could not run in the container (Hugging Face unreachable): it is covered by the Windows checks.

## Tests

- Unit: `UtteranceEndpointerTests` (silence, pause, click, timeout, cut), `TranscriptionPiecesTests` (energy
  threshold, text cleaning, settings), `RecognizerKeeperTests` (single load, change, missing then found, CUDA
  unavailable), `UtteranceListenerTests` (full turn with the real state machine, nothing heard, cancel,
  interruption during the transcription, fallback without Silero, missing model, typed turn),
  `VoiceAdaptersTests` (model paths, named causes, registration).
- Playwright (`TranscriptionTests`): hotkey, simulated phrase, "thinking", text shown on the page and in the
  settings.

## Windows checks

For the next session with Benoit (him only for his voice):

1. **Models in place** (table above): the "Transcription" page shows "Model loaded, running on: Graphics card
   (CUDA)". Note the load time in the log.
2. **End of utterance with a real microphone and some noise** (TV, fan): `Ctrl+Alt+J`, a short request, a long
   one with pauses. The utterance must not be cut in the middle nor wait too long. Adjust the silence if needed;
   **if Silero is not reliable, try "Energy threshold" and write it down** (ADR 0006, section 3).
3. **Nothing said after the activation**: back to idle after 8 s.
4. **Recognized text** in French then in English (change the language); note the transcription time shown
   (spike E4 target: 0.43 to 0.68 s with `large-v3-turbo`).
5. **Fallback:** `small` on the graphics card, `base` on the processor; the device shown follows.
6. **Interruption** during the transcription (hotkey): no trace of the old turn's text in the log.
