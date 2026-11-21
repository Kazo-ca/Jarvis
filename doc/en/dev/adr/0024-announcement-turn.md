# ADR 0024: the announcement turn (completes 0005)

## Status

Proposed on 2026-11-21 (work item `stage2/13-announcement-turn-adr`). Waiting for Benoit's answer to the
question asked in the conversation.

- **Completes:** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md): one more row in the
  transition table. No phase and no look is added; no existing row changes.
- **Applies:** [ADR 0021](0021-claude-code-sessions.md), section 9 (Jarvis announces aloud, without
  interrupting the user or an answer in progress; during quiet hours the announcement stays in the
  conversation) and section 8 (at the end of a session, Jarvis says its summary).
- **Stories:** US2.12 of the [design document](../plans/design.md).
- **Does not decide:** what is announced, the per-folder setting or the quiet hours (ADR 0021, section 9);
  announcements of other origins (reminders, timers), which may go through the same turn.

## Context

Until now, Jarvis only speaks to answer: every sentence said belongs to a turn the user opened (ADR 0005),
and the question of a confirmation is said in the turn it was born in (ADR 0023). ADR 0021 asks for speech
nobody asked for: "Claude Code is done on Kazo Bar." It must wait until the assistant is at rest, show on the
orb, and be cut like an answer when the user speaks.

The transition table of ADR 0005 has no row for that. ADR 0005 says a pair missing from the table is ignored,
and that the table is the contract: adding a row takes an ADR.

## Decision

### 1. One more row in the table

| Phase | Event | New phase | Effect |
| --- | --- | --- | --- |
| Idle | Announcement submitted | Thinking | New turn, of input "announcement", without microphone and without model. |

- In any other phase, the submitted announcement is **ignored**: whoever submits it waits for the rest and
  submits it again (section 3).
- The announcement turn then follows the existing rows: "first sentence ready" moves it to "Speaking", "end
  of speech" brings it back to "Idle". The orb therefore shows the "speaking" look, with no new look.
- **Interruption**: an activation during an announcement cuts it at once and opens a listening turn, as for
  an answer (US1.12). The announcement cut is not said again.
- **The "fault" condition is not cleared** by the end of an announcement turn. A turn that succeeds clears
  the fault because it proves the microphone and the model work; an announcement proves neither.
- An announcement is also said while the "interaction required" condition is set (ADR 0023): "Claude Code
  waits for your permission" arrives at that very moment. It is also said with the microphone muted: muting
  the microphone is not silence.
- If the voice fails, the turn fails like any other ("fault" condition, cause of the voice).

### 2. What the user sees and hears

- An announcement is written in the conversation **as soon as it arrives**, said or not, as a line of Jarvis
  with no request above it. It is what remains of a silent announcement (quiet hours) or of one cut.
- An announcement is **not sent to the model** in the history of the next turns, nor kept by the
  conversation memory (ADR 0019): its text may come from a session, hence from data (ADR 0021, section 9).
- Announcements are said one by one, in order of arrival. An announcement that has waited for the rest for
  more than ten minutes is no longer said: it stays written. At most five wait; beyond that, the oldest ones
  are no longer said.

### 3. Slices and contracts

- The kernel carries two messages: `AnnouncementToSay` (the text, the language, aloud or not), published by
  any slice that has something to announce, and the input event `AnnouncementSubmitted` of the state machine.
- The conversation slice writes the announcement in its journal, waits for the rest, submits the
  announcement, then gives its sentence to the voice when the turn starts. It does not decide **whether** an
  announcement is said: whoever publishes it decides (per-folder setting, quiet hours).
- The voice slice does not change: it says the sentences of a turn.

## Question to Benoit

Asked in the conversation on 2026-11-21: how Jarvis says an announcement nobody asked for. The recommendation
is this ADR (an announcement turn). His answer will be repeated here when the ADR is accepted.

## Options set aside

- **Saying the announcement outside any turn**, like the question of a confirmation (ADR 0023). The question
  of a confirmation lives in a turn that already exists; an announcement has none. Without a turn, the orb
  does not show that Jarvis speaks, an activation during the announcement opens the microphone on Jarvis' own
  voice, and nothing cuts it.
- **An ordinary text turn** ("Text submitted"). The text would go to the local model, which would answer the
  announcement instead of saying it.
- **An "Announcement" phase.** One more phase asks for one more look and touches everyone who reads the
  phase, for a behaviour identical to "Speaking".
- **Interrupting an answer to announce.** ADR 0021 rules it out.

## Consequences

- The state machine keeps its four phases; its state also remembers that the current turn is an
  announcement, so as not to clear the fault at its end.
- The slices that react to "new turn" see a third input, "announcement": listening does not open, the model
  is not called.
- Work item 13 (session announcements) is the first to publish `AnnouncementToSay`. The end summary of a
  session started by Jarvis, left aside in work item 12, goes through it.
- To check on Windows: an announcement said while nothing happens, then cut by the wake word.
