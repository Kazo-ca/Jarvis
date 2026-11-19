# ADR 0013: preparing the Relais module (v2): projects, subjects, tasks and orchestrator

## Status

Proposed on 2026-11-19 (work item `stage1/relais-architecture-adr`). Waiting for Benoit's agreement.

- **Sources:** Benoit's request of 2026-11-19: plug an MCP server and hooks into Claude or ChatGPT, let the
  models talk to each other while the human sees the exchanges, not coded yet (probably v2), but making sure
  the architecture follows the Project (repository) → Subject → Task → Sub-task structure with an
  orchestrator; his specification "Module Relais dans le harness .NET" of the same day (kept outside the
  repository, called **the spec** here); decisions 21, 22 and 36 of the [design](../plans/design.md); stories
  US2.9, US2.11, US2.12, US2.15 and US3.2; spikes E2 and E8 ([summary](../spikes.md)).
- **Completes:** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (slices and kernel),
  [ADR 0003](0003-hosts-and-native-services.md) (Windows host and embedded server),
  [ADR 0004](0004-configuration-storage-and-secret-vault.md) (storage),
  [ADR 0009](0009-permission-and-confirmation-model.md) (permissions),
  [ADR 0010](0010-task-fan-and-orb-click.md) (task fan).
- **Supersedes no ADR.** The points where v2 will have to supersede one are listed in section 9.

## Context

- The spec makes Relais a building block of the "harness": a bus where humans and AIs talk (channels,
  messages, presence, questions, approvals), which also tracks work as **Project → Subject → Task →
  Sub-task**. Each subject has an **orchestrator**: an agent session that follows the whole subject. Agents
  (Claude Code, Codex, ChatGPT, Claude.ai, a local model) connect through `/mcp` and `/hook`.
- For this repository, **the harness is Kazo.Jarvis**: its Windows host is already a WPF application that
  runs all the time (tray icon, starts with Windows).
- The spec was written for a generic .NET harness. Four of its choices clash with accepted ADRs:
  - it splits Relais by layer (`Core`, `Data`, `Hosting`), while code must be in vertical slices
    (ADR 0002);
  - it puts the harness views in native WPF (`TreeView`), while in Jarvis every UI is a Razor component of a
    slice and the host holds no feature (ADR 0002, ADR 0003);
  - it stores everything in PostgreSQL, while Jarvis keeps its data in SQLite (ADR 0004);
  - ChatGPT and Claude.ai web reach `/mcp` only through a tunnel from the Internet, while security rule 7 of
    the design exposes nothing on the Internet. Benoit decided on 2026-11-19 (section 8).
- Stages 2 and 3 will build, before Relais, things Relais will take over: the list of Claude Code sessions
  through hooks (US2.11, spike E2), the sessions launched by Jarvis (US2.9, ADR 0009 section 7), the
  background tasks shown in the fan (ADR 0010). If they take another shape, v2 will have to rewrite them.
- Nothing is built by this ADR. It sets what must stay true until then, one cheap change for stage 1 and
  constraints for the stage 2 ADRs.

## Decision

### 1. What Relais is for Jarvis

- Relais is a **separate module**: the bus, its data, its `/mcp`, `/hook`, `/api` entry points and its
  SignalR hub. It does not know Jarvis.
- Jarvis is a **client** of the bus, through the spec's `IRelaisClient` interface, and nothing else. It can
  also be an agent on it: its local model can be the operator of a task or the orchestrator of a subject,
  under an agent account of its own.
- The hierarchy, the rules of tasks and sub-tasks, the orchestrator, questions, attribution and MCP tools
  are **those of the spec**. This ADR does not repeat them; it says how Jarvis prepares for them.

### 2. Projects of the solution

The spec's split is kept where it serves hosting (what runs where), and replaced by vertical slices inside:

| Project | Content | References | Replaces in the spec |
| --- | --- | --- | --- |
| `src/Kazo.Relais` | All the slices of the bus, plus `AddRelais` / `MapRelais` and `InProcessRelaisClient` | `Kazo.Relais.Client`, ASP.NET Core, Npgsql | `Relais.Core`, `Relais.Data`, `Relais.Pki`, `Relais.GitHub`, `Relais.Hosting` |
| `src/Kazo.Relais.Client` | `IRelaisClient`, the event and request records, `RemoteRelaisClient` | SignalR client only | `Relais.Client` |
| `src/Kazo.Relais.Host` | Executable of the bus as a separate process (`UseWindowsService()` in service mode) | `Kazo.Relais` | `Relais.Host` |
| `tests/Kazo.Relais.Tests` | Slice tests and **contract tests** run against both clients | all three | (PoC 3) |

- **A Relais slice** is a folder `Kazo.Relais/<Domain>/<Slice>/` holding its request and handler, its SQL,
  its MCP tool, its `/hook` or `/api` route and its tests (same path). Examples: `Tasks/StartTask/`,
  `Tasks/CreateSubtask/`, `Questions/AskQuestion/`, `Questions/RelayAnswer/`,
  `Orchestration/ClaimOrchestrator/`, `Subjects/ClosePrSubject/` (GitHub App). The internal CA is a `Pki/`
  slice like the others.
- The rules of ADR 0002 apply inside `Kazo.Relais`: a slice does not reference another one, they talk
  through messages, and what serves three slices moves to a small `Kazo.Relais/Kernel/` (identifiers,
  statuses, connection access).
- **Direction of dependencies:** `Kazo.Relais*` references no `Kazo.Jarvis.*` project. On the Jarvis side,
  only `Kazo.Jarvis.Adapters` references `Kazo.Relais.Client`; the Windows host references `Kazo.Relais`
  only if the embedded mode is chosen (section 4). `Kernel` and `Features` never see Relais.
- The projects live **in this repository**, under `src/` and `tests/`. If Relais must serve another harness
  (Kazo Bar, for example), it will move to its own repository; a new ADR will say so.
- The architecture tests of ADR 0002 gain these rules the day the projects are created.

### 3. Jarvis side: a kernel contract, one slice per view

- The Jarvis kernel gets its own bus contract, reduced to what the slices use, and messages
  (`Kernel/Messages/Relais/`) that mirror the spec's events (`TaskChanged`, `QuestionAsked`,
  `ApprovalRequested`, `OrchestratorChanged`, `ConnectionStateChanged`…). It names no type of
  `Kazo.Relais.Client` (ADR 0002, section 4). The copy is deliberate: the boundary stays the one of ADR 0002.
- `Kazo.Jarvis.Adapters` implements it with `IRelaisClient`. It reads the `SubscribeAsync` stream in a
  background task and turns it into kernel messages, **batched every 100 ms** (rule of the spec). The Web
  host provides a simulated, deterministic bus for Playwright.
- The views are **Razor slices** of the `Features/Relais/` domain: Project → Subject → Task → Sub-task tree,
  thread of a subject or a task, question queue, approval queue, orchestrator of the subject. No native WPF.
  For Jarvis, this answers the spec's open question about the discussion thread: `BlazorWebView`, already
  used everywhere.
- Screen updates go through Blazor's rendering thread (`InvokeAsync`), never through the stream-reading
  thread.

### 4. Hosting modes

- The three modes of the spec remain possible: **A** embedded in the Windows host, **B** companion process,
  **C** Windows service. The choice is made in v2, by proofs of concept 1 and 2 of the spec.
- What already weighs, for the record: Jarvis runs all the time, which makes mode A cheaper than elsewhere;
  but it also loads the wake word, Whisper and speech synthesis in its process (ADR 0006), which answers
  "yes" to the spec's first open question and pushes towards B or C.
- The "one process, one instance" rule of ADR 0003 applies to Jarvis. In mode B or C, `Kazo.Relais.Host` is
  another program, not a second instance of Jarvis.
- **The bus has its own `WebApplication`**, separate from the embedded server of ADR 0003 (mobile,
  webhooks): no shared injection container, its own port, listening on `localhost` only by default. The
  embedded server is therefore not blocked by Relais, and Relais does not depend on it.
- **Prerequisite from stage 1** (work item `stage1/4-hosts`): the Windows host is built on the .NET generic
  host (`Host.CreateApplicationBuilder`). Its background services start after the orb is shown, with
  `StartAsync` without blocking the UI thread; on "Quit", they stop with `StopAsync` within at most 10
  seconds, then the application closes. Mode A is then just one more background service, and the embedded
  server of stage 2 or 4 benefits too.

### 5. Storage

- Relais keeps **PostgreSQL** (prerequisite of the spec), in its own database. Its data does not go through
  the document store of ADR 0004 and does not go into `jarvis.db`.
- **Relais is optional.** Jarvis without Relais needs neither PostgreSQL nor an extra port: everything
  described by ADRs 0001 to 0012 works on its own.
- On Benoit's PC, PostgreSQL can run in Docker, like Ollama. The choice is made in v2.
- The agent tokens and the human token that Jarvis uses on the client side go to its vault (ADR 0004). The
  key of the internal CA stays with Relais.

### 6. Jarvis tasks take the Relais shape from stage 2

This is the point that avoids a rewrite. Everything stages 2 and 3 show as a "running task" (Claude Code
sessions, delegated tasks, fan panels) uses **the shape and vocabulary of Relais**, even while Relais does
not exist:

- **Vocabulary.** In docs and code, *project*, *subject*, *task*, *sub-task*, *operator*, *orchestrator* and
  *supervisor* have the meaning of the spec. An **autonomous task** of stage 3 (scheduled or triggered)
  keeps that compound name and is not a Relais task; what it launches (a Claude Code session, for example)
  is one.
- **Shape of a task:** identifier, parent task (empty for a task, set for a sub-task, one level only),
  subject, project, title, summary, operator (account and session), source (`harness`, `hook`, `mcp`,
  `orchestrator`), dates.
- **Statuses:** the nine of the spec, and no others: `pending`, `working`, `paused`, `idle`,
  `waiting_answer`, `waiting_approval`, `blocked`, `done`, `abandoned`.
- **Project key:** the Git remote URL of the folder, normalized; otherwise the folder name. The session list
  (US2.11) groups by project, not by raw path.
- **Subject key:** the classification rules of the spec (`pr:<n>`, `issue:<n>`, `branch:<name>`, `_inbox`
  for `main` or without Git). At stage 2, without the GitHub App, the `pr:<n>` key is not found yet: the
  branch is enough.
- **Fan (ADR 0010):** a panel shows one task; its sub-tasks show inside the panel, not as panels. A
  `waiting_answer` or `waiting_approval` task is **amber**.
- The stage 2 ADRs on Claude Code sessions and on the fan take this section over.

### 7. Hooks and MCP from stage 2

- Tracking of Claude Code sessions at stage 2 (US2.11, US2.12, spike E2) uses **the same hooks** as Relais:
  `SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `Stop`, and `SubagentStop` for sub-tasks. They
  call a local route named after the spec (`/hook/task.context`, `/hook/task.start`,
  `/hook/approval.requested`, `/hook/task.idle`). In v2, only the receiver changes.
- **Sessions launched by Jarvis (US2.9):** the task is created before the launch, and its identifier and
  the session's are passed as environment variables (`RELAIS_TASK_ID`, `RELAIS_SESSION_ID` in v2). The hooks
  see the identifier and create nothing. These hooks go into Jarvis's own Claude Code settings (ADR 0009,
  section 7).
- **Sessions launched by hand:** their hooks go into the user's settings. Installing and removing them is
  described by the stage 2 ADR on Claude Code sessions.
- Codex: `notify` to `/hook/task.idle` and `start_task` through an instruction in `AGENTS.md`, as in the
  spec.

### 8. Security: what the spec and ADR 0009 share, and what is left to decide

- **Agreement:** the author of a message (`user` or `agent`) is set by the server, only a human decides an
  approval, an orchestrator cannot. This is principle 4 of ADR 0009.
- **For Jarvis's model, a message from the bus is data** (principle 2 of ADR 0009). An order from an
  orchestrator or another agent can steer the work of a task; it can never grant a permission or change the
  class of an action.
- **A Relais approval request is shown like an ADR 0009 confirmation request**: amber panel in the fan,
  buttons near the orb, a click on the PC as the strong channel.
- **Network:** the bus listens on `localhost` by default; local network and VPN are an option.
- **Access from the Internet: a tunnel for the whole bus** (Benoit's choice on the card of 2026-11-19,
  over "`/mcp` only" and "local agents only"). ChatGPT and Claude.ai web reach an MCP server only from the
  Internet; the tunnel also serves remote machines and humans. Rules set for v2:
  - **closed by default**, opened by a setting that clearly says the bus becomes reachable from the
    Internet;
  - `/mcp`, `/hook`, `/api` and the SignalR hub all go through the tunnel; **no route is served there
    without authentication**: one token per agent account, a token or strong authentication for a human,
    each revocable on its own;
  - the embedded server of Jarvis (mobile, webhooks, ADR 0003) **does not go through this tunnel**: it stays
    on the local network and the VPN;
  - the tunnel provider, the certificate shown to Internet clients and request rate limiting are set by the
    v2 ADR.
- **Accepted risk:** the attack surface is much larger than with `/mcp` only. A leaked token gives access to
  the threads of every subject that account can read. Access logging and fast revocation are part of v2.

### 9. What v2 will have to change in accepted ADRs

For the record; a new ADR will do it at the start of the v2 series:

- **ADR 0009, section 7, and US2.15:** today, Jarvis never answers a permission request of a session it did
  not launch. With the Relais `PermissionRequest` hook, the human will be able to decide it from Jarvis. A
  human still decides, but the rule changes.
- **Security rule 7 of the design** ("listens only on the local network and the VPN"): the Relais bus is an
  exception when its tunnel is open (section 8). The embedded server of Jarvis stays under the rule.
- **ADR 0004:** PostgreSQL added as the database of the Relais module.

### 10. Benoit's answers of 2026-11-19

- **An answer said aloud** to a `user` question is posted as `user`: the human is speaking. It never counts
  as agreement to an approval, which still follows ADR 0009 (channels and classes).
- **The orchestrator of a subject is a setting**, and the default is **Jarvis's local model**. It speaks under
  its agent account, never as `user`.

### 11. What this ADR does not decide

- The hosting mode chosen (proofs of concept 1 and 2 of the spec).
- Putting an orchestrator to sleep when it has no active task (open question of the spec).
- How subjects and tasks are shown on the phone (mobile ADR, stage 4).
- The other open questions of the spec.

## Consequences

- Nothing is built before v2, except the generic host of section 4, in work item `stage1/4-hosts`.
- The stage 2 ADRs (Claude Code sessions, fan, embedded server) get three more constraints: the shape of
  tasks, the hooks and the separate port of the bus. In exchange, v2 adds Relais without rewriting what
  exists.
- Jarvis keeps a second bus contract in its kernel, copied from `IRelaisClient`. That is the price of the
  ADR 0002 boundary.
- The spec's `Core` / `Data` / `Hosting` split is not taken over: in its place, slices in `Kazo.Relais`. The
  hosting modes and the `IRelaisClient` contract do not change.
- PostgreSQL becomes a dependency of Relais, not of Jarvis.
