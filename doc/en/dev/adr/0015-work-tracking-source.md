# ADR 0015: work tracking source (GitHub Issues or Azure Boards) and mandatory link for commits and PRs

## Status

Proposed on 2026-11-19 (work item `stage1/work-tracking-adr`). Feature **for later**: nothing is built before
v2, together with Relais.

- **Source:** Benoit's request of 2026-11-19: be able to set a source for organizing and tracking tasks
  (GitHub Issues or Azure DevOps); an instruction given to every agent so that its PRs and commits are linked
  to a work item or an issue; for Azure DevOps, Azure Boards set up on the GitHub repository and
  `AB#{WorkItemId}` in commits and PR descriptions; the rule written into the agent's memory and the project's
  memory, with the user told to approve those changes; an option that forces a work item or an issue for every
  subject (feature) or task, which Jarvis can create, change and track.
- **Documentation checked on 2026-11-19:**
  [Azure Boards: link GitHub](https://learn.microsoft.com/en-us/azure/devops/boards/github/link-to-from-github)
  (page updated 2026-07-17),
  [connect Azure Boards to GitHub](https://learn.microsoft.com/en-us/azure/devops/boards/github/connect-to-github),
  [GitHub: link a PR to an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue).
- **Completes:** [ADR 0013](0013-relais-readiness.md) (Project → Subject → Task → Sub-task hierarchy,
  orchestrator, subject keys), [ADR 0004](0004-configuration-storage-and-secret-vault.md) (settings and vault),
  [ADR 0009](0009-permission-and-confirmation-model.md) (permissions).
- **Supersedes no ADR.**

## Context

- Relais (ADR 0013) tracks work as projects, subjects, tasks and sub-tasks, but that tracking lives in its own
  database. Benoit wants it to be able to live where the team already tracks work too: GitHub issues or
  Azure Boards work items.
- Agents (Claude Code, Codex, the local model) make commits and PRs. Without a link to an issue or a work
  item, nobody can find why a change was made, and the tracker state does not move on its own.
- What the documentation says about Azure Boards, which the agent instructions must follow:
  - **Prerequisites:** the [Azure Boards](https://github.com/apps/azure-boards) app installed on the GitHub
    repository, and the Azure Boards project connected to that repository. It takes the **Contributor** role on
    both sides. Without the connection, a link added by hand fails.
  - **Syntax:** `AB#{ID}` (for example `AB#125`) creates a link in a **commit message**, in a PR
    **description** or in a GitHub issue description. **It creates nothing in a PR title or in a comment.**
    Several work items: `AB#123, AB#124 and AB#126`. Removing `AB#` from a PR description removes the link.
  - **Changing state:** a word placed before `AB#`: `Fixes AB#123` (also `fix`, `fixed`) moves the work item to
    the first state of the *Resolved* category, or else *Completed*; a state name (`Closed AB#123`) moves it to
    that state if it exists, otherwise nothing changes. **These transitions apply only when the PR is merged
    into the default branch.** In `Fixes AB#123, AB#124`, only the first one changes state: the word must be
    repeated before each work item.
  - A PR opened from a branch created from the work item (*New GitHub branch* menu) is linked automatically.
  - Azure Boards **does not reject** a PR without `AB#`: Microsoft provides no such check.
- On GitHub Issues: `#123` (or `owner/repo#123`) in a commit or a PR makes a reference; `Fixes #123` (also
  `close`, `closes`, `closed`, `fix`, `fixed`, `resolve`, `resolves`, `resolved`) in a PR **description** or a
  commit message closes the issue when the PR or the commit reaches the **default branch**.

## Decision

### 1. One tracking source per Relais project

- A setting per **project** (the repository, as in ADR 0013): `tracking source` = `none` (default),
  `GitHub Issues` or `Azure Boards`.
- `GitHub Issues`: the repository itself, or another named repository.
- `Azure Boards`: the Azure DevOps organization, the project, and if needed the default area path and
  iteration. The setting checks, and shows, that the Azure Boards app is installed and the repository connected;
  otherwise it says what to do, with the link.
- Access tokens (GitHub token, Azure DevOps personal access token or Microsoft Entra sign-in) go to the vault
  (ADR 0004), never into the repository.

### 2. Mapping to the Relais hierarchy

| Relais | GitHub Issues | Azure Boards |
| --- | --- | --- |
| Project | repository | Azure DevOps project (and area path) |
| Subject | parent issue | *Feature* (or *Epic*, configurable) |
| Task | sub-issue | *User Story*, *Product Backlog Item* or *Issue* depending on the process (configurable) |
| Sub-task | sub-issue of the task, or nothing | *Task*, or nothing |

- The `issue:<n>` subject key of ADR 0013 is used for GitHub. For Azure Boards the `ab:<id>` key is added.
- Azure Boards states depend on the process (Agile, Scrum, Basic, or custom). Jarvis therefore relies not on
  state names but on **state categories** (*Proposed*, *In Progress*, *Resolved*, *Completed*, *Removed*),
  which the API returns for each work item type.

| Relais status | GitHub Issues | Azure Boards category |
| --- | --- | --- |
| `pending` | open | *Proposed* |
| `working`, `paused`, `idle`, `waiting_answer`, `waiting_approval`, `blocked` | open | *In Progress* |
| `done` | closed, *completed* | *Resolved*, then *Completed* on merge |
| `abandoned` | closed, *not planned* | *Removed* |

- The detail of the six active statuses is not pushed to the tracker (too noisy); a label or a field for
  `blocked` may be added in v2.

### 3. Instruction to agents: every commit and every PR is linked

When a source is set, Jarvis gives **every agent** it launches or that connects to Relais (Jarvis's own
Claude Code settings, `AGENTS.md` for Codex, system instruction of the local model) the following instruction,
adapted to the source:

- **Azure Boards (Azure DevOps projects only):**
  - every commit message contains `AB#<id>` of the task's work item;
  - the **description** of every PR contains `AB#<id>` (the title does not count, nor does a comment);
  - to close the work on merge: `Fixes AB#<id>` in the description, **the word repeated before each work
    item**; only the merge into the default branch changes the state;
  - preferably, the branch is created from the work item or named after it.
- **GitHub Issues:** simply the issue's `#<id>` (no `AB#`) in every commit message and in every PR
  description. To close the issue on merge, the PR that finishes it writes `Fixes #<id>`.
- **Without a work item or issue:** the agent does not commit without a link. It creates one if the option of
  section 4 allows it, otherwise it asks the human which one to use.
- **Memory:** the instruction also tells the agent to **write this rule into its own memory and into the
  project's memory**, and to **tell the user to approve those changes** (memory file, `CLAUDE.md`,
  `AGENTS.md`), so the agent always follows it, including in a session started outside Jarvis. Jarvis does
  not change a third-party tool's memory itself without that approval.

### 4. "Mandatory link" option and synchronization

- Per-project option, **off by default**: every subject and every task must have a linked issue or work item.
  A task without a link stays `pending` until it has one.
- With the option, Jarvis (or the subject's orchestrator) can **create**, **change** (title, description,
  parent, state) and **track** the issues and work items of its subjects and tasks. It never deletes anything:
  abandoned work moves to *Removed* or is closed as *not planned*.
- **Two-way synchronization:**
  - a status change of a Relais task updates the linked state according to section 2;
  - a change made in GitHub or Azure Boards (closing, reopening, new title) flows back to Relais. By default,
    Jarvis polls the source at a regular interval; GitHub webhooks and Azure DevOps service hooks need an
    address reachable from the Internet and will go through the tunnel of ADR 0013, section 8.
  - On conflict, the tracking source's state wins, and the subject's thread notes it.
- Classification under ADR 0009: creating or changing an issue or a work item in the source set for the
  project is a **reversible and expected** action of the option, with no confirmation. Any other destination
  (another repository, another Azure DevOps project) needs a confirmation.

### 5. Technical access

- One slice per source in `Kazo.Relais` (`Tracking/GitHubIssues/`, `Tracking/AzureBoards/`), behind a common
  contract in the Relais kernel. Jarvis sees only kernel messages, as for the rest of Relais.
- Access through each service's REST API, or through their MCP servers (the GitHub MCP server, Microsoft's
  Azure DevOps MCP server): chosen in v2.

### 6. What this ADR does not decide

- A continuous integration check that rejects a PR without a link (Azure Boards provides none; a third-party
  GitHub Action exists, not verified).
- Tracking in GitHub Projects (project fields and views), Jira or other tools.
- How links show in the task fan (ADR 0010) and on the phone.

## Consequences

- Nothing is built before v2. Until then, the stage 2 ADRs on Claude Code sessions keep room for the
  instruction of section 3 in Jarvis's own settings.
- The task shape of ADR 0013 gains, in v2, a "tracking link" field (source, identifier, URL).
- The subject classification rules of the Relais spec gain the `ab:<id>` key.
- The quality of the link rests on the instruction and on the agent's memory: without a continuous
  integration check, an agent that forgets it is not blocked.
