# ADR 0009: permission and confirmation model

## Status

Proposed on 2026-11-18, waiting for Benoit's approval (work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 23, 39 and 40 of the [design](../plans/design.md) and its "Security and privacy"
  section; stories US1.13, US2.10, US2.15, US3.10 and US3.15; spikes E2, E8 and E9
  ([summary](../spikes.md)); decisions 5 and 6 of the
  [spikes](../spikes.md#decisions-taken-on-november-18-2026).
- **Completes:** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (a conversation turn),
  [ADR 0007](0007-llm-providers-and-tool-format.md) (Jarvis keeps the tool loop).

## Context

- Jarvis listens all the time, controls the house and will be able to run code. The design sets nine
  security rules that apply to every stage.
- A voice confirmation can be captured by mistake or spoken by someone else. The design asks for a click for
  the riskiest actions.
- 3CX telephony is dropped: the voice PIN planned for calls no longer has a use.
- Spike E8: `claude -p` refuses and reports unauthorized writes. But **the user's personal hooks and
  settings apply to the sessions Jarvis starts**: one of Benoit's hooks intercepted a write before the
  permission refusal, and the session cost $0.64 with his context loaded.
- Spike E9: Home Assistant events arrive through the WebSocket API, not through MCP.
- Spike E2: Claude Code's `sessions/*.key` and `ide/*.lock` files hold secrets.
- Stage 1 has no tool, no Claude and no autonomous task. The model is set now so that everything that
  follows is built on it.

## Decision

### 1. Four principles

1. **The model proposes, the code decides.** A model, local or cloud, can only ask for an action. The
   decision is taken by code, outside the model, in the same place for every tool.
2. **Data is never an instruction.** The content of an email, a web page, a file, a webhook body, a script
   output, a Home Assistant event or a tool result is passed on as data. It can neither grant a permission
   nor change what a task is allowed to do.
3. **Deny by default.** Without explicit approval within the time limit, the action does not happen.
4. **Only a human approves.** An approval comes from a human channel: voice, a click on the PC, or the
   paired phone. An approval produced by a model, a tool or a task is rejected.

### 2. Action classes

| Class | Examples | Approval required |
| --- | --- | --- |
| Read | Read a file from an allowed folder, give the time, calculate, read the state of the house | None |
| Everyday effect | Turn on a light, set the volume | None |
| Sensitive | Write a file, run a command, run a generated script | Confirmation, by voice or by a click |
| Critical | Security home automation: lock, alarm, garage door | Confirmation through a **strong channel**: click on the PC or paired phone. Voice is not enough. |
| Forbidden | Leave the allowed folders, read Claude Code's secret files, approve oneself | Refused, logged |

- Each tool declares its class. The class of each tool, and how security home automation is recognized, are
  set by the stage 2 ADRs.
- A write, a command or a script is never classed below "Sensitive".

### 3. Confirmation request

- A request carries: who makes it (tool, session or task), a **summary** in the user's language, the
  **exact detail** (path, command, differences), its class, the accepted channels and its time limit.
- Jarvis reads the summary aloud and shows it near the orb with Approve and Refuse buttons.
- **The voice answer is recognized by code, not by the model**: a closed list of words per language (yes,
  no, confirm, cancel and their French equivalents), valid only in the listening that Jarvis opens right
  after its question. Any other answer counts as a refusal.
- An approval covers **one** action. There is no standing approval, with one exception: an approved script
  (section 6).
- Each request and its answer are logged, with the channel used.

### 4. Autonomous tasks

- An autonomous task can never approve a write, a command or a script by itself (US3.15).
- If it needs an approval, it goes on hold and notifies the user. It only resumes with a human's approval.

### 5. Allowed folders

- File tools and Claude Code only see the folders of the list in the settings.
- **Read-only by default.** Any write or command is of class "Sensitive".
- A path is compared after normalization: full path, links and junctions resolved, case ignored.

### 6. Generated scripts

- A script written by Jarvis or by Claude stays "pending" until the user approves it.
- The approval is tied to the hash of the content: any change puts the script back to pending
  (decision 40). Scripts written by the user are approved automatically.

### 7. Claude Code sessions started by Jarvis

- **Jarvis starts its sessions with its own Claude Code settings.** The user's personal settings and hooks
  are not loaded: behavior is predictable, and the context is smaller.
- Jarvis drives `claude -p` with JSON streams (E8) and chooses by itself:
  - the setting sources (`--setting-sources`) and its settings file (`--settings`);
  - the MCP servers of the session (`--mcp-config` with `--strict-mcp-config`);
  - the tools allowed without asking (`--allowedTools`) and the permission mode (`--permission-mode`);
  - the folders (`--add-dir`), from the list of allowed folders;
  - the cap of the session (`--max-budget-usd`).
- A Claude Code permission request reaches Jarvis through a local MCP tool (`--permission-prompt-tool`) and
  follows section 3. This was not tried: it is checked by a spike at the start of stage 2, and the exact
  command line is set by the stage 2 ADR.
- **Left to decide at stage 2:** whether the project settings (`project`) are loaded. They bring the
  repository's instructions, but also its hooks.
- For sessions Jarvis did not start, it only announces. It never answers in their place (US2.15).
- Jarvis never reads `sessions/*.key` or `ide/*.lock`.

### 8. Home Assistant

- Commands go through MCP and follow the classes of section 2.
- Triggers go through the WebSocket API (`subscribe_trigger`), read-only. The content of an event is data
  (principle 2).
- Both channels use the same address and the same token, kept in the vault
  ([ADR 0004](0004-configuration-storage-and-secret-vault.md)).

### 9. What leaves the machine

- The wake word, recognition and local synthesis run on the PC.
- A text or a sound only leaves the machine by an explicit choice of the user: a cloud voice, a cloud
  provider, a handoff. The screen where that choice is made says so.
- Microphone muted (US1.13): the capture stream is closed, no sound is captured.
- The spending cap and the opening of the embedded server are described by the ADRs of stages 2 to 4.

### 10. What stage 1 builds

- The microphone mute, the vault and the cloud voice warning.
- The permission decision point is built with its first tool, at stage 2. Nothing is written in advance.

## Consequences

- An instruction injected in an email or a web page can fool the model, but cannot obtain a write, a command
  or a critical action: the approval does not depend on the model.
- Sensitive actions need a gesture every time. This is intended for the first version; a lasting approval
  per folder or per tool would need a new ADR.
- The voice PIN of decision 12 is not carried over, since telephony is dropped.
- Before stage 4, the only strong channel is the click on the PC. A critical action therefore cannot be
  approved remotely.
- The Claude Code sessions started by Jarvis do not benefit from Benoit's personal hooks. If he wants some
  of them, they are copied into Jarvis's settings.
- The list of actions that require a click is put to Benoit in the conversation. This ADR keeps the
  recommended option: security home automation only.
