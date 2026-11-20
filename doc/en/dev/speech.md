# Speech and voices page

Work item `stage1/12-speech` (US1.9 to US1.12, ADR [0006](adr/0006-voice-chain.md), section 5, and
[0009](adr/0009-permission-and-confirmation-model.md), section 9): Jarvis says its answers aloud, and the
"Voices" settings page lets the user listen to and choose the voice of each language.
Français : [../../fr/dev/speech.md](../../fr/dev/speech.md).

## What is built

```
conversation (work item 13) ─► SentenceToSpeak, AnswerSentencesEnded ─► SpeechPlayer (slice Voice/Speech)
                                                                          │ IVoiceSelection: voice of the language
                                                                          ▼
                                     ISpeechEngine (Piper, Kokoro, ElevenLabs, OpenAI) ─► AudioClip
                                                                          │
               FirstSentenceReady, SpeechEnded ◄──────────────────────────┤
                                     IAudioPlayback (output of the Audio page) ◄─┤
                                     ISoundLevelSink (voice) ─► orb ◄─────────────┘
"Voices" page (slice Voice/TestVoice) ─► IVoiceSelection + ISpeechEngine + IAudioPlayback (same contract)
```

### Kernel (`Kernel/Speech`, `Kernel/Messages/Speech`)

| Type | Role |
| --- | --- |
| `ISpeechEngine` | Native contract of an engine: its name (`piper`, `kokoro`, `elevenlabs`, `openai`), whether the text leaves the PC, its voices usable now, the synthesis of one sentence into an `AudioClip`. Several engines are registered. |
| `SpeechVoice`, `VoiceChoice` | A voice (engine, identifier, name, language or `null` when multilingual, leaves the PC); the choice kept in the settings (engine, voice, speed from 0.5 to 2). |
| `SpeechException`, `SpeechFailure` | Why a voice failed: model missing, key missing or refused, service unreachable, unknown voice, other. |
| `IVoiceSelection` | The voice of each language, given by the `Voice/Speech` slice which owns the setting; read and changed by the "Voices" page, later by the first run (work item 14). |
| `SpeechKeys` | References of the voice keys in the vault: `voice.elevenlabs.apikey`, `voice.openai.apikey`. |
| `SentenceToSpeak`, `AnswerSentencesEnded` | What the conversation will publish: each complete sentence in order, then the end of the answer. |

### Slice `Features/Voice/Speech`

- **`SpeechPlayer`** receives the sentences of a turn, synthesizes and plays them in order; the next sentence is
  synthesized while the previous one plays (one ahead). The voice is fixed at the first sentence of the turn,
  from the language of the assistant.
  - First sound: `FirstSentenceReady` (the orb turns to "speaking"). Last sound after `AnswerSentencesEnded`:
    `SpeechEnded`. Answer without a sentence: `AnswerFinishedWithoutSpeech`.
  - **Interruption** (`TurnCancelled`, US1.12): the sound stops at once (`IAudioPlayback.StopAll`), waiting
    sentences are dropped, the level falls to zero. Late messages of the turn are ignored.
  - **Failure**: `TurnFailed` with the `ModelMissing` fault (model files missing, no voice for the language)
    or `VoiceMissing` (key refused, service unreachable, no audio output).
  - **Voice level to the orb**: the clip's envelope is computed before it plays (`VoiceEnvelope`, one value
    per 1/30 s, in decibels from −60 to 0 like the microphone), then sent at the pace of the sound.
- **`VoiceSelection`** (`IVoiceSelection`): setting `Voice.Speech` of `config.json` (voice and speed per
  language). Voice used: the chosen one if it is there; otherwise the default Piper voice; otherwise a local
  voice of the language. **Never a cloud voice the user did not choose** (ADR 0009, section 9).
- Default voices (ADR 0006): `piper:fr_FR-tom-medium` and `piper:en_GB-alan-medium`, speed 1.

### Slice `Features/Voice/TestVoice`

- **"Voices" settings page** (`Voice.TestVoice`, after "Audio"): language (French or English), voices of the
  language grouped "On this computer" and "In the cloud", editable sample sentence (one per language), speed
  from 0.5 to 2, "Listen", "Stop", "Use for French" buttons.
- A cloud voice shows that **the text of every answer is sent** to the service (ADR 0009, section 9). The
  ElevenLabs and OpenAI keys are typed on the same page (shared secret field); their voices appear as soon as
  the key is saved.
- The logic is in `VoiceTrial` (C# first); "Stop" only cuts the page's sentence, not Jarvis's voice.

### Adapters (`Kazo.Jarvis.Adapters/Speech`)

| Engine | What it reads | Notes |
| --- | --- | --- |
| `PiperSpeechEngine` | Every complete `models\vits-piper-*` folder (`.onnx`, `tokens.txt`, `espeak-ng-data`) | sherpa-onnx 1.13.8, 4 threads. Any Piper voice put there is listed, named after its file (`fr_FR-tom-medium` gives "Tom (fr_FR, medium)"). |
| `KokoroSpeechEngine` | `models\kokoro-multi-lang-v1_0` | `ff_siwis` in French, a few English voices (`bm_george`, `bm_lewis`, `bm_daniel`, `bf_emma`, `am_michael`, `af_heart`). One model loaded per language. |
| `ElevenLabsSpeechEngine` | Key `voice.elevenlabs.apikey` | Voices of the account (`GET /v1/voices`, read again every 10 min), model `eleven_flash_v2_5`, raw 16-bit sound at 24 kHz, speed brought between 0.7 and 1.2. |
| `OpenAiSpeechEngine` | Key `voice.openai.apikey` | `gpt-4o-mini-tts`, 11 fixed voices, raw 16-bit sound at 24 kHz. |

- A local model is loaded at its first sentence and kept in memory; its generations follow each other (one
  sentence at a time per model).
- Cloud voices are multilingual and only listed with a key in the vault. One sentence is bounded to 30 s.

### Web host

`SimulatedSpeechEngine` poses as Piper (`fr_FR-tom-medium`, `fr_FR-siwis-medium`, `en_GB-alan-medium`) and
produces a hum whose length follows the text; `SimulatedCloudSpeechEngine` lists "Simulated Nova" when the
OpenAI voice key is in the vault. The home page has a "Simulate a spoken answer" button, which plays a turn as
the conversation will, and shows the last sentence said (`#voice-spoken`).

## Real run in the container

Piper `fr_FR-tom-medium` was loaded and spoken by `PiperSpeechEngine` on Linux on 2026-11-20 (model downloaded
outside the repository): 3.4 s of sound at 44.1 kHz in 1.3 s for the first sentence (loading included), 0.47 s
afterwards, 2.6 s of sound at speed 1.5. Neither Kokoro nor the cloud voices were tried for real.

## Provisional choices of this work item

ADR 0006 does not settle them; to be confirmed by Benoit:

1. **The first sound turns the orb to "speaking"**: the voice slice publishes `FirstSentenceReady`, not the
   conversation.
2. **One voice per turn**, chosen at the first sentence: a settings change applies from the next turn.
3. **A failing cloud voice** (key refused, no network) **fails the turn** with the "voice missing" fault;
   Jarvis does not switch to a local voice by itself in the middle of an answer.
4. **Chosen voice missing** (model deleted, key removed): the default Piper voice, or another local voice of
   the language, speaks instead, with a line in the log.
5. **Speed per language**, saved with the voice, from 0.5 to 2 (ElevenLabs accepts 0.7 to 1.2 only).
6. **Separate OpenAI voice key**, apart from the one of an OpenAI model provider.
7. ElevenLabs with `eleven_flash_v2_5` (the fastest); OpenAI with `gpt-4o-mini-tts`.
8. English Kokoro voices offered: six voices of the model, with the American pronunciation of spike E4.
9. Sample sentences: "Bonsoir, Monsieur. Tous les systèmes sont opérationnels." and "Good evening, sir. All
   systems are operational."

## Tests

- Unit: `SpeechPlayerTests` (order, first sound, end, interruption, failures, old turns, level, with the real
  state machine), `VoiceSelectionTests`, `SpeechOptionsTests`, `VoiceEnvelopeTests`, `VoiceTrialTests` (page),
  `LocalSpeechEnginesTests` (model folders), `CloudSpeechEnginesTests` (requests, raw sound, causes of failure,
  no network).
- Playwright (`VoicesTests`): "Voices" page in both languages, default voice saved, cloud voice and its
  warning, spoken answer, interruption.

## Windows checks

To do at the next session with Benoit (**his ears**; the Web host does not prove them). Models go into
`%USERPROFILE%\.kazo\jarvis\models` (sources in [ADR 0006](adr/0006-voice-chain.md), section 7):

1. **Perceived quality** of the Piper voices `fr_FR-tom-medium` and `en_GB-alan-medium` on the real speakers,
   then on the headset; Kokoro `ff_siwis` and `bm_george` if installed; an ElevenLabs or OpenAI voice if a key
   is given.
2. **Speed**: 0.8, 1 and 1.3, keep the one he prefers for each language.
3. **Sound card delay**: between the click on "Listen" and the first sound, warm (second try). Write the
   figure in the memory.
4. **Clean stop**: "Stop" during a long sentence cuts the sound without a click.
5. The page shows no voice for a language whose model is missing, and says so.
