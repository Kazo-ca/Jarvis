# First run

Work item `stage1/14-first-run` (US1.4, [stage 1 plan](plans/stage-1.md#14-first-run-stage114-first-run),
[ADR 0006](adr/0006-voice-chain.md), section 7). Français : [first-run.md](../../fr/dev/first-run.md).

## What is built

Two slices: `Setup/Models` downloads the models, `Setup/FirstRun` chains settings pages into a wizard. The wizard
knows the pages only by their key (`SettingsPage`, ADR 0002, section 3): each page saves its own choices, as in the
settings window.

### Kernel

| Item | Role |
| --- | --- |
| `Kernel/Models`: `ModelDownload`, `IModelInstaller`, `ModelDownloadException` | One file to put in the models folder: source, relative target, size, checksum (`sha256:` or `sha1:`), plain file or `.tar.bz2` archive. Failure causes: server unreachable, wrong checksum or size, disk, other. |
| `Kernel/Hardware`: `IGraphicsCardProbe`, `GraphicsCard` | The NVIDIA card (name, memory), or none. |
| `WhisperModelChosen` (`Messages/Voice`) | The Whisper model installed from the models page; the transcription slice, which owns that setting, takes it. |
| `FirstRunWizardRequested` (`Messages/Desktop`) | The "Setup wizard…" entry of the tray menu. |

### Adapters

- **`HttpModelInstaller`** (`Adapters/Models`): writes the download to `models\.downloads\`, checks the size then the
  checksum, and only then moves it into place (a wrong or partial file never replaces a model). The sherpa-onnx
  voices are `.tar.bz2`: bzip2 by SharpZipLib, then .NET's `TarFile`, which refuses any entry outside the folder;
  the archive is deleted once unpacked. Its own HTTP client, 30 minutes at most.
- **`NvidiaSmiGraphicsCardProbe`** (`Adapters/Hardware`): runs `nvidia-smi --query-gpu=name,memory.total`, which the
  NVIDIA driver installs; without it there is no CUDA either. Windows reports a card's memory on 32 bits only
  (4 GB at most), hence the driver.

### Slice `Features/Setup/Models`

- **`ModelCatalog`**: the models, their pinned sources, sizes and checksums.

| Model | Files | Source | Size |
| --- | --- | --- | --- |
| "Hey Jarvis" | `models\wake-word\` (3 files) | openWakeWord release 0.5.1 on GitHub | 3.7 MB |
| Silero | `models\vad\silero_vad.onnx` | sherpa-onnx `asr-models` release on GitHub | 0.6 MB |
| Whisper large-v3-turbo, small, base | `models\whisper\` | `ggerganov/whisper.cpp` on Hugging Face (SHA-1 published by whisper.cpp) | 574, 488, 148 MB |
| Piper Tom (fr), Alan (en) | `models\vits-piper-…\` | sherpa-onnx `tts-models` release on GitHub | 67 MB each |
| Kokoro | `models\kokoro-multi-lang-v1_0\` | sherpa-onnx `tts-models` release on GitHub | 350 MB |

- **`ModelAdvice`**: what fits the card found. NVIDIA card with at least 10 GB: Whisper turbo on the card and
  `qwen3:14b`. Less than 10 GB: Whisper `small` on the card and a language model of 7 to 8 billion parameters (for
  instance `qwen3:8b`). No NVIDIA card: Whisper `base` on the processor, and the same advice.
- **`ModelsKeeper`**: one batch of downloads at a time, one file after the other; the state of each model (missing,
  waiting, downloading with a percentage, installed, failed with the cause); "Stop" deletes the file in progress and
  keeps those already in place. The batch goes on if the page closes. At the end, if the chosen Whisper is in place,
  `WhisperModelChosen` (graphics card when an NVIDIA card is there, processor otherwise).
- **"Voice models" page** (`Setup.Models`, order 40, after "Voices"): the card found and the advice, the models by
  group (wake word, end of speech, recognition, voices) with their size and state. Checked by default: the wake
  word, Silero, the advised Whisper, both Piper voices. Kokoro stays a choice. One button: "Download the selection
  (size)", or "Use the chosen recognition" when everything is there.

### Slice `Features/Setup/FirstRun`

- **Steps**: Language (`Settings.Language`), Audio (`Audio.Devices`), Voice models (`Setup.Models`), Language models
  (`Llm.Providers`), then "Ready". A page no slice registers is skipped. "Back" and "Next" only change the page; the
  step survives a language change (`FirstRunWizardState`).
- **`FirstRunLauncher`** opens the wizard at start when `config.json` does not exist or the wizard was left
  unfinished, and on request (tray menu). Section `Setup.FirstRun` of `config.json`: `pending` turns `true` as soon
  as a first run opens it and `false` on "Finish". A file written before this work item has no such section: the
  wizard does not open by itself.

### Ollama on 11434 then 11435 (slice `Llm/Providers`)

`ProviderEditor.FindOllamaAsync` tries `http://127.0.0.1:11434` then `:11435` (3 s each). A server that answers
without the model counts (the test then says which model to pull). The "Language models" page searches by itself
while the providers are the default ones; found on another port, the address is saved at once. A "Look for Ollama
on this PC" button searches again for any Ollama provider.

### Hosts

- **Windows**: "Setup wizard…" entry in the tray menu; installer and card probe registered by `AddJarvisAdapters`.
- **Web**: `SimulatedModelInstaller` (0.6 s per file, in memory, nothing leaves the machine),
  `SimulatedGraphicsCardProbe` (RTX 3060, 12 GB), "Open the first-run wizard" button. The Web host now simulates an
  existing profile, so that every test starts on the orb.

## Provisional choices of this work item

To be confirmed by Benoit; each one changes in one line:

1. **Advice thresholds**: 10 GB of graphics memory for the full chain; below, Whisper `small` on the card; without
   an NVIDIA card, Whisper `base` on the processor. Fallback model advised: `qwen3:8b` (until the measure of work
   item 11).
2. **SHA-1 checksum for Whisper**: whisper.cpp publishes only that one. The other models are checked with SHA-256.
3. **New dependency SharpZipLib 1.4.2** (MIT), only for the bzip2 of the sherpa-onnx voices, in `Adapters`.
4. **Order of the steps**: language, audio, voice models, language model (the voice models advice serves the
   language model). No "Voices" step: the Piper voices of both languages are the default ones.
5. **Both Piper voices checked by default** (French and English are mandatory in v1), Kokoro not.
6. **Automatic Ollama search** only while the providers are the default ones, saved at once when found on another
   port.
7. **The wizard opens again** from the tray menu, without becoming "pending" again.

## Tests

- Unit: `HttpModelInstallerTests` (SHA-256 and SHA-1 checksum, size, unreachable server, archive unpacked, target
  outside the folder refused, cancellation leaving nothing), `NvidiaSmiGraphicsCardProbeTests`, `ModelCatalogTests`
  (each file where the adapters read it), `ModelAdviceTests`, `ModelsKeeperTests`, `FirstRunLauncherTests`,
  `OllamaSearchTests` (11435 found after 11434), `WhisperChoiceApplierTests`.
- Playwright (`FirstRunTests`): the whole wizard in English (simulated download included), and in French.
- Real run in the container on 20 November 2026: `HttpModelInstaller` downloaded, checked and put in place the wake
  word, Silero, both Piper voices and Kokoro from GitHub; Piper and Kokoro listed them and Piper spoke. Hugging Face
  is blocked from the container: **Whisper was not really downloaded**.

## Windows checks

To do at the next session with Benoit:

1. Rename `%USERPROFILE%\.kazo\jarvis` (for instance to `jarvis.bak`), start Jarvis: the wizard opens.
2. Choose French, the microphone and the output; the "Voice models" page says "NVIDIA GeForce RTX 3060, 12 GB" and
   advises Whisper turbo. Download the selection: everything turns "Installed", **Whisper included** (Hugging Face).
3. "Language models" page: "Ollama answers on port 11435; its address is saved."
4. Finish, say "Hey Jarvis" and ask a question: the whole chain works without copying anything by hand.
5. Close the wizard half-way on an empty profile: it comes back at the next start.
6. Put the original folder back (`jarvis.bak` → `jarvis`).
