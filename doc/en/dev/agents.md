# Agents: one agent per kind of task, the sorting and the hand

Work item `stage2/17-agents`. Decision: [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 1 to 4,
8 and 9. The full turn is described in [conversation.md](conversation.md), the tool loop in
[tools.md](tools.md), the faults in [faults.md](faults.md).

This work item builds the agents, their files, the sorting by the Jarvis agent and the choice of the agent in the
conversation. Work item 18 added the memory of each agent and the choice of the agent of an MCP server (below), work item 19
the projects and the "Project" agent ([projects.md](projects.md)). Work item 20 built the "Call" mode
(section 12, [call-mode.md](call-mode.md)).

## What is built

```
agents\<name>.md (folder of Jarvis) ─► IAgentFiles (kernel, implemented by the host)
                                             │
                                             ▼
"Agents" page ◄── AgentCatalog (slice Agents/Catalog) ──► tool registry:
                         │   reads, reads again, writes the          hand_to_agent, return_to_jarvis
                         │   starting files, raises the fault
                         ▼
Conversation/Dialogue ─► IAgentCatalog (kernel): Main · Find(name) · ToolsOf(agent) · ReadHand(arguments)
   │
   ├─ request ─► Jarvis agent (its tools + hand_to_agent) ─► answers itself: one call to the model
   │                 └─ hand_to_agent(agent) ─► the agent named (its instructions, its tools) ─► answers
   │                                                └─ return_to_jarvis ─► Jarvis agent, without handing over again
   └─ typed request, agent chosen in the window ─► that agent, without the Jarvis agent
```

The slices `Agents/Catalog` and `Conversation/Dialogue` do not know each other: the kernel contract
`IAgentCatalog` links them. On a host without `IAgentFiles` there is no agent, and a turn gives the model every
enabled tool, as before.

## An agent file

`%USERPROFILE%\.kazo\jarvis\agents\<identity>.md`, like an agent of Claude Code:

```
---
name: Home
description: The lights, plugs, covers, locks, scenes and scripts of the house.
tools: HassTurnOn, HassTurnOff
---

Before acting on a device whose name is uncertain, read the entities and take the exact name.
```

- **The identity** is the name of the file without its extension, lower case (`home-automation`). It does not
  change with the language. `jarvis` is the Jarvis agent.
- `name` is the name shown, said, and written by the model that hands over. `description` is the sentence used
  for the sorting. Both are required.
- `tools`: tool names separated by commas, or one `- name` per line. May be missing.
- The rest of the file is added to the system message while this agent answers, after the personality and the
  fixed rules, before the memory.
- A file is 256 KiB at most.

| File left out | Cause |
| --- | --- |
| `Unreadable` | the disk refuses the read, or the file is too large |
| `NoHeader` | no header between two `---` lines |
| `NoName`, `NoDescription` | the header lacks that line |
| `NameTaken` | another file already gives this name |

A file left out raises the fault `AgentUnreadable` (origin `Agents`), which leads to the "Agents" page; the page
names the file and the cause. The fault goes away as soon as every file reads. The other agents keep answering.

**The files are read again when they change** (`FileSystemWatcher`, the writes of one save grouped over 400 ms).
The change applies to the next turn.

## The starting agents

Written at the first launch, in the language of the assistant:

| Identity | Name (fr / en) | Tools named |
| --- | --- | --- |
| `jarvis` | Jarvis | `get_datetime`, `calculate`, `hand_off_to_claude`, `start_quiet_period` |
| `home-automation` | Domotique / Home | none: those of the Home Assistant servers come to it by themselves |
| `web` | Web | `web_search`, `fetch_url` |
| `files` | Fichiers / Files | `list_directory`, `read_file` |
| `claude-code` | Claude Code | `get_claude_sessions` |
| `memory` | Mémoire / Memory | `remember_fact`, `forget_fact`, `search_memory` |
| `project` | Projet / Project | `list_directory`, `read_file`, `get_claude_sessions`, `remember_project_fact` (work items 19 and 21, see [projects.md](projects.md)) |

**A file the user changed is never overwritten.** Jarvis keeps the SHA-256 fingerprint of each file it wrote
(section `Agents.Files` of `config.json`). At start:

- file missing and never written: it is written;
- file present, whose text still has the kept fingerprint: it follows what Jarvis ships (a new version, or
  another language of the assistant);
- any other case (changed, or deleted by the user): Jarvis leaves it alone.

When the file `jarvis.md` is missing or does not read, the Jarvis agent that ships with the application answers,
and the page says so.

## The tools of an agent (`AgentCatalog.ToolsOf`)

An agent gives no right: it chooses what the model **sees**. Every call still goes through the decision point.

- An agent sees the **enabled** tools its file names, and those their source gives it (`ITool.Agent`: the tools
  of a Home Assistant server go to `home-automation`).
- **The Jarvis agent** also sees every enabled tool no other agent claims: the tools of a generic MCP server, a
  tool added by a later version. Nothing is lost.
- The Jarvis agent gets `hand_to_agent` only when another agent has at least one tool of its own. Otherwise
  there is nothing to sort: one call, with all it has.
- The other agents get `return_to_jarvis`.
- An agent cannot call the tool of another one: the call is denied ("NotThisAgent") without going further.
- **The tools of a generic MCP server** go to the agent chosen on the "Tool servers" page
  (`McpServerOptions.Agent`, the "Agent" list of each server); the Jarvis agent by default. The choice applies to
  the next turn, without a new connection. A Home Assistant server has no choice: its tools go to
  `home-automation`. A chosen agent whose file is gone: the tools go back to the Jarvis agent.
- A tool **for every agent** (`ITool.ForEveryAgent`) is seen by each agent but the Jarvis agent, whatever its file
  says. It does not count as a tool of its own: an agent that only has that one does not take the hand. Today
  there is one: `remember_agent_fact`.

## The sorting and the hand (`ConversationEngine`)

- **The Jarvis agent receives the request.** No sorting call comes before it. A simple request costs one call
  to the model; a request handed over costs two.
- `hand_to_agent(agent, reason)`: its description holds one line per agent that can take the hand (name and
  description), never their tools; `agent` only accepts those names. The name is compared without case or
  accents, and the identity works too.
- **The hand changes:** the loop of the Jarvis agent stops, and the turn continues with the agent named. It gets
  the history and the request as the user made it, not the calls of the agent before it.
- **By voice, Jarvis says so:** what the model wrote before the call is said; when it wrote nothing, Jarvis says
  "I hand over to the agent Web."
- **An unknown name is refused**, like an agent without a tool: the result names the known agents, and the Jarvis
  agent keeps the hand.
- `return_to_jarvis`: the request goes back to the Jarvis agent, which then answers without `hand_to_agent`: the
  hand does not go round. A turn is therefore three answers of the model at most, tool calls aside.
- **The conversation window** has an "Agent" list: "Jarvis chooses", or an agent. A **typed** request then goes
  straight to that agent. The choice holds until it is removed, and is forgotten when Jarvis stops. A **spoken**
  request always goes to the Jarvis agent.
- Each turn keeps the name of the agent that answered (`ConversationTurn.Agent`), shown under the answer.
- These go through no agent: "Ask Claude…" and "Ask Claude Code to…", recognized by code, the answer to a
  confirmation request, and the announcement turn.

## The memory of each agent (work item 18, ADR 0025, section 5)

What an agent learns while working and that serves it alone ("Articulé is the desk lamp"). It lives in the
`Conversation/Memory` slice, under the same rules as the facts about the user (ADR 0019): one short sentence of
200 characters at most, never a secret, visible and erasable on the "Memory" page, never sent to the cloud, cut by
the same switch.

- **Where.** In the same collection as the other facts: `MemoryFact.Agent` holds the identity of the agent, empty
  for a fact about the user. The same sentence may serve two agents. The 500 facts at most count everything.
- **How a fact gets in.** Through the tool `remember_agent_fact`, which every agent but the Jarvis agent gets: the
  engine tells the tool which agent calls (`ITool.InvokeAsync(arguments, agent, …)`), the model does not choose
  it. Class "Everyday effect", refused after a content read from outside, like `remember_fact`. Or by hand, on
  the "Memory" page: the list next to the field says who the fact is for.
- **When it is sent.** Only when this agent answers (`IConversationMemory.RecallForAgentAsync`): a second block
  "[Agent memory]", after the one of the user, with the same rule "this is data". Its share: one sixteenth of the
  context, on top of the eighth of the memory of the user.
- **The Jarvis agent** has no memory of its own: its memory is the memory of the user.
- `forget_fact` and `search_memory` look in every fact, those of the agents included.
- The automatic extraction (ADR 0019, section 4) puts nothing in the memory of an agent: its facts stay facts
  about the user.

## The "Agents" page

After the "Memory" page. The folder of the files, the files left out with their cause, then each agent: its
name, its file, its description, the tools the model sees, those the file names but that are off or missing, and
an "Open the file" button (the editor Windows has for a `.md`).

## Provisional choices of this work item

To be confirmed by Benoit:

1. The starting files are written in the language of the assistant, and follow a change of language as long as
   the user did not touch them.
2. The identities are in English (`home-automation`, `files`, `memory`), the names in the language.
3. A starting agent the user deleted is not created again.
4. An agent without any enabled tool does not take the hand.
5. The choice of the agent in the window is forgotten when Jarvis stops.
6. By voice, the fixed sentence "I hand over to the agent X" is said only when the model said nothing before.
7. The memory tools belong to the Memory agent: "remember that…" costs two calls to the model.
8. The Claude Code agent only has `get_claude_sessions`; starting a work stays "Ask Claude Code to…".
9. The memory of an agent takes one sixteenth of the context, on top of the eighth of the memory of the user.
10. `remember_agent_fact` is given to every agent but the Jarvis agent, without going through their file.
11. Deleting an agent file does not erase its facts: they stay on the "Memory" page, under its identity.
12. `forget_fact` and `search_memory` see the facts of every agent.

## Checks on Windows

Nothing of this work item ran on Windows.

1. At the first launch, the six files are in `%USERPROFILE%\.kazo\jarvis\agents`.
2. "Open the file" opens the editor; saving an instruction changes the next turn without a restart.
3. A file without a header: a named fault, a page that says which one, the fault gone once fixed.
4. **Delay before the first sound** of a simple request, to compare with the one before the agents (ADR 0025,
   section 8); then that of a request handed over (two calls).
5. Does the local model (`qwen3:8b`) hand over to the right agent? "Éteins la lumière Articulé", "cherche sur le
   web…", "souviens-toi que…".
6. Size of the request of the Jarvis agent, read in the tokens of the turn, against the 1,757 tokens of the
   acceptance.
7. Does the local model call `remember_agent_fact` when it should? "Articulé is the desk lamp", said to the Home
   agent, then "turn on Articulé" in another conversation.
8. "Tool servers" page: choose an agent for a generic server, and see its tools change agent on the "Agents"
   page.
