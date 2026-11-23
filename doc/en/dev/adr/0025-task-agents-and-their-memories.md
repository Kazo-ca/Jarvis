# ADR 0025: one agent per kind of task, its instructions and its memories

## Status

Accepted by Benoit on 2026-11-22 (proposed on 2026-11-21, work item `stage2/adr-0025-task-agents`), from his
five answers of 21 November during the stage 2 acceptance, his answers of the same evening to the four open
points, the Jarvis agent and the "Call" mode (sections 9 to 12), and his "ok" of 22 November to the last three
points (section 13).

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
| Project | those needed to work in the folder of a project: files, Claude Code sessions | loaded with the folder and the memory of the project asked for (section 13, point 2) |
| Jarvis (general) | clock, calculation, hand-off, the tools of Jarvis itself | everyday conversation; does the sorting and decides to hand over (sections 4 and 9) |

The tools of a generic MCP server go to the agent the user chooses on the page of the server; the Jarvis agent
by default.

### 4. Sorting (answer 2, and section 13, point 1)

**The Jarvis agent does the sorting itself.** It gets the request, with its own tools and the list of the other
agents (name and description). It answers a simple request itself: one call to the model. For the rest, it
hands over: it names an agent, and the turn goes on with that agent, its instructions, its tools and its
memories. No silent sorting call comes before it.

- Handing over is a tool of the Jarvis agent: the name of a known agent, and the project when there is one
  (section 10). An unknown name is refused, and the Jarvis agent keeps the turn.
- The request of the Jarvis agent stays short: its tools and one line per agent, never the tools of the others.
- **In the conversation window, the user can choose the agent**: the turn then goes straight to that agent,
  without going through the Jarvis agent. The choice holds until the user removes it.
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

- A simple request keeps a single call to the local model. A request handed over to another agent makes two.
  To be measured on Benoit's PC: the delay before the first sound must not exceed today's for a simple
  request.
- The request sent is shorter: only the tools of the loaded agent are in it.
- A wrong sorting gives an agent without the right tool: the agent must be able to say "this is not for me",
  and the Jarvis agent then takes over again.

### 9. The Jarvis agent

There is an agent for Jarvis itself, with the tools of Jarvis. By voice, **it is always the one that gets the
request and decides to hand over** to another agent. It says so: to "In Kazo Os, what is left for us to do?",
it answers something like "Ok, I will check that. I am handing over to the agent that takes care of Kazo OS."

- By voice, an agent is not named to skip the Jarvis agent (point 4, answer B). In the conversation window,
  choosing the agent by hand stays possible (section 4).
- Outside the "Call" mode, the Jarvis agent takes over again at the end of the turn.

### 10. Which project a turn is about (point 1, answer A)

When it hands over, the Jarvis agent names the agent **and the project** together; the project can also be chosen in the
conversation, or named in the sentence ("In Kazo Os, …"). Without a recognized project, only the memory of the
agent is sent.

### 11. Where the project memory and the hand-off live (points 2 and 3, answers A)

- The memory of a project is **a readable Markdown file in the folder of the project**, that Claude Code reads
  too.
- The hand-off to Claude or ChatGPT stays **a tool**, as today (ADR 0020), not one more agent.

### 12. The "Call" mode

A sustained conversation, on the phone or in the application.

- **It starts** when the user clicks the "Call" button, or says something like "We need to talk about such a
  project" or "such a problem".
- **During the call, the agent changes and stays**: the one of the project or of the subject keeps the turn
  from one turn to the next, until the user asks to go back to Jarvis.
- **During the call, Jarvis listens to what follows without "Hey Jarvis"**, as on the phone (section 13,
  point 3). The microphone mute and the orb keep their rules (ADR 0005): the orb shows listening as long as it
  lasts.
- **It ends** when the user says goodbye.

Calls in the Android application are stage 5 of the design document; this section only sets what the "Call"
mode means for the agents.

### 13. The last three points (Benoit's "ok" of 2026-11-22, the three recommendations)

1. **The Jarvis agent does the sorting itself**: it answers simple requests and hands over for the rest. No
   silent sorting call comes before it (section 4).
2. **"The agent that takes care of Kazo OS" is one "Project" agent**, loaded with the folder and the memory of
   the project asked for. There is no agent file per project: creating a project requires writing no agent
   (section 3).
3. **During a call, Jarvis listens to what follows without "Hey Jarvis"**, until "goodbye" (section 12).
