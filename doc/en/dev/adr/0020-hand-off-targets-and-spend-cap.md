# ADR 0020: hand-off, targets and spend cap

## Status

Proposed on 2026-11-20 (work item `stage2/2-adrs`). Waiting for Benoit's answers to the questions of the
"Open questions" section.

- **Settles the points left by:** [ADR 0007](0007-llm-providers-and-tool-format.md) (sections 1 and 7:
  cloud providers "only answer from stage 2 on, through the hand-off and under the spend cap"),
  [ADR 0009](0009-permission-and-confirmation-model.md) (section 9: "the spend cap is described by the ADRs
  of stages 2 to 4").
- **Stories:** US2.5 to US2.8 of the [design document](../plans/design.md), decision 20.
- **Does not decide:** how a Claude Code session is started and followed. That is ADR 0021.

## Context

- Decision 20: the local model answers first and calls a "hand off" tool; the user can force the hand-off
  by voice; a monthly cap bounds the spending.
- Spike E7: `qwen3:14b` without thinking picks `hand_off_to_claude` reliably in French.
- Since work item `stage1/11-llm-providers`, the `anthropic` and `openai` providers can be configured and
  tested, but answer nothing. Every answer already records its provider, model, tokens sent and received
  and duration (ADR 0007, section 7).
- ADR 0009, section 9: a text leaves the machine only through an explicit choice, and the screen where that
  choice is made says so.
- The orb has seven looks set by ADR 0011. None says "Claude is answering".

## Decision

### 1. Two targets

| Target | Used for | What it is |
| --- | --- | --- |
| **Questions** | A question the local model cannot handle: long reasoning, specialized knowledge, careful writing | A cloud provider already configured (`anthropic` or `openai`), chosen by its name |
| **Tasks** | Work on files or in several steps | Claude Code, started by Jarvis (ADR 0021) |

- Settings section `Llm.HandOff`, "Hand-off" page of the settings. Each target may stay empty.
- **No target is set by default.** As long as the questions target is empty, nothing goes to a cloud
  provider. The screen where it is chosen says what will leave the machine (section 4).
- An empty target removes its use from the tool: the local model is not offered what it cannot do.

### 2. The `hand_off_to_claude` tool

- Name set by ADR 0007 and spike E7. It serves both targets, even when the questions target is ChatGPT: a
  tool name is stable, its description says who it hands off to.
- Parameters: `kind` (`question` or `task`) and `reason`, a short sentence said to the user.
- Class "Read" for `question`: choosing the target is the consent, there is no confirmation at each
  hand-off. The class of `task` is the one ADR 0021 sets.
- **It is a terminal tool.** For `question`, the target's answer is spoken straight to the user, sentence
  by sentence: it does not go back through the local model.
- Before the target's first sound, Jarvis says a fixed, translated sentence naming the target ("I am asking
  Claude."). The delay before the first sound is measured in work item `stage2/9-hand-off`.
- **Target failure** (key refused, server unreachable, timeout, cap reached): the tool result is the named
  cause, given back to the local model as data. It tells the user and answers as best it can. This is not
  an assistant fault.

### 3. Forcing the hand-off by voice

- "Demande à Claude…", "ask Claude…" at the start of a request send the rest of the sentence to the
  questions target, without going through the local model (US2.6). "Demande à Claude Code…", "ask Claude
  Code…" aim at the tasks target.
- **Recognized by code**, not by the model: a closed list of phrases per language, compared with the start
  of the transcription, tolerant to Whisper's variants of the name ("Claude", "Claud", "Clode").
- A request written in the conversation window follows the same rule.
- If the target is empty or the cap reached, Jarvis says so and does not answer in its place.

### 4. What goes to the questions target

- A system message of its own for the cloud: the personality and the fixed rules (language, text made to be
  spoken).
- The request, and the same history window as the local model, so that the question can be understood.
- **The kept facts and the summary of the memory** ([ADR 0019](0019-conversation-memory.md)): depending on
  question 1.
- The enabled tools of class "Read", through the same loop and the same decision point as the local model
  (ADR 0007, section 2). `hand_off_to_claude` is not given to the target.
- Never: a secret, the content of the vault, a sound.

### 5. Who answers

- The conversation already shows the provider and model of each turn (US1.14). A turn handed to a target
  also shows the reason of the hand-off.
- On the orb: depending on question 2. The seven looks of ADR 0011 do not change.

### 6. Spend cap

- **A monthly cap, in US dollars**, the billing currency of the APIs. The month is the calendar month, in
  local time.
- **What is counted:** every answer of a cloud provider, from the tokens already recorded, multiplied by
  the price of the model. Claude Code sessions: depending on question 4.
- **Prices:** per configured provider, two adjustable numbers (input and output, per million tokens). The
  application proposes the prices known at its release date; it never fetches them from the network. A
  cloud provider without a price cannot be chosen as a target.
- **Before each call**, code compares the month's spending with the cap. At the cap, the call does not
  happen. The possible overshoot is therefore bounded by a single answer (16,000 tokens at most,
  [llm.md](../llm.md)).
- **At 80% of the cap**, Jarvis says so once in the month.
- **At the cap**, Jarvis says so and stays local (US2.8): the tool is no longer offered for `question`
  until the next month or until the user raises the cap.
- The counter is visible on the "Hand-off" page and in the conversation window.
- Cloud voices (ElevenLabs, OpenAI) are not counted at this stage: they are billed per character and are
  set apart. The page says so.
- Default amount: depending on question 3.

### 7. Slices

- `Features/Llm/HandOff`: the targets, their page, the tool, the forcing phrases.
- `Features/Llm/Spend`: the prices, the cap, the counter, the `Llm.Spend.Entries` collection.
- They do not reference each other (ADR 0002): the kernel carries the message "answer of a provider
  recorded" (provider, model, tokens) and the contract "is this call allowed by the cap?".

## Open questions

1. **Does the memory go to the cloud target with the question?**
   - A. No: the request and the recent conversation only. A setting lets the user add it.
     **(Recommended:** what Jarvis knows about you stays on the PC until you choose otherwise.**)**
   - B. Yes, always: Claude answers better when it knows you.
   - C. The request alone, without the recent conversation. Questions that build on the current exchange
     will be misunderstood.
2. **How does the orb say that Claude is answering?**
   - A. A small name under the orb ("Claude") during the thinking and speaking of that turn.
     **(Recommended:** readable at a glance, without touching the validated drawing.**)**
   - B. Nothing on the orb: the conversation window and the sentence "I am asking Claude" are enough.
   - C. A color of its own for the target. Needs a new ADR on the orb and a mockup.
3. **Default monthly cap.**
   - A. US$10. **(Recommended.)**
   - B. US$25.
   - C. US$5.
   - D. No default amount: the cloud stays blocked until you enter a cap.
4. **Do Claude Code sessions count towards the cap?** Claude Code reports a cost at the end of each
   session, even when it runs on a subscription where that cost is not billed.
   - A. A setting "Claude Code is paid by subscription": checked, the reported cost is shown apart and does
     not count; unchecked, it counts. Checked by default. **(Recommended if you use Claude Code with your
     subscription.)**
   - B. Always counted.
   - C. Never counted.

## Options set aside

- **Having the local model rephrase the cloud's answer.** Doubles the delay and damages the answer.
- **Fetching prices from the network.** One more outbound connection, and a format that changes.
- **A confirmation at each hand-off.** The cap and the explicit choice of the target are enough; one more
  question each time would make the hand-off unusable by voice.

## Consequences

- With the default configuration, nothing leaves: no target, no hand-off.
- The cost shown is an estimate from tokens and entered prices. The provider's invoice is what counts.
- The proposed prices age with the application. An update refreshes them; the user can correct them.
- A question handed to the cloud takes the recent conversation along: the target's screen says so in full.
