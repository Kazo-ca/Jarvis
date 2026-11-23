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
| [home-assistant.md](home-assistant.md) | Home Assistant through MCP: classification of a call from the devices it aims at, "Critical" security home automation, "security" list, "Home Assistant" page |
| [mcp.md](mcp.md) | MCP client: "tool server" contract, adapter, connection and new attempts, tools off when found, fingerprint of a tool, classes, "MCP servers" page |
| [tools.md](tools.md) | Tools: kernel contracts, permission decision point and its log, tool loop, time and calculation |
| [llm.md](llm.md) | Language models: providers, adapters, default model, connection test |
| [first-run.md](first-run.md) | First run: setup wizard, "Voice models" page (checked downloads), Ollama search |
| [conversation-memory.md](conversation-memory.md) | Conversation memory: facts and summary, extraction by the local model from the words of the user only, block sent to the model, tools `remember_fact`, `forget_fact`, `search_memory`, "Memory" page |
| [hand-off.md](hand-off.md) | Hand-off: target for questions among the cloud providers, terminal tool `hand_off_to_claude`, "ask Claude" recognized by code, what leaves for the target, who answers, "Hand-off" page |
| [agents.md](agents.md) | Agents: one Markdown file per agent, starting agents, tools each agent sees, sorting by the Jarvis agent, tools `hand_to_agent` and `return_to_jarvis`, choice of the agent in the conversation, "Agents" page |
| [projects.md](projects.md) | Projects: a name and a folder, memory read from `JARVIS.md`, project of a turn (chosen, named in the sentence or when handing over), "Project" agent, "Projects" page |
| [call-mode.md](call-mode.md) | "Call" mode: starting (button or sentence), the agent that keeps the hand, listening without "Hey Jarvis", "goodbye" |
| [projects-view.md](projects-view.md) | Projects view (ADR 0026), first work item: rail, "To handle", "All projects", panel of the project, band of the tasks of a subject, thread of the task, "Find"; read from the task board |
| [spend-cap.md](spend-cap.md) | Spending cap: price per cloud provider, cost counted at each answer of the target, count of the month, warning at 80 %, cap that withdraws the tool and keeps Jarvis local, "Spending" page |
| [tasks-and-sessions.md](tasks-and-sessions.md) | Tasks and Claude Code sessions: shape of a task and its nine statuses (Relais), project and topic keys, board of the tasks, reading of the registry of the sessions without ever opening a key file, "Claude sessions" page |
| [hook-server.md](hook-server.md) | Local server and Claude Code hooks: server on `127.0.0.1` only, no route without a token, the five `/hook/…` routes that move the tasks, installation and removal of the hooks in the user's Claude Code settings on their click, with a preview and a dated copy |
| [claude-code-launch.md](claude-code-launch.md) | Starting Claude Code (first part of work item 12): requests of a session at the decision point, `approve` tool on `/mcp`, command line of `claude -p`, files of Jarvis without a secret, events of the stream |
| [claude-code-tasks.md](claude-code-tasks.md) | Claude Code tasks (second part of work item 12): "Ask Claude Code to…", choice of the allowed folder, task created before the launch, classification of the requests of a session, stop of a session that writes without asking, ends of a session, cost and "Claude Code is paid by subscription", "Claude Code tasks" page |
| [task-fan.md](task-fan.md) | Task fan (work item 14): right click on the orb, double click kept for the projects view, one panel per task, sub-tasks, amber panel and its Allow and Refuse buttons, arc layout, animation, fan window kept hidden in the Windows host, amber badge and task count of the orb |
| [session-announcements.md](session-announcements.md) | Announcements of the Claude Code sessions (work item 13): the announcement turn (ADR 0024), what is news, last message of a session shortened or cut, per-folder setting, quiet hours and quiet period, tools `start_quiet_period` and `get_claude_sessions`, "Announcements" page |
| [faults.md](faults.md) | Faults: named causes, corrective action, link to the right setting, "Jarvis problem" window |
| [updates.md](updates.md) | Updates: check, download, updater, publishing |
| [pitfalls.md](pitfalls.md) | Pitfalls met and how they were solved |
| [spikes.md](spikes.md) | Summary of the technical spikes (E1 to E15) |
