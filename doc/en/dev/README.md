# Development

| Document | Content |
| --- | --- |
| [plans/](plans/README.md) | Design document and series plans |
| [adr/](adr/README.md) | Architecture decisions |
| [ui/](ui/README.md) | Catalog of the UI designs (orb, task fan, full screen) |
| [build.md](build.md) | Build, test, continuous integration, architecture tests |
| [kernel.md](kernel.md) | The kernel: message bus, state machine, sound level |
| [storage.md](storage.md) | Settings (`config.json`), SQLite database, secret vault |
| [hosts.md](hosts.md) | Windows and Web hosts: start-up, orb, tray icon, hotkey, dialogs |
| [orb.md](orb.md) | The orb, high-quality and light versions: component, quality setting, looks, motion, cost, demonstration page |
| [shared-ui.md](shared-ui.md) | Shared style, fonts, translations, language setting, settings window, secret field |
| [audio.md](audio.md) | Audio devices: microphone, output, mute, listening buffer |
| [wake-word.md](wake-word.md) | Wake word: openWakeWord models, threshold, live score, settings page |
| [transcription.md](transcription.md) | Transcription: end of utterance (Silero, energy threshold), Whisper, "Transcription" page, models |
| [speech.md](speech.md) | Speech: Piper, Kokoro and cloud engines, voice of each language, "Voices" page |
| [conversation.md](conversation.md) | Conversation: the full turn, the interruption, the conversation window, the personality |
| [confirmation.md](confirmation.md) | Confirmation request: condition and orb look, window, question said, answer by voice or click, no timeout, announcement for a typed request, permission log |
| [basic-tools.md](basic-tools.md) | Basic tools: allowed folders, `list_directory`, `read_file`, `web_search`, `fetch_url`, browser of Jarvis (WebView2), "Tools" page |
| [mcp.md](mcp.md) | MCP client: "tool server" contract, adapter, connection and new attempts, tools off when found, fingerprint of a tool, classes, "MCP servers" page |
| [tools.md](tools.md) | Tools: kernel contracts, permission decision point and its log, tool loop, time and calculation |
| [llm.md](llm.md) | Language models: providers, adapters, default model, connection test |
| [first-run.md](first-run.md) | First run: setup wizard, "Voice models" page (checked downloads), Ollama search |
| [faults.md](faults.md) | Faults: named causes, corrective action, link to the right setting, "Jarvis problem" window |
| [updates.md](updates.md) | Updates: check, download, updater, publishing |
| [pitfalls.md](pitfalls.md) | Pitfalls met and how they were solved |
| [spikes.md](spikes.md) | Summary of the technical spikes (E1 to E15) |
