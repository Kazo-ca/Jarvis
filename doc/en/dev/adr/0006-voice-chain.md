# ADR 0006: voice chain (wake word, recognition, synthesis)

## Status

Accepted by Benoit on 2026-11-19 (proposed on 2026-11-18, work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 15, 16, 17 and 18 of the [design](../plans/design.md); stories US1.5, US1.7, US1.9
  to US1.13; spikes E4 and E5 ([results](../../../../spikes/e4-voice-chain/RESULTS.md)); decisions 3 and 4
  of the [spikes](../spikes.md#decisions-taken-on-november-18-2026) (Piper by default, wake word to be tuned
  with Benoit's voice).
- **Completes:** [ADR 0003](0003-hosts-and-native-services.md) (audio capture and playback),
  [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (phases and interruption).
- **Completed by:** [ADR 0007](0007-llm-providers-and-tool-format.md) (local model),
  [ADR 0008](0008-languages-and-localization.md) (language of recognition and voice).

## Context

- Spike E4 ran the whole chain in C#, without Python: openWakeWord wake word ported to C#, Whisper.net,
  local model, Piper and Kokoro through sherpa-onnx.
- Delay measured between the end of the utterance and the first sound: **1.6 to 1.8 s** warm, with Whisper
  large-v3-turbo on the graphics card, qwen3 14B and Piper. The 2 s target is met.
- Two reservations: a "hey jarvis" pronounced the French way by a synthetic voice is not recognized (score
  of 0.04 to 0.16, against 0.997 in English), and Kokoro on the processor adds 1.3 s.
- Spike E5: Whisper turbo and the 14B model fit together on the RTX 3060, with 11.4 to 11.7 GB used out of
  12.3. On the processor, only Whisper `base` stays bearable (1.05 to 1.1 s of transcription).
- The spike used neither a real microphone nor a speaker: the utterances were produced by synthesis.

## Decision

### 1. Overview

```
microphone ─► ring buffer ─► wake word ─► end of utterance ─► recognition ─► model (ADR 0007)
                                                                                  │ sentence by sentence
speaker ◄─ playback ◄─ synthesis ◄────────────────────────────────────────────────┘
```

- Each link is a kernel contract, with a real adapter and a simulated one. The voice slices follow one
  another through the messages of ADR 0005: none references another.
- Capture at 16 kHz mono, the format expected by the wake word and by Whisper.
- **The listening buffer stays in memory and is never written to disk.** When the microphone is muted
  (US1.13), the capture stream is closed: no sound is captured.

### 2. Wake word

- **openWakeWord "hey_jarvis_v0.1" model**, with its two upstream models (spectrogram and embeddings), run
  by ONNX Runtime on the processor. Measured cost: 1.3 to 2 % of real time on one core.
- Default threshold 0.5, adjustable. An indicator shows the live score (US1.5).
- **The threshold is tuned with Benoit's voice during stage 1.** If his pronunciation does not pass, the
  threshold is lowered. If that is not enough, training a model is a separate decision, through a new ADR;
  the hotkey and the click stay usable in the meantime (US1.6).
- One detection per call: the detector is silent while listening.
- The same wake word is used in French and in English (assumption 9 of the design).

### 3. End of utterance

- The end of the utterance is detected by silence (US1.7), with a Silero voice activity detector run by
  sherpa-onnx, already present for synthesis.
- Starting values, adjustable: 500 ms of silence end the utterance; with no speech after the activation,
  listening stops after a timeout and goes back to idle.
- **Not proven by spike E4**, which assumed 500 ms without a detector. It is the first check of stage 1 with
  a real microphone. Fallback: a simple energy threshold.

### 4. Recognition

- **Local Whisper through Whisper.net**, with the CUDA runtime when an NVIDIA card is present.
- Model and device are adjustable (US1.7):

| Model | Device | Measured transcription | Use |
| --- | --- | --- | --- |
| `large-v3-turbo` (q5) | Graphics card | 0.43 to 0.68 s | **Default** |
| `small` | Graphics card | 0.24 to 0.5 s | When graphics memory is short |
| `base` | Processor | 1.05 to 1.1 s | Fallback without an NVIDIA card; the total delay becomes 2.2 to 2.8 s |

- `small` and `large-v3-turbo` on the processor are not offered (3.7 s and 15 s).
- The language is set by the language setting, without automatic detection (ADR 0008).
- The model is loaded once at start-up and kept in memory.

### 5. Synthesis

- **Piper by default**, through sherpa-onnx **1.13.8**: `fr_FR-tom-medium` in French, `en_GB-alan-medium` in
  English. A 3.5 s sentence is produced in 0.16 to 0.40 s on the processor.
- **Kokoro as a "quality" option** (`ff_siwis`, `bm_george`) as long as it does not run on the graphics
  card: on the processor, it takes 1.6 to 1.7 s per sentence and misses the target.
- **Cloud voices** (ElevenLabs, OpenAI): adapters behind the same contract, listed only when a key is in the
  vault (US1.10). Choosing a cloud voice sends the text of the answers off the machine; the selection
  screen says so.
- The model's answer is split into sentences. Each sentence is synthesized then played in order, and
  synthesis starts at the first complete sentence (US1.9).
- **Interruption** (US1.12): playback stops at once and the waiting sentences are dropped.
- The voice test page (US1.11) uses the same contract as the conversation.

### 6. Delay and memory budget

- Target: **under 2 s** between the end of the utterance and the first sound, warm, with the default
  configuration. It is measured again with the real microphone and speaker.
- The first cold call takes 70 to 115 s, the time to load the local model. Preloading is described in
  ADR 0007.
- Graphics memory: about 600 MiB remain with the default configuration on a 12 GB card. The setup wizard
  offers Whisper `small` and a smaller local model as a fallback (ADR 0007).

### 7. Models

- No model is in the repository or in the install package. The setup wizard downloads them to the models
  folder ([ADR 0004](0004-configuration-storage-and-secret-vault.md)), shows their size and checks their
  hash.

| Model | Source |
| --- | --- |
| `melspectrogram.onnx`, `embedding_model.onnx`, `hey_jarvis_v0.1.onnx` | openWakeWord release 0.5.1 on GitHub |
| `ggml-large-v3-turbo-q5_0.bin`, `ggml-small.bin`, `ggml-base.bin` | `ggerganov/whisper.cpp` on Hugging Face |
| `vits-piper-fr_FR-tom-medium`, `vits-piper-en_GB-alan-medium`, `kokoro-multi-lang-v1_0` | sherpa-onnx synthesis models on GitHub |

## Consequences

- The default chain meets the target on Benoit's PC. Without an NVIDIA card it works, but slower than the
  target.
- sherpa-onnx stays on 1.13.8. Version 2.0 removed espeak-ng, which Piper and Kokoro need outside English.
- **To check at the start of stage 1,** because the spike did not: the wake word with Benoit's voice, the
  real microphone and ambient noise, the end of utterance through Silero, the real delay of the sound card,
  the perceived quality of the voices, and the wake word while Jarvis speaks.
- **Licenses to settle before any public distribution** (assumption 7 of the design). Nothing blocks
  personal use.
  - espeak-ng, used by Piper and Kokoro in sherpa-onnx 1.13.8, is under GPLv3.
  - The pre-trained openWakeWord models are under CC BY-NC-SA 4.0 (non-commercial use), according to their
    Hugging Face model card read on 2026-11-18.
  - Each voice has its own license.
- Shipping no model with the application avoids redistributing these files, at the price of a download on
  first run.
