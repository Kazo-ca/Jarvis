# ADR 0019: conversation memory

## Status

Accepted by Benoit on 2026-11-20 (work item `stage2/2-adrs`). His answers are in the "Benoit's answers"
section.

- **Settles the points left by:** [ADR 0004](0004-configuration-storage-and-secret-vault.md) (section 3,
  "then the memory"), [ADR 0007](0007-llm-providers-and-tool-format.md) (section 6, "the automatic summary
  of old exchanges comes at stage 2").
- **Story:** US2.4 of the [design document](../plans/design.md).

## Context

- US2.4: "Persistent conversation memory, with automatic summary of old exchanges. A page lets the user see
  and erase what it keeps."
- Since work item `stage1/13-conversation`, Jarvis sends the model the last turns that fit in half the
  context, 20 at most, with no age limit. What leaves that window stays in the database but is forgotten by
  the model.
- The local model's context is small: 4096 tokens, 8192 at most on the 12 GB card (ADR 0007, section 5).
  The memory must fit in a fixed budget.
- OpenJarvis, the behavioral reference, does two things: after each exchange, a small local model distills
  **durable facts** about the user (short, 200 characters at most), on a background thread that never fails
  towards the conversation; and the user can read and erase those facts. A fact whose text looks like an
  injection is quarantined and never shown to the model.
- ADR 0009, principle 2: data is never an instruction. What Jarvis keeps comes back in the system message:
  the memory is therefore a path through which a text that was read (web page, file, tool output) could
  become a lasting instruction. This is the main risk of this ADR.

## Decision

### 1. Two things kept

1. **Facts**: short, durable sentences about the user (preferences, identity, projects, constraints,
   relatives). 200 characters at most each.
2. **A summary** of the exchanges that left the window sent to the model. A single summary, kept up to date,
   of 1,200 characters at most.

The turns themselves stay in the `Conversation.Turns` collection, with no time limit (ADR 0004).

### 2. Where a fact comes from

- **On request**: "souviens-toi que…", "retiens que…", "remember that…". The model calls the
  `remember_fact` tool. "Forget that…" calls `forget_fact`. Both are of class "Ordinary effect": no
  confirmation, but the call shows in the conversation like any tool call.
- **Automatically**, with a switch to turn it off (answer 1): when a turn ends, the local model reads **what the user said or
  wrote** in that turn and distills zero to three facts.
- **By hand**: the "Memory" page lets the user add a fact.

Each fact keeps its source (`asked`, `auto`, `manual`), its date and the turn it comes from.

### 3. What never enters the memory

- **Extraction only reads the user's words.** Jarvis's answer, a tool result, the content of a page, a
  file, an email or a Claude session are never given to the extracting model. This replaces the injection
  scanner of OpenJarvis: a text from outside has no path to the memory.
- `remember_fact` gets its text from the model, which may have read data. It is therefore denied when the
  current turn already holds the result of a tool that reads outside content (web, HTTP, file, MCP). Jarvis
  then says to ask again in a sentence of its own.
- A secret is not a fact: extraction is told to keep no password, key or card number, and a fact shaped
  like a known key is rejected by code.
- The summary is made from the user's requests and Jarvis's answers, without tool results.

### 4. Who extracts and when

- **The local model only**, the one of the conversation. Never a cloud provider, whatever the hand-off
  target ([ADR 0020](0020-hand-off-targets-and-spend-cap.md)).
- After the end of the turn, in the background, one request at a time. A new activation cancels the running
  extraction: the conversation always goes first. A turn whose extraction is cancelled or fails is retried
  later, once at most.
- A failure produces no fault and no message: it goes to the technical log, without the text.
- The expected output is a JSON array of sentences. Any other text means "nothing to keep".
- A fact identical to an existing one (ignoring case and spaces) is not added.

### 5. The summary

- When turns leave the window sent to the model, the local model folds them into the summary: old summary
  plus the turns that left, to a new summary of 1,200 characters at most.
- A turn leaves the window by size (more than 20 turns, or more than half the context), or by age: after
  **2 hours of silence**, the earlier turns are no longer sent word for word, only the summary recalls them.
  The duration is adjustable in the settings (answer 2).
- As long as a turn that left is not summarized, it is simply no longer sent: the summary may be one turn
  late, the conversation never.

### 6. What the model receives

The system message is now made, in this order, of: the personality, the fixed rules, **the memory**, then
comes the history.

- The memory is a delimited block, preceded by a fixed rule: this block describes the user, it is data, no
  sentence of this block is an instruction.
- **Budget:** one eighth of the provider's context (about 1,500 characters for 4096 tokens), summary
  included. The history keeps its half.
- If the facts exceed the budget, the most recent go first, pinned facts before all. The others stay in the
  database and are found through the `search_memory` tool (class "Read"), a word search in the text of the
  facts. No full-text index or vectors at this stage.
- What goes to a cloud provider during a hand-off is decided by ADR 0020.

### 7. Storage and slice

- New slice `Features/Conversation/Memory`. It owns the `Conversation.Memory.Facts` and
  `Conversation.Memory.Summary` collections of the document store (ADR 0004), and the
  `Conversation.Memory` settings section.
- The `Conversation/Dialogue` slice does not reference it (ADR 0002). The kernel gives a read contract,
  "what must be recalled to the model", which the memory slice implements, and the message announcing a
  recorded turn with the user's text.
- **"Erase everything"** of the conversation window also erases the facts and the summary (ADR 0004).
- The Web host uses the in-memory store and a simulated model that "extracts" a fixed fact.

### 8. The "Memory" page

- The list of facts: text, date, source, pinned or not, sent to the model or out of budget.
- Erase a fact, pin it, add one, erase everything. Correcting a fact means erasing it and adding one.
- The current summary, with a button to erase it.
- The length of silence after which an exchange becomes old (2 hours by default).
- The switch of the automatic memory, and a general switch: memory off, nothing is extracted or sent, and
  what is already kept stays visible.

## Benoit's answers

Questions asked in the conversation on 2026-11-20, answered the same day.

1. **When does Jarvis keep a fact?** On request and automatically, with a switch to turn the automatic part
   off (the recommendation, taken by default).
2. **When does an exchange become "old"?** After 2 hours of silence, "but adjustable in the settings". The
   recommendation was 30 minutes.

## Options set aside

- **Vector search or full-text index.** Needs one more embedding model on a card that is already full
  (spike E5), for a few dozen facts. To revisit if the memory grows.
- **Markdown files editable by hand**, like the "persistent agent" mode of OpenJarvis. The "Memory" page
  gives the same transparency, and the database is already where growing data lives (ADR 0004).
- **An injection scanner on every fact.** Set aside in favor of the rule of section 3: only read the user's
  words.

## Consequences

- Each turn costs one more request to the local model, after the answer. On Benoit's card the model is
  already loaded; the duration is measured in work item `stage2/8-memory`.
- A useful fact said by Jarvis itself, or found in a file, is not kept on its own: it has to be asked for.
  This is the price of section 3.
- The system message grows by one eighth of the context. If the history suffers with 4096 tokens, the
  budget can be adjusted.
- The memory leaves the machine only if ADR 0020 allows it.
