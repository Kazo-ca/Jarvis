# Features

## Available

*(none yet)*

## In development

| Feature | Stage | Reference |
| --- | --- | --- |
| Orb, voice, local model and settings: built and merged into `main` on November 20, 2026, acceptance on Windows still to do | 1 | [Design](dev/plans/design.md), [series plan](dev/plans/stage-1.md) |
| Tools, MCP, handoff and Claude sessions. Built: the tool loop, the permission decision point, the date and time and calculation tools ([details](dev/tools.md)); the confirmation request before a sensitive or critical action, by voice or click, and the permission log ([details](dev/confirmation.md)); reading files in the allowed folders, web search and reading a page through the in-app browser of Jarvis (off at the start), the "Tools" page ([details](dev/basic-tools.md)); MCP servers added by address or by command, their tools turned on one by one ([details](dev/mcp.md)); Home Assistant through MCP, where a light turns on without a question and a lock, the alarm or the garage door require a click ([details](dev/home-assistant.md)); the conversation memory: facts kept on request or automatically from the words of the user only, summary of the old exchanges, "Memory" page to see and erase ([details](dev/conversation-memory.md)); the hand-off: a cloud target chosen by the user (none at first) answers when the local model hands the question over or when the request starts with "Ask Claude", and the conversation says who answered ([details](dev/hand-off.md)); the spending cap: a price per cloud provider, the cost of each answer counted, a visible count of the month, a warning at 80 %, and at the cap (10 USD a month at first) Jarvis says so and stays local ([details](dev/spend-cap.md)) | 2 | [Design](dev/plans/design.md), [series plan](dev/plans/stage-2.md) |
| Task fan: a double click on the orb brings out the running tasks | 2 | [ADR 0010](dev/adr/0010-task-fan-and-orb-click.md) |
| Autonomous mode, triggers and email | 3 | [Design](dev/plans/design.md) |
| Android mobile app | 4 | [Design](dev/plans/design.md) |
| Relais: Claude, ChatGPT and other agents talk to each other through MCP and hooks while the human sees the exchanges, and their work is tracked as projects, subjects, tasks and sub-tasks with an orchestrator | v2 | [ADR 0013](dev/adr/0013-relais-readiness.md) |
| Work tracking in GitHub Issues or Azure Boards: one source set per project, every agent commit and PR linked to an issue (`#<id>`) or an Azure DevOps work item (`AB#<id>`), and, as an option, a mandatory issue or work item for every subject and task, which Jarvis creates, changes and synchronizes | v2 | [ADR 0015](dev/adr/0015-work-tracking-source.md) |
