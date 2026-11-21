# Hand-off: targets, the `hand_off_to_claude` tool, "ask Claude"

Work item `stage2/9-hand-off` (US2.5, US2.6, US2.7). Decision:
[ADR 0020](adr/0020-hand-off-targets-and-spend-cap.md), sections 1 to 5 and 7. The spending cap (section 6) is
work item `stage2/10-spend-cap`. The full turn is described in [conversation.md](conversation.md), the tool loop
in [tools.md](tools.md), the providers in [llm.md](llm.md).

## What is built

```
"Hand-off" page ─► settings Llm.HandOff (target for questions, memory)
                                   │
Llm/Providers ─► ILlmProviderList (kernel) ─► HandOffTargets ─► ILlmConnector: client of the target
                                   │              │
                                   │              └─► tool registry: hand_off_to_claude,
                                   │                  present only while a target is set
                                   ▼
Conversation/Dialogue ─► IHandOff (kernel): Forced(request) · Read(arguments) · Open(kind)
   │
   ├─ "Ask Claude…" ─────────────────────────────────────► target, without the local model
   └─ local model ─► hand_off_to_claude(kind, reason) ───► target, which answers the user
```

The slices `Llm/HandOff`, `Llm/Providers` and `Conversation/Dialogue` do not know each other: two kernel
contracts link them.

## Targets (`HandOffTargets`, section `Llm.HandOff`)

| Setting | Default | Role |
| --- | --- | --- |
| `QuestionsProviderId` | empty | The cloud provider (`anthropic` or `openai`) that answers the questions handed over |
| `WithMemory` | no | Also send the kept facts and the summary ([conversation-memory.md](conversation-memory.md)) |

- **No target at first: nothing leaves for a cloud provider**, and the local model is not given the tool.
- Only a cloud provider can be a target. An id that names a local or removed provider means "no target".
- The client of the target follows the settings: changing the target, or the model of the chosen provider,
  closes the former client and opens a new one.
- The name said to the user comes from the kind of the provider: "Claude" for `anthropic`, "ChatGPT" for
  `openai`.
- **The target for tasks (Claude Code) does not exist yet**: it comes with the Claude Code sessions (ADR 0021,
  work item `stage2/12`). A hand-off of kind `task` is refused with its cause.

## The `hand_off_to_claude` tool (`HandOffTool`)

- A fixed name whatever the target; its description names the target. Origin `hand-off`, class "Read":
  choosing the target is the consent, no confirmation at each hand-off. On as soon as a target is set; the
  switch of the "Tools" page turns it off.
- Parameters: `kind` (`question` only for now) and `reason`, 200 characters at most.
- **A terminal tool.** The conversation recognizes it by its name: once the decision point allowed it, it has
  the target answer, and nothing goes back to the local model.

## Forcing the hand-off (`HandOffPhrases`)

At the start of the request, spoken or typed, recognized by code:

| Formula | Aims at |
| --- | --- |
| « Demande à Claude … », « Demandez à Claude … », "Ask Claude …" | the target for questions |
| « Demande à Claude Code … », "Ask Claude Code …" | the target for tasks |

- Tolerates "Claud", "Clode", « demande a », a leading "Jarvis,", case and punctuation.
- The formulas of both languages are tried whatever the detected language.
- What follows the formula is the request sent to the target. A formula with nothing after it is not a forced
  hand-off.
- The local model is neither loaded nor asked. If the target is missing or fails, Jarvis says the cause and
  **does not answer in its place**.

## What the conversation does (`ConversationEngine.HandOff.cs`)

1. `IHandOff.Open(kind)` gives the target, or the cause of its absence.
2. For a spoken request: what the local model had written, then the fixed sentence "I am asking Claude."
3. The target answers **in the same loop** as the local model (`RunAsync`): its text is shown and said
   sentence by sentence, its tool calls go through the same decision point.
4. The turn records the provider, the model and the tokens **of the target**; the `HandOff` entry of the turn
   keeps the kind, the reason, whether it was forced, the name of the target, the provider, model and tokens
   of the local model, the delay before the first text of the target and, when there is one, the cause of the
   failure.

### What leaves for the target

| Leaves | Does not leave |
| --- | --- |
| The personality and the fixed rules | The kept facts and the summary, unless `WithMemory` is ticked |
| The same window of history as the local model | The tool call of the local model and its reason |
| The request (for a forced hand-off: what follows the formula) | A secret, the content of the vault, a sound |
| The enabled tools whose declared class is "Read", without `hand_off_to_claude` | Any other tool |

A target that calls a tool it was not given is denied (`ToolDisabled`). The permissions log names the target
as the requester.

### When the target fails

| Case | What happens |
| --- | --- |
| By the tool | The result of the tool is the named cause ("Claude refused the key."); the local model says so and answers as well as it can |
| Forced hand-off | Jarvis says the cause; nobody answers in the place of the target |

Causes: no target, no target for tasks, and the eight causes of a provider ([llm.md](llm.md)). A target that
writes nothing counts as "server error". **Never a fault of the assistant**: no `TurnFailed`.

## Who answers

The conversation window shows the provider and the model of the turn, and a "Hand-off" line: "Handed over to
Claude: *reason*", "Asked Claude directly, at your request", or "The hand-off did not happen (*cause*)".
**Nothing changes on the orb** (ADR 0020, answer 2).

## "Hand-off" page

After "Language models". It first says what leaves the computer, then offers: the target for questions (the
list of the configured cloud providers), the memory box, the target for tasks (not available yet), the forcing
formulas. A change is saved at once.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `IHandOff` (`Kernel/Conversation`) | Does a request force the hand-off? What does a call of the tool say? Who answers? |
| `HandOffKind`, `HandOffRefusal`, `ForcedHandOff`, `HandOffCall`, `HandOffTarget`, `HandOffOpening` | The values of that contract |
| `ILlmProviderList` (`Kernel/Llm`) | The configured providers, for a slice that lets one be chosen |

## Web host

The simulated local model calls `hand_off_to_claude` on « question difficile » when the tool is offered. A
simulated cloud provider answers « Ici *model*, dans le cloud. Demande reçue : … Historique : N message(s).
Mémoire : oui|non. »: the tests see what the target received. A key `refused` is refused.

## Provisional choices of this work item

The ADR does not settle them; to be confirmed by Benoit:

1. **The reason (`reason`) is shown in the window, not said**: only the fixed sentence is said before the
   target.
2. **"I am asking Claude" is not written in the answer** of the turn: only said, the "Hand-off" line shows it.
3. **The name said comes from the kind of the provider** ("Claude", "ChatGPT"), not from the name typed in the
   settings.
4. **The formulas only name "Claude"**, even when the target is ChatGPT: no "ask ChatGPT".
5. **A formula with nothing after it** ("Ask Claude") goes to the local model.
6. **« de », « si », "to" after the formula stay in the request** sent ("to fix the build").
7. **The target has its own eight tool calls** at most, on top of those of the local model.
8. **After a hand-off by the tool that fails, the local model may ask again** in the same turn, within the
   bound of eight calls.
9. **`kind` only offers `question`** as long as the target for tasks does not exist; "Ask Claude Code" is
   recognized and refused with its cause.

## Known limits

- **No spending cap bounds the calls yet**: that is the next work item. The page says so. A provider without
  a price can be chosen as a target as long as prices do not exist.
- The history sent to the target is sized on the context of the local model, not on that of the target.

## Not verified

- **No call left Jarvis for the Anthropic or OpenAI API.** The cloud adapters date from work item
  `stage1/11-llm-providers` and never answered a turn.
- **Nothing ran on Windows.** The delay before the first sound of the target is not measured.
- The real `qwen3:14b` did not pick the tool inside Jarvis (spike E7 showed it apart).
- The formulas were not tried with Whisper.

## Tests

- Unit: `HandOffTests` (targets, client, registry, formulas, tool) and `ConversationEngineHandOffTests`
  (hand-off by the tool, forced hand-off, memory, tools of the target, failures, interruption).
- Playwright: `HandOffTests` (nothing without a target, and the page; a chosen target, the tool and "Ask
  Claude"; the memory box; a refused key).

## Windows checks

To do with Benoit, with a real key and a small amount:

1. Choose the target: the tool shows on the "Tools" page.
2. « Demande à Claude … » by voice: note the delay between the end of the sentence, "I am asking Claude" and
   the first sound of the answer (log: `first text of the target`).
3. A hard question without the formula, in both languages: does `qwen3:14b` pick the tool, and only when
   needed?
4. Does Whisper write "Claude", "Claud" or "Clode"? Add the variants heard.
5. A wrong key, then the network cut: the cause is said, the orb does not show a fault.
6. Interrupt the answer of the target with the wake word.
7. With an allowed folder: the target reads a file, and the window shows the call.
