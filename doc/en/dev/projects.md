# Projects: a name, a folder, a memory, and the "Project" agent

Work items `stage2/19-projects` and `stage2/19-project-sessions`. Decision: [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 6, 10,
11 and 13 (point 2). The agents are described in [agents.md](agents.md), the conversation memory in
[conversation-memory.md](conversation-memory.md).

The first work item builds the projects, their page, their memory read from the folder, the "Project" agent and
the project of a turn. The second adds the request to assign to a project a folder that has none (when a Claude
Code session works there) and starting a Claude Code session in the folder of the project.
**Still to build:** Jarvis writing the memory of a project, then the projects view (ADR 0026).

## What is built

```
"Projects" page ──► ProjectCatalog (slice Projects/Catalog) ──► settings: section Projects.List
                         │   reads JARVIS.md through ILocalFiles (kernel)
                         ▼
Conversation/Dialogue ─► IProjectCatalog (kernel): Projects · Find(name) · NamedIn(request) · RecallAsync(project)
   │
   ├─ project of the turn: chosen in the window (typed request), else named in the sentence,
   │                       else named by the Jarvis agent in hand_to_agent(agent, project)
   └─ the agent that answers (but the Jarvis agent) gets the block "[Project]": name, folder, memory
```

The slices `Projects/Catalog`, `Agents/Catalog` and `Conversation/Dialogue` do not know each other: the kernel
contract `IProjectCatalog` links them. Without this slice, no turn is about a project.

## A project (`ProjectCatalog`)

- **A name and a local folder**, kept in the settings (`Projects.List`). 50 projects at most, a name of
  60 characters at most, with at least one letter or digit.
- **Two projects do not share a name**, whatever the case and the accents.
- The identity comes from the first name (`Kazo OS` gives `kazo-os`) and never changes.
- Removing a project leaves its folder and its memory file as they are.
- Creating a project writes no agent (section 13, point 2) and allows no folder (section 6).

## The memory of a project

**The file `JARVIS.md` at the root of the folder of the project.** It is readable Markdown, written by the user
or by a Claude Code session; Jarvis **reads** it at each turn about the project, keeping nothing between two
reads. Jarvis does not write it yet.

- Read through `ILocalFiles`, without going through the allowed folders: the user gave this folder to the
  project, and only this file is read. The tools `list_directory` and `read_file` keep their rule: the agent
  reads the rest of the folder only when the folder is allowed.
- Its share: one sixteenth of the context (`ProjectCatalog.Budget`). A longer file is cut, with "…".
- A bracket of the file becomes a parenthesis: it cannot close the block.
- Never sent to the cloud this way: the block goes to the local model, with the memory.

## The project of a turn (`ConversationEngine`)

In this order:

1. the project **chosen in the conversation window** ("Project" list, typed request only);
2. else the project **named in the sentence** ("In Kazo Os, …"), by whole words, the longest name first;
3. the Jarvis agent may name another one **when it hands over**: `hand_to_agent` has a `project` argument, whose
   values are the names of the projects. A name no project has changes nothing.

When a turn has a project, **the agent that answers gets the block "[Project]"** after its own memory: the name,
the folder, then the text of `JARVIS.md` or the sentence that says there is none. The Jarvis agent does not get
it: its request stays short, and it hands over. Without a recognized project, only the memory of the agent is
sent (section 10). The turn keeps the name of the project (`ConversationTurn.Project`), shown under the answer.

## The "Project" agent

A seventh starting agent, `project`: tools `list_directory`, `read_file` and `get_claude_sessions`. Its
instructions say to answer from the block and the files of the folder, and to ask which project when none is
given. The description of `hand_to_agent` adds one line with the names of the projects.

## The "Projects" page

After the "Agents" page. Each project shows its name, its folder, the start of its memory (or how to write
one), and whether Jarvis may read the folder. **"Allow this folder"** asks for a confirmation, then adds the
folder to the allowed folders (`IAllowedFolders.AllowAsync`): creating a project never does it alone.

## A folder with no project (`ProjectCatalog.Unassigned`)

When a Claude Code session Jarvis follows (a task of the board, `ITaskBoard`, that has not ended) works in a
folder that belongs to no project, the "Projects" page shows a request at its top:
**"A Claude session works in X, which belongs to no project."** The user answers in one of two ways:

- **assign the folder to a project that exists**: the folder joins the other folders of the project
  (`ProjectEntry.MoreFolders`, 20 at most). The memory stays in the main folder of the project;
- **create a project on that folder**, by giving it a name.

Jarvis never chooses, and the request has no time limit: it stays as long as a session works in the folder. A
folder belongs to a project when it is its folder, one of its other folders, or a sub-folder of one of them,
whatever the case and the direction of the slashes. A folder taken back from a project ("Remove this folder")
asks again.

## A Claude Code session in the folder of the project

When a turn is about a project and a work goes to Claude Code ("Ask Claude Code to fix the network driver of
Kazo OS", or the hand-off tool), the session starts **in the folder of the project**: `ClaudeTaskRequest.Folder`.
The question "on which folder?" is then not asked. The rule of the allowed folders does not change (ADR 0021):
if the folder of the project is not allowed, nothing starts and Jarvis says so, with the name of the folder and
the page where to allow it.

## Provisional choices of these work items

To be confirmed by Benoit:

1. The memory file is named `JARVIS.md`, at the root of the folder. Claude Code does not read it by itself: it
   will have to be told at the start of a session, or the file named from `CLAUDE.md`.
2. Jarvis reads the memory of a project and does not write it.
3. The memory file is read even when the folder is not among the allowed folders.
4. The Jarvis agent does not get the block of the project.
5. The project chosen in the window goes before the one the sentence names, and is forgotten when Jarvis stops.
6. The "Home automation" project of the ADR (section 6) is not created by default.
7. The memory of a project takes one sixteenth of the context, on top of the two other memories.
8. The request for a folder with no project shows **only on the "Projects" page**: neither the orb, nor the fan,
   nor the voice signals it yet.
9. "Assign to an existing project" adds the folder to the other folders of the project; the main folder and the
   memory do not move.
10. The request goes away when the sessions of the folder have ended, and there is no button to dismiss it.
11. The project of the turn goes before a folder named in the sentence to choose where the session starts.
12. The session started in the folder of a project is not told to read `JARVIS.md` yet.

## Checks on Windows

Nothing of this work item ran on Windows.

1. Create a project on a real folder (`C:\...`), with and without `JARVIS.md`; a relative path is refused.
2. "Allow this folder", then read a file of the project through the "Project" agent.
3. Does the local model name the project when it hands over? "In Kazo Os, what is left to do?"
4. Size of the request of the "Project" agent with a long `JARVIS.md`.
5. Start Claude Code by hand in a folder with no project: does the request appear on the "Projects" page, with
   the path as the hook writes it?
6. "Ask Claude Code to … in Kazo OS": does the session start in the folder of the project?
