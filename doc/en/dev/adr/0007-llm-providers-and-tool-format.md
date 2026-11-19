# ADR 0007: LLM providers and tool format

## Status

Accepted by Benoit on 2026-11-19 (proposed on 2026-11-18, work item `spikes/11-stage-1-adrs`).

- **Sources:** decisions 13, 20 and 21 of the [design](../plans/design.md); stories US1.8, US1.9, US1.14 and
  US1.15; spikes E4, E5 and E7 ([results](../../../../spikes/e7-local-tool-calls/RESULTS.md)); decision 2 of
  the [spikes](../spikes.md#decisions-taken-on-november-18-2026) (`qwen3:14b` without thinking).
- **Completes:** [ADR 0004](0004-configuration-storage-and-secret-vault.md) (keys in the vault),
  [ADR 0006](0006-voice-chain.md) (the answer goes to synthesis sentence by sentence).
- **Completed by:** [ADR 0009](0009-permission-and-confirmation-model.md) (tool execution).

## Context

- The user configures one or several providers: Ollama, llama.cpp or any OpenAI-compatible server, Claude,
  ChatGPT. Address, port and model are free: at Benoit's, Ollama listens on 11435, not 11434 (US1.8).
- Spike E7: `qwen3:14b` with thinking turned off picks the right tool 42 times out of 42 through the
  official channel, in 0.9 s. With thinking on, it needs 5.5 s. `qwen2.5-coder` also picks the right tool,
  but writes it as JSON in the text 36 times out of 42.
- Spike E4: the first cold call takes 70 to 115 s, the time for Ollama to load the 9 GB model.
- Spike E5: with a 4096-token context, about 600 MiB of graphics memory remain.
- Stage 1 has no tools. The format is set now so that the contract does not change at stage 2.
- The official MCP C# SDK exposes the tools of an MCP server as `Microsoft.Extensions.AI` `AIFunction`
  objects (`McpClientTool` derives from it), according to its documentation read on 2026-11-18.

## Decision

### 1. Provider types

| Type | Protocol | Nature | Examples |
| --- | --- | --- | --- |
| `ollama` | Ollama native API (`/api/chat`) | Local | Ollama in Docker on port 11435 |
| `openai-compatible` | `/v1/chat/completions` | Local | llama.cpp (`llama-server`), LM Studio |
| `openai` | `/v1/chat/completions` | Cloud | ChatGPT |
| `anthropic` | Messages API | Cloud | Claude |

- A configured provider has a name, a type, a full address, a model, options, and when needed the reference
  of its key in the vault. Several providers of the same type can coexist.
- **"Test the connection"** calls the server and names the cause on failure: server unreachable, model
  missing, key rejected, timeout.
- **The default model of stage 1 is a local provider** (US1.9, decision 20). Cloud providers are configured
  and tested from stage 1; they only answer from stage 2, through the handoff and under the spending cap.

### 2. A single contract for all providers

- The kernel contract is that of the **`Microsoft.Extensions.AI`** abstractions: `IChatClient`, its
  messages, its options and `AIFunction` for tools. The kernel only references the abstractions package.
- Adapters are in `Adapters` and implement `IChatClient`:
  - `ollama`: written on `HttpClient`, as in spikes E4 and E7, to keep control of `think`, `keep_alive`,
    the context size and the lenient parser;
  - `openai-compatible` and `openai`: one adapter, written on `HttpClient`;
  - `anthropic`: written on `HttpClient`, or on the official SDK if it implements the same contract. The
    choice is made when the adapter is built.
- Reasons: it is the standard .NET contract, and the MCP tools of stage 2 plug into it without conversion.
- **Jarvis keeps the tool loop.** Automatic function invocation is not used: every call requested by the
  model goes through the permission model (ADR 0009) and is shown in the conversation (US2.1).
- Every answer is streamed and can be cancelled: an interruption (US1.12) stops generation.

### 3. Tool format

- A tool has a name, a description and a JSON schema of its parameters. Each adapter translates it into its
  provider's format.
- **Names:** in English, ASCII, lower case with underscores, stable over time (`get_datetime`,
  `hand_off_to_claude`), as in spike E7.
- **Descriptions:** in the assistant's language. Spike E7 proved them in French; English is checked at
  stage 2 with the same protocol.
- A tool result goes back to the model as **data**, never as an instruction (ADR 0009).
- The risk class of each tool is set by the tools ADR, at stage 2.

### 4. Lenient parser

- The local adapters (`ollama`, `openai-compatible`) accept a tool call written in the text.
- Strict rule, the one of spike E7: the **whole** answer must be a single JSON object naming a declared
  tool. Anything else is text.
- Consequence for streaming: if the answer starts with `{`, it is held until its end before being said or
  interpreted. Otherwise it goes to synthesis sentence by sentence.
- The option can be turned off per provider.

### 5. Default local model and its settings

| Setting | Starting value | Source |
| --- | --- | --- |
| Model | `qwen3:14b` | E7 |
| Thinking | **Off** (`think: false`) for any model that has it; adjustable per provider | E7 |
| Context | 4096 tokens; at most 8192 advised on a 12 GB card | E5 |
| Temperature | 0.3 | E4 |
| Loading | Preloaded when Jarvis starts and kept in memory while Jarvis runs | E4 |

- If the model has to be reloaded, for instance because Ollama released it for another one, the orb stays
  in thinking and the conversation says the model is loading.
- The setup wizard offers a smaller local model as a fallback (7 to 8 billion parameters). None was tried:
  the fallback model is chosen by a measurement at stage 1.

### 6. System message and history

- The system message is made of the **persona**, editable and resettable (US1.15,
  [ADR 0008](0008-languages-and-localization.md)), and of **fixed technical rules**: answer in the chosen
  language, write text meant to be spoken (no Markdown, no lists, short sentences), the current date and
  time.
- The history sent to the model is a window of the latest turns that fits in the context. Automatic
  summaries of older exchanges come at stage 2 (US2.4).

### 7. What is recorded for each answer

- The provider, the model, the input and output tokens, the duration. The conversation shows the model that
  answered each turn (US1.14).
- This data will feed the spending cap of stage 2 (US2.8).

### 8. Errors

- A failure names its cause and the corrective action (US1.17): server unreachable (check the address and
  the port), model missing (download it), key rejected, timeout.

## Consequences

- Adding a provider means writing an adapter. No slice changes.
- The kernel depends on a Microsoft abstractions package. It stays free of business logic and native
  dependencies ([ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md)).
- Three hand-written adapters have to be kept current when the APIs change. In exchange, the settings
  specific to voice (`think`, preloading, lenient parser) stay under control.
- Keeping the model loaded uses about 9.5 GB of graphics memory while Jarvis runs. Ollama may release it to
  load another model; Jarvis's next answer then waits for the reload.
- **Not verified by spike E7:** the full turn (call, result, final answer), the correctness of the
  arguments, and a long tool list like that of a Home Assistant MCP server. These points are checked at the
  start of stage 2.
- Benoit chose `Microsoft.Extensions.AI` rather than an in-house contract (question asked in the
  conversation, answered on 2026-11-19).
