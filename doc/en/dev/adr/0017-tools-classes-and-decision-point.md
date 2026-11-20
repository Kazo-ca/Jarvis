# ADR 0017: basic tools, class of each tool and permission decision point

## Status

Proposed on 2026-11-20 (work item `stage2/2-adrs`). The open questions are in the "Questions for Benoit"
section.

- **Settles the points left by:** [ADR 0007](0007-llm-providers-and-tool-format.md) (section 3: "the risk
  class of each tool is set by the tools ADR, at stage 2"),
  [ADR 0009](0009-permission-and-confirmation-model.md) (sections 2 and 10: the class of each tool, and the
  decision point "built with its first tool").
- **Stories:** US2.1 and the base of US2.10 of the [design document](../plans/design.md).
- **Spikes:** E7 and E13 ([summary](../spikes.md)): `qwen3:14b` without thinking picks the right tool 42 times
  out of 42 in French and 117 times out of 117 with English descriptions.
- **Does not decide:** the tools of an MCP server and Home Assistant
  ([ADR 0018](0018-mcp-client-and-home-assistant.md)), the hand-off
  ([ADR 0020](0020-hand-off-targets-and-spend-cap.md)), Claude Code sessions
  ([ADR 0021](0021-claude-code-sessions.md)), the shape of the confirmation request (ADR 0009, section 3,
  built in work item `stage2/4-confirmation`).

## Context

- ADR 0007 sets the format of a tool (stable English name, description in the assistant's language, JSON
  schema) and says Jarvis keeps the tool loop: no call runs automatically.
- ADR 0009 sets five action classes and four principles, among them "the model proposes, the code decides"
  and "denied by default". It does not say where that code lives nor which class each tool has.
- Spike E14: with Home Assistant, the class of a call depends on the targeted device, not on the tool name.
  The class is therefore decided **per call**, with its arguments.
- Spike E12: a Claude Code session asks a local Jarvis tool for its permissions. That request must follow the
  same path as one from a tool of the local model.
- The local model's context is 4096 tokens (ADR 0007, section 5). The tool list and the tool results go into
  it.

## Decision

### 1. What a tool is for Jarvis

- The model receives `AIFunction`s (ADR 0007, section 2). In the kernel, each tool of the registry also
  carries:
  - its **origin**: `builtin`, `mcp:<server>` or `hand-off`;
  - its **descriptions per language** (for a built-in tool; an MCP tool keeps its server's);
  - its **classifier**: code that receives the arguments of a call and returns an ADR 0009 class;
  - its **activation setting** (section 6).
- The **tool registry** is a kernel contract. Each slice registers its tools there; the
  `Conversation/Dialogue` slice reads the list to give the model from it. No slice references another
  ([ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md)).
- **Names in English, descriptions in the assistant's language**: the ADR 0007 rule is confirmed by spike
  E13. Changing the assistant's language changes the descriptions on the next turn.

### 2. A single decision point

- Kernel contract: "is this action allowed?". It receives **who** asks (tool, session or task), **what**
  (exact name and arguments), and returns "allowed" or "denied, with the cause".
- Its implementation is a slice, `Permissions/Decision`. **Every action a model asks for goes through it**:
  a tool call from the local model, a tool call from a cloud target (ADR 0020, section 4), a permission
  request from a Claude Code session launched by Jarvis (ADR 0021).
- How a decision runs:
  1. is the tool known and enabled? If not: denied;
  2. the tool's classifier returns the class of **this call**;
  3. "Read" and "Everyday effect": allowed;
  4. "Sensitive" and "Critical": confirmation request (ADR 0009, section 3), on the channels the class
     accepts; the human answer, or the timeout, decides;
  5. "Forbidden": denied, without a question.
- **Denied by default at every step:** unreadable arguments, a classifier that fails, a tool without a
  classifier, timeout, interruption by the user. A classifier can never return a class lower than the one
  set by the tables of this ADR and of ADRs 0018 and 0021.
- **Every decision is logged** in `jarvis.db`: date, requester, tool, arguments (truncated), class, result,
  channel of the answer if there is one. Secret values never go in.
- A denial goes back to the model as the tool's result, so as **data**: the cause in one sentence. The model
  tells the user; it cannot ask for the same action again in the same turn.

### 3. Built-in tools of stage 2

| Tool | What it does | Class | Enabled by default | Work item |
| --- | --- | --- | --- | --- |
| `get_datetime` | Date, time and time zone of the PC | Read | Yes | 3 |
| `calculate` | Computes an arithmetic expression, in code (no script execution) | Read | Yes | 3 |
| `list_directory` | Lists an allowed folder | Read | Yes (with no allowed folder, it sees nothing) | 5 |
| `read_file` | Text of a file in an allowed folder | Read | Yes (same) | 5 |
| `web_search` | Web search through the chosen provider | Read | **No**: the query leaves the machine | 5 |
| `fetch_url` | Reads a page with `GET` | Read | **No**: the address leaves the machine | 5 |
| `hand_off_to_claude` | Hands off (ADR 0020) | By `kind` (ADRs 0020 and 0021) | Only if a target is set | 9 |

- **No built-in tool writes a file or runs a command at stage 2.** A write goes through a Claude Code
  session (ADR 0021), where every write is of class "Sensitive".
- `read_file` and `list_directory` outside an allowed folder: class "Forbidden", logged denial. The path is
  compared after normalization (ADR 0009, section 5). They also refuse, everywhere, Claude Code's
  `sessions/*.key` and `ide/*.lock`.
- `fetch_url`: `http` and `https` only, `GET` method only, no header or body chosen by the model, redirects
  followed under the same rule. **Addresses of the machine and of the local network are refused** (loopback,
  private ranges, link-local), so that a booby-trapped page cannot make the model read Home Assistant,
  Jarvis's local server or a router. A list of exceptions is set by hand.
- `web_search`: behind a kernel contract, so other providers can be added. The first one is that of
  question 1. Its key goes to the vault ([ADR 0004](0004-configuration-storage-and-secret-vault.md)).
- The screen that enables `web_search` or `fetch_url` says what leaves the machine (ADR 0009, section 9), and
  both go into the acceptance list of outbound connections.

### 4. The tool loop

- The `Conversation/Dialogue` slice runs the calls **one by one**, in the order asked, each through the
  decision point.
- **Bounds, adjustable:** at most 8 tool calls per turn; 30 s per call; result returned to the model
  truncated to 2,000 characters, with a note that it is truncated. At the call bound, the model answers with
  what it has.
- A tool's result is data (ADR 0009, principle 2): it is returned in the "tool" role, never in the system
  message.
- The orb stays in thinking during a call. An interruption (US1.12) cancels the current call and the
  following ones; the cancellation is a denial for the log.
- **The list given to the model is that of the enabled tools**, no more: a disabled tool, or one whose target
  is missing, is not offered. The tools page shows an estimate of the list size, in tokens, next to the
  context size.

### 5. What the conversation shows

- Each call shows in the conversation window, within the turn: tool name, arguments, then result or cause of
  the denial, collapsed by default (US2.1).
- The call, its result and its duration are recorded with the turn. The spoken text never reads the
  arguments: only the model speaks, except for a confirmation request.

### 6. Activation

- Settings section `Tools`, "Tools" page of the settings: one switch per tool, with its class and origin.
- A tool that makes something leave the machine is **disabled by default**. So is a tool from an MCP server
  (ADR 0018).
- The user can make a tool **stricter** (for instance ask a confirmation for `fetch_url`), never less strict
  than its class.

### 7. Slices

- `Permissions/Decision`: the decision point and the log.
- `Tools/Clock`, `Tools/Calculator`, `Tools/Files`, `Tools/Web`: one tool or family per slice, with its
  options and page.
- `Tools/Catalog`: the "Tools" page.
- The kernel carries the contracts: tool and registry, decision point, classes, web search. It carries no
  classification logic.

## Questions for Benoit

Asked in the conversation on 2026-11-20.

1. **Web search provider.** The plan kept Brave Search "with a free offer to check". Checked on 2026-11-20 on
   Brave's pricing page: there is no separate free offer any more; the "Search" plan is prepaid, US$5 per
   1,000 requests, with US$5 of credit given every month (about 1,000 searches). According to an article of
   2026-06-08, a credit card is asked at sign-up. Recommendation: keep Brave Search, disabled by default, the
   key supplied by the user.
   **Benoit asks in return** (2026-11-20) whether the search can be done in a browser visible to the user,
   driven by something like Playwright. The question stays open; this ADR is only accepted once it is
   settled.

## Options set aside

- **A fixed class per tool.** Does not hold for Home Assistant (spike E14) nor for a file path.
- **Letting the model ask for the confirmation itself.** Against principle 1 of ADR 0009.
- **A file-writing tool as early as stage 2.** Claude Code does it better, with the exact differences to
  confirm.
- **`fetch_url` open to the local network.** A web page could make the model read an internal service.

## Consequences

- An injection in a page or a file can make the model ask for a tool; it cannot change the class of the call,
  nor approve it.
- The per-call class costs a little code per tool; in exchange, Home Assistant, files and Claude Code follow
  the same rule.
- 2,000 characters per result is little for a web page: `fetch_url` returns the start of the useful text. A
  summary of a whole page by the local model would need another ADR.
- With the default configuration, no tool makes anything leave the machine.
