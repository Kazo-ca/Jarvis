# ADR 0025: one agent per kind of task, its instructions and its memories

## Status

Proposed on 2026-11-21 (work item `stage2/adr-0025-task-agents`), from Benoit's five answers of the same day,
during the stage 2 acceptance. **Not accepted yet**: four points are still open (section 9).

- **Completes:** [ADR 0017](0017-tools-classes-and-decision-point.md) (the tools and the decision point do not
  change: an agent chooses *which* tools the model sees, never what a tool is allowed to do) and
  [ADR 0019](0019-conversation-memory.md) (the conversation memory stays; two memories are added).
- **Builds on:** [ADR 0020](0020-hand-off-targets-and-spend-cap.md) (hand-off),
  [ADR 0021](0021-claude-code-sessions.md) (allowed folders, Claude Code sessions),
  [ADR 0004](0004-configuration-storage-and-secret-vault.md) (the folder of Jarvis).
- **Built:** after the stage 2 acceptance, as the first work item of what follows (answer 1).

## Context

At the acceptance, « Éteins la lumière articulée » gave a tool call with the name « lumière articulée » for the
light named « Articulé ». The immediate fix is in the code (`EntityNames`). Benoit asked for more: "we should
have an agent for each kind of task. When a request is made, it evaluates what kind of task it is, then loads
the right agent. These instructions should be in the Home automation / Home Assistant agent." Then: "there
should be a local memory for each agent and another, complementary one, per project."

Today each turn sends the local model one instruction and **all** the enabled tools. With a local model,
reading that request is what costs: at the acceptance, 1,757 tokens took more than two minutes when the
graphics card was full. An agent that only brings its own tools shortens the request.

## Decision

### 1. An agent

An agent is: a name, a one-sentence description (used for sorting), instructions, and the list of the tools
the model sees when this agent is loaded. It gives no right: the class of each call and the confirmation stay
those of ADR 0017. A tool turned off in the "Tools" page stays invisible, even if an agent names it.

### 2. Markdown files (answer 3)

Each agent is a file `agents\<name>.md` in the folder of Jarvis, in the manner of Claude Code agents: a header
(`name`, `description`, `tools`) then the instructions as text.

- Jarvis writes the starting files at the first run. A file changed by the user is never overwritten by an
  update.
- The files are read again when they change. A file that cannot be read is ignored, and a fault says so.
- A settings page lists the agents and their tools, and opens the file.

### 3. The starting agents (answer 4)

| Agent | Tools | Own instructions |
| --- | --- | --- |
| Home automation | those of the servers of the Home Assistant kind | read the entities before acting on an uncertain name, never guess a lock |
| Web | `web_search`, `fetch_url` | cite the source |
| Files | `list_directory`, `read_file` | stay inside the allowed folders |
| Claude Code | starting and following the sessions | summarize the request before starting |
| Memory | `remember_fact`, `forget_fact`, `search_memory` | |
| General | clock, calculation, hand-off | everyday conversation |

The tools of a generic MCP server go to the agent the user chooses on the page of the server; "General" by
default.

### 4. Sorting (answer 2)

Before each request, **one sorting call** to the local model: it gets the request and the list of the agents
(name and description), and returns the name of an agent. Then the turn is made with that agent: its
instructions, its tools, its memories.

- The sorting request is short and has no tool: it is what makes the second one shorter.
- An answer that names no known agent loads "General".
- **In the conversation window, the user can choose the agent**: the sorting is then skipped. The choice holds
  until the user removes it.
- A confirmation request that waits does not go through the sorting: the answer belongs to the turn that asked
  (ADR 0023).
- The conversation shows the agent of each turn.

### 5. The memory of each agent

Each agent has its local memory: what it learns while working and that is of use to it alone ("Articulé is the
desk lamp, in the living room"). It is sent only when this agent is loaded. Same rules as ADR 0019: short
facts, taken from the user's own words or from what the user confirms, visible and erasable in the "Memory"
page, never sent to the cloud.

### 6. Projects and their memory (answer 5)

A project is created in the interface of Jarvis. It is **a local folder** and a name. There will also be a
project for home automation.

- The folder of a project can be the one where Claude Code (or another target) works during a hand-off: the
  project is then where the session is started.
- The memory of the project is **complementary**: it is added to that of the agent when the turn is about this
  project. It lives in the folder of the project, so that the external session can read it.
- **A folder without a project**: when a Claude session (followed by Jarvis, ADR 0021) makes a request in a
  folder that belongs to no project, Jarvis shows a request: assign it to an existing project, or create a new
  project. Jarvis never chooses in the user's place, and the request has no time limit (as in ADR 0023).
- Creating a project on a folder does not add it to the allowed folders without saying so: the page offers it,
  and the user confirms (ADR 0021).

### 7. What does not change

The decision point, the classes, the confirmation, the spend cap, the permission log. The announcement turn
(ADR 0024) goes through no agent.

### 8. Consequences

- Two calls to the local model per request instead of one, unless the agent is chosen by hand. To be measured
  on Benoit's PC: the delay before the first sound must not exceed today's for a simple request.
- The request sent is shorter: only the tools of the agent are in it.
- A wrong sorting gives an agent without the right tool: the agent must be able to say "this is not for me",
  and Jarvis then makes the turn again with "General".

### 9. Open points

1. How does a turn know which project it is about: a project chosen in the conversation, named by voice, or
   deduced by the sorting?
2. The memory of the project: a readable Markdown file in the folder (that Claude Code reads too), or the
   database of Jarvis?
3. The hand-off to Claude or ChatGPT: one more agent, or a tool of the "General" agent as today?
4. By voice, can the agent be named ("Jarvis, home automation: turn off Articulé") to skip the sorting?
