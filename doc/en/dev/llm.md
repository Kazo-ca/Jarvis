# Language models

Work item `stage1/11-llm-providers` of the [stage 1 plan](plans/stage-1.md). Decision:
[ADR 0007](adr/0007-llm-providers-and-tool-format.md) (providers, `Microsoft.Extensions.AI` contract, settings
of the local model). User stories US1.8 (configure providers) and US1.9 (default local model).

## The kernel contract

`src/Kazo.Jarvis.Kernel/Llm/` depends only on the `Microsoft.Extensions.AI.Abstractions` package (10.11.1):

| Type | Role |
| --- | --- |
| `LlmProvider`, `LlmTuning` | A provider as an adapter receives it: kind, address, model, settings, key reference |
| `LlmProviderKind` | `Ollama`, `OpenAiCompatible`, `OpenAi`, `Anthropic`; `IsLocal()` and `RequiresKey()` |
| `ILlmConnector` | Gives an `IChatClient` per provider, tests the connection, says whether the model is in memory, preloads it |
| `LlmFailure`, `LlmException`, `LlmConnectionResult` | The cause of a failure (ADR 0007, section 8) |
| `IAssistantModel` | The default model: its client, its load state, `EnsureLoadedAsync` before a turn |
| `AssistantModelLoadChanged` | Event published when the loading of the model changes ("the model is loading") |

The conversation (work item 13) reads `IAssistantModel` without knowing the slice. The adapters never use
automatic function invocation: Jarvis keeps the tool loop (ADR 0007, section 2).

## The adapters

`src/Kazo.Jarvis.Adapters/Llm/`, hand-written on a single `HttpClient` (5 minute timeout until the first byte,
to cover loading a 9 GB model):

| Kind | Protocol | Notes |
| --- | --- | --- |
| `ollama` | `/api/chat`, NDJSON stream | `think: false` always sent, sent again without `think` if the model refuses it (the model is remembered); `keep_alive: -1`; `num_ctx`, `temperature`, `num_predict` |
| `openai-compatible`, `openai` | `/v1/chat/completions`, SSE | `v1/` added to an address without a path; `max_tokens` (local) or `max_completion_tokens` (OpenAI); tokens counted with `stream_options.include_usage` |
| `anthropic` | `/v1/messages`, SSE | `anthropic-version: 2026-10-21`, key in `x-api-key`, no `temperature` (refused by that version), system message in its own field |

- **Test the connection** lists the server's models (`/api/tags`, `/v1/models`) and checks the model is
  there. Ollama adds `:latest` to a name without a tag. An `openai-compatible` server that lists no model is
  taken at its word: `llama-server` serves the one it started with.
- **Named causes:** invalid address, server unreachable, model missing, key missing, key refused (401, 403),
  timed out, server error. Technical detail goes to the log; it never holds the key.
- **Preload** (Ollama only): an `/api/chat` without messages, with the same `num_ctx` as the chats, otherwise
  Ollama reloads the model at the first question. `/api/ps` says whether the model is in memory; the other
  kinds cannot tell.
- **Keys:** read from the vault at each request, under the reference `llm.<id>.apikey`. A cloud provider
  without a key fails before any network call.
- **Tools:** refused (`NotSupportedException`) until stage 2, with the first tool and the tolerant parser.

## The `Llm/Providers` slice

`src/Kazo.Jarvis.Features/Llm/Providers/`:

| File | Role |
| --- | --- |
| `LlmProvidersOptions.cs`, `LlmProviderSettings.cs` | Section `Llm.Providers` of `config.json`: the list, the id of the default provider, validation |
| `AssistantModelKeeper.cs` | Implements `IAssistantModel`; background service that preloads the model at start and after each settings change |
| `ProviderEditor.cs` | Logic of the page: draft, add, remove, test before saving, save |
| `LlmProvidersSettingsPage.razor` (+ `.css`, `.resx`) | The "Language models" page of the settings window |
| `LlmProvidersSlice.cs` | Registration |

- **Defaults:** one provider `local`, Ollama on `http://127.0.0.1:11434` with `qwen3:14b`, reasoning off,
  context 4096, temperature 0.3, preloaded and kept in memory (ADR 0007, section 5).
- **The default provider must be local at stage 1** (validation rule); a cloud provider is configured and
  tested, its "default" button is greyed out.
- **Preload:** at start, then after each change of the default provider. On failure (Docker starting Ollama
  after logon, for instance), new attempts after 15 s, 30 s, 1 min, 2 min and 5 min, then wait for a turn or a
  settings change.
- **Before a turn**, `EnsureLoadedAsync` asks Ollama whether the model is in memory. If it was freed,
  `AssistantModelLoadChanged(Loading)` is published (the conversation will say "the model is loading"), then
  the model is loaded again.
- **The page** tests the draft values, before saving, within 20 s. The server's model list feeds the model
  input. Changing a provider's kind takes the usual address and model of that kind.

The Web host uses `SimulatedLlmConnector`: no network request. An address ending in `.invalid` is
unreachable, one ending in `.slow` never answers, the key `refused` is refused, only `qwen3:14b`,
`qwen2.5:14b`, `default`, `claude-opus-5-5` and `gpt-5` exist.

## Provisional choices of this work item

ADR 0007 does not settle them; each is a one-line change:

1. **Default port 11434** (Ollama's), not 11435: on Benoit's PC the address is set once in the page. The
   first-run wizard (work item 14) may look for Ollama on both ports.
2. **Default model of a new Anthropic provider:** `claude-opus-5-5`. OpenAI has no proposed model: the test
   lists the account's models.
3. **Anthropic adapter written on `HttpClient`** rather than the official SDK: same code as the two others, no
   extra dependency. ADR 0007 left the choice open.
4. **Longest answer:** 16,000 tokens by default, setting `MaxOutputTokens` (required by Anthropic).
5. **Connection test timeout:** 20 s; timeout until the first byte of an answer: 5 min.
6. **Key of a provider:** always `llm.<id>.apikey`; the id never changes once created.

## Checks on Windows

To do at the next session with Benoit (the Web host and the simulated servers do not prove them):

1. Set the address of the `local` provider to `http://127.0.0.1:11435` (Ollama in Docker), "Test the
   connection": passes, lists the models (`local-ia-active`, `qwen3:14b`, `qwen2.5:14b`).
2. When Jarvis starts, the log shows "Default model qwen3:14b of provider local loaded in … ms" and
   `ollama ps` shows the model kept (`Forever`). Note the cold duration.
3. An `openai-compatible` provider on `http://127.0.0.1:8080` (`llama-server`): test passes, the served model
   name is in the list.
4. The causes: Ollama stopped (unreachable), model `llama9` (missing, with the list), wrong Anthropic key
   (refused). No key in the log nor in `config.json`.
5. Free the model (`ollama stop qwen3:14b`): at the next turn (work item 13), "the model is loading".
6. **Measure a fallback model** of 7 to 8 billion parameters (ADR 0007, section 5): time to the first sentence
   and GPU memory with Whisper loaded, to be proposed by the first-run wizard.
