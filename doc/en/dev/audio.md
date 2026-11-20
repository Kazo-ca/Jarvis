# Audio devices

Work item `stage1/8-audio-devices` (US1.13, ADR [0003](adr/0003-hosts-and-native-services.md) and
[0006](adr/0006-voice-chain.md)): the microphone, the audio output and muting the microphone.
Français : [../../fr/dev/audio.md](../../fr/dev/audio.md).

## What is built

```
microphone ─► IAudioCapture (host) ─► MicrophoneService (slice) ─┬─► MicrophoneFeed (kernel) ─► wake word, transcription
                                                                 └─► ISoundLevelSink (kernel) ─► orb
OutputBinder (slice) ─► IAudioPlayback.SelectOutput (host) ◄─ voice, test page (work item 12)
```

### Kernel (`Kernel/Audio`, `Kernel/Messages/Audio`)

| Type | Role |
| --- | --- |
| `IAudioCapture` | Native contract: list the microphones, open a stream (`IAudioCaptureStream`), watch the devices. The stream delivers **16 kHz mono `float`** (`MicrophoneFormat`); closing it releases the device. |
| `IAudioPlayback` | Native contract: list the outputs, choose the output, play an `AudioClip` (mono, any rate), stop everything at once (US1.12). |
| `IMicrophoneSink` / `IMicrophoneSource`, `MicrophoneFeed` | What the microphone captures: a live stream and a **30 s ring buffer in memory**, never written to disk. Outside the bus, like the sound level (ADR 0002, section 5): it serves three slices (audio, wake word, transcription). |
| `MicrophoneMuteRequested` | Request to mute or unmute (tray, settings page). |

### Slice `Features/Audio/Devices`

- **`MicrophoneService`** (background service) keeps one stream open on the right microphone. Each change
  (start, setting, device plugged or unplugged, end of a stream, mute request) runs the same comparison
  between the open stream and the wanted one.
  - Chosen microphone present: it is opened. Missing: the Windows default, until it comes back.
  - No microphone, or one that refuses to open: "fault" condition with cause `MicrophoneMissing`, cleared as
    soon as a microphone opens again. Activations are refused meanwhile (ADR 0005).
  - Microphone muted (US1.13): the stream is closed, the buffer emptied, the level drops to zero,
    `MicrophoneMuteChanged` goes to the state machine, and the choice is saved.
  - Each captured block goes to `MicrophoneFeed` and its level (`MicrophoneLevel`, decibels from −60 to 0)
    to the orb.
- **`OutputBinder`** gives the chosen output to `IAudioPlayback` at start-up and after each change: the
  slices that play a sound do not read this slice's settings.
- **"Audio" settings page** (`Audio.Devices`, after the language, the orb and the language models): microphone (with "Windows default
  (name)"), live level, "Mute the microphone" box, output, "Test the output" button (two generated notes,
  `OutputChime`).
- Settings: section `Audio.Devices` of `config.json` (`microphoneId`, `outputId`, `microphoneMuted`).

### Windows host (`Kazo.Jarvis.Windows/Audio`)

- NAudio 3.3, WASAPI in shared mode, with the new `WasapiRecorderBuilder` and `WasapiPlayerBuilder` classes
  (see the [pitfalls](pitfalls.md)).
- **Capture:** device format (often 48 kHz stereo `float`), converted by `CaptureConverter`: mean of the
  channels, 7 kHz low-pass filter, then interpolation to 16 kHz. 50 ms buffer. An unplugged microphone stops
  the stream (`AUDCLNT_E_DEVICE_INVALIDATED`), which triggers the comparison.
- **Playback:** each clip is converted to the output's mix format (`PlaybackConverter`) and played by its
  own player; Windows mixes clips played together. 60 ms latency.
- **Devices:** `MMDeviceNotificationClient` (added, removed, state, default device).
- Every Core Audio call goes through a pool thread: the objects are never created on the UI thread.
- **Tray:** the "Mute the microphone" entry is enabled, and ticked while the microphone is muted.

### Web host

`SimulatedAudioCapture` (two microphones, a 220 Hz hum whose loudness rises and falls, buttons to unplug and
plug back) and `SimulatedAudioPlayback` (silent, remembers the last output used).

## Provisional choices of this work item

ADRs 0003 and 0006 do not settle them; to be confirmed by Benoit:

1. **Mute survives a restart** (`microphoneMuted` setting): a muted microphone does not open again by itself.
2. **Chosen microphone missing:** Jarvis listens through the Windows default until it comes back, with no
   fault. The fault is set only when there is no microphone at all.
3. **Muting clears the "microphone missing" fault:** nothing is missing while the user does not want the
   microphone.
4. **30 s listening buffer** (about 2 MB).
5. **Sound level in decibels**, from −60 dB (0) to 0 dB (1).
6. Conversion to 16 kHz done by Jarvis (filter and interpolation), not by the Windows audio engine.
7. No voice level in the playback: it comes with synthesis (work item 12).

## Tests

- Unit: `MicrophoneFeedTests` (ring buffer), `MicrophoneServiceTests` (opening, choice, mute, unplug, fault,
  with the real state machine), `AudioPiecesTests` (level, test sound, output), `AudioConvertersTests`
  (conversions of the Windows host, also compiled on Linux).
- Playwright (`AudioDevicesTests`): mute from the tray and from the page, microphones unplugged, change of
  microphone and level, test sound on the chosen output.

## Windows checks

To do at the next session with Benoit (the Web host does not prove them):

1. **Real microphone:** the "Audio" page lists the Windows microphones, the level moves when speaking,
   choosing another microphone applies without a restart. Note the device format in the log.
2. **Muted microphone:** from the tray (entry ticked) then from the page; the orb shows "microphone muted"
   and **Windows no longer shows an application listening** (microphone icon in the taskbar, Settings >
   Privacy > Microphone). After a restart, the microphone stays muted.
3. **Microphone unplugged while Jarvis runs:** with two microphones, Jarvis moves to the other; with one,
   the orb shows the error, then recovers when it is plugged back. Nothing at `Error` level in the log.
4. **Output:** "Test the output" plays the two notes on the chosen output (speakers, then headset), without
   crackling.
5. **Cost:** microphone open while idle, share of one core for the process (spike E6 method), to note in the
   memory.
