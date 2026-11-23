# Spending cap: prices, count of the month, cap

Work item `stage2/10-spend-cap` (US2.8). Decision:
[ADR 0020](adr/0020-hand-off-targets-and-spend-cap.md), sections 6 and 7. The hand-off, the only path by which
a cloud provider answers, is described in [hand-off.md](hand-off.md).

## What is built

```
"Spending" page ─► settings Llm.Spend (cap, price per provider)
                          │
Conversation/Dialogue ─► ProviderAnswered (kernel) ─► SpendLedger ─► settings Llm.SpendCount (count of the month)
   after each request of the target                     │      └─► collection Llm.SpendEntries (detail)
                                                        ▼
Llm/HandOff ─► ISpendCap (kernel): Check(provider) · Status · TakeNotice() · Changed
   before each hand-off, and before each later request of the target
```

The slices `Llm/Spend`, `Llm/HandOff` and `Conversation/Dialogue` do not know each other: one message and one
contract of the kernel link them (ADR 0020, section 7).

## Settings (`Llm.Spend`)

| Setting | Default | Role |
| --- | --- | --- |
| `MonthlyCapUsd` | 10 | The cap of the month, in US dollars. Zero: no cloud call at all |
| `Prices` | empty | By provider id: dollars per million tokens sent and written |

- **A cloud provider without a price is never called**: its cost could not be counted. The "Hand-off" page
  greys it out in the list of targets.
- `KnownPrices` proposes the prices known at the publication date (Anthropic's price list of November 3, 2026,
  by start of the model id). **Nothing is fetched from the network.** No price is proposed for OpenAI: the user
  types it.
- A price is its two amounts: nothing is saved while one is missing.

## Counting (`SpendLedger`)

- The conversation publishes `ProviderAnswered` (provider, model, tokens, time) **after each request of the
  target**, not only at the end of the turn: a target that calls tools makes several requests. Nothing of what
  was said is in this message. The local model is never announced.
- Cost = tokens sent × input price + tokens written × output price, at the price of that moment.
- The count is that of the **calendar month in local time**. It is kept in the settings (`Llm.SpendCount`:
  month, amount); the detail of each answer (time, provider, model, tokens, cost) in the collection
  `Llm.SpendEntries`.
- An answer without a token count, or from a provider without a price, is not counted; the log says so.
- On the first day of the month the count starts again. The month is looked at every ten minutes, so the tool
  comes back without a restart.

## Capping

- **Before each hand-off**, `HandOffTargets.Open` asks `ISpendCap.Check`: without a price or at the cap, the
  call does not happen. The conversation asks again **before each later request of the target**: the possible
  overrun is bounded by one answer.
- **At the cap or without a price, the tool `hand_off_to_claude` leaves the registry**: the local model is not
  offered what would be refused. It comes back when the cap is raised, the price typed, or the month changed.
  The choice of the target is kept.
- "Ask Claude…" at the cap: Jarvis says the cause and does not answer in the place of the target.
- A target stopped between two requests: what it wrote stays, the cause is said, what was spent is counted.

| Cause (`HandOffRefusal`) | What Jarvis says |
| --- | --- |
| `NoPrice` | "The hand-off did not happen: the target has no price…" |
| `CapReached` | "The hand-off did not happen: this month's cloud spending cap is reached…" |

## Warning

After the answer of the target, one sentence of Jarvis, written in the answer and said when the turn is
spoken, **once**:

- when the answer passes 80 % of the cap: "By the way, more than 80 % of this month's cloud spending cap is
  used."
- when the answer reaches the cap: "This month's cloud spending cap is now reached: I stay local…"

An answer that jumps from under 80 % to the cap only says the cap.

## Where the count shows

- "Spending" page (after "Hand-off"): spent out of the cap, the month, the last 20 counted answers, the cap,
  the prices, "Put the count back to zero" with a confirmation, and what the count is not.
- "Hand-off" page: the line "Cloud this month".
- Conversation window: the same line, once something was spent.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `ISpendCap` (`Kernel/Llm`) | Is this call allowed? The count and the cap. What must be told once |
| `SpendVerdict`, `SpendNotice`, `SpendStatus` | The values of this contract |
| `ProviderAnswered` (`Kernel/Messages/Llm`) | An answer of a cloud provider ended: provider, model, tokens |
| `HandOffRefusal.NoPrice`, `CapReached`; `IHandOff.Notice` | The two causes of refusal, and the sentence to say after a hand-off |

## Differences from ADR 0020

- **The collection is named `Llm.SpendEntries`**, not `Llm.Spend.Entries`: a collection name has one dot only
  ([storage.md](storage.md)).
- **The setting "Claude Code is paid by subscription"** (checked by default) and the cap of each session
  (`--max-budget-usd`) came with the Claude Code sessions: see
  [claude-code-tasks.md](claude-code-tasks.md#cost-llmspend).

## Provisional choices of this work item

The ADR does not settle them; to be confirmed by Benoit:

1. **"Erase everything" in the conversation window does not put the count back to zero**: the count is in the
   settings. Only the detail of the counted answers goes with the documents.
2. **A button "Put the count back to zero"** exists on the page, with a confirmation.
3. **The warning is said after the answer of the target**, in the same turn, not apart.
4. **The count shows in the conversation window only once something was spent.**
5. **A pending warning is lost if Jarvis restarts** before the next hand-off; the refusal at the cap always
   says the cause.
6. **The price of a removed provider stays in the settings**: a provider added again with the same id finds it.

## Known limits

- **The count is an estimate.** Cached tokens, discounts and taxes are not known. The bill of the provider is
  what counts. The page says so.
- The cloud voices are not counted (ADR 0020, section 6). The page says so.
- An answer that is interrupted, or fails on the way, is not counted: the provider did not give its tokens. It
  may still be billed.
- The calls of the automatic memory go to the local model: nothing to count.

## Not verified

- **No call left for the API of Anthropic or OpenAI**: the tokens the real adapters return while streaming were
  not compared with a bill.
- **Nothing ran on Windows.**
- The proposed prices come from a dated price list; they were not read again on the provider's site.

## Web host

The simulated cloud provider returns 42 tokens sent and 18 written per answer. The tests reach the cap by
typing a price of a million dollars per million tokens: 60 USD per answer.

## Tests

- Unit: `SpendTests` (default cap, price required, cost, 80 %, cap, next month, restart, reset, bounds, known
  prices), `HandOffTests` (tool withdrawn without a price and at the cap, sentence to say),
  `ConversationEngineHandOffTests` (one announcement per request, refusal before and between two requests,
  sentence after the answer).
- Playwright: `SpendTests` (page; provider without a price; count and reset; 80 %, cap, raising it; cap out of
  bounds).

## Windows checks

To do with Benoit, with a real key:

1. Type the proposed price for Claude, ask a question with "Ask Claude": the count goes up, and the amount
   looks like the one in the provider's console.
2. A question where the target reads a file: two counted answers.
3. Cap at one cent: the next answer says the cap is reached, then "Ask Claude" is refused by voice, and the
   tool has left the "Tools" page.
4. Raise the cap: the tool comes back without a restart.
5. Restart Jarvis: the count is still there.
