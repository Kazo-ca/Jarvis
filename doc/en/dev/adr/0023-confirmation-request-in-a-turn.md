# ADR 0023: the confirmation request within a turn (completes 0005)

## Status

Accepted by Benoit on 2026-11-21 (work item `stage2/confirmation-adr`), with his three answers, repeated in
the "Benoit's answers" section. They differ from the recommendations: the ADR describes his choices.

- **Completes:** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md): one more condition and one
  more look. No phase and no transition of its table changes.
- **Applies:** [ADR 0009](0009-permission-and-confirmation-model.md), section 3 (what a request carries, the
  voice answer recognized by code, one agreement for one action), and
  [ADR 0017](0017-tools-classes-and-decision-point.md), section 2 (step 4 of the decision point).
- **Stories:** US2.10 (base) of the [design document](../plans/design.md).
- **Does not decide:** the permission requests of a Claude Code session
  ([ADR 0021](0021-claude-code-sessions.md)), nor the paired phone as a strong channel (stage 4).

## Context

- Since work item `stage2/3-tool-loop`, the decision point denies every "Sensitive" or "Critical" call:
  nobody can be asked.
- ADR 0009 says what a confirmation request is. It does not say how it fits in a turn: the state machine of
  ADR 0005 has only four phases, and the end of speech ends the turn there.
- The transcription slice only listens at the start of a spoken turn. A "yes" or "no" answer comes in the
  middle of a turn, while the tool loop waits.
- A confirmation request will also come from elsewhere than a turn: a Claude Code session (work item 12),
  later an autonomous task.

## Decision

### 1. One more condition, no transition changed

- The assistant state gains a fourth condition: **interaction required** (`PendingInteraction`). It
  carries the identifier of what waits and its kind: question, approval, confirmation or manual action.
  The confirmation request is the first kind built; the others will use it without a new ADR.
- Two input events: "request opened" sets the condition, "request closed" clears it. The machine stays a
  pure function. Only one request is open at a time; the others wait for their turn.
- **The phase does not change during a request.** A request born in a turn keeps the phase the turn
  is in: "Thinking" most of the time, "Speaking" when the model already started to speak before it called
  the tool. The tool loop waits. The question said and the answer listened to enter neither "Speaking"
  nor "Listening".
- An activation during a request (wake word, hotkey, click on the orb) remains the interruption of
  ADR 0005: the turn is cancelled, the request is closed by a denial, logged "interrupted".
- **Look** (answer 1): while the condition is set, the orb shows an "interaction required" look, the same
  for every kind. It comes before the look of the phase. The animation follows the rule of the active
  phases (ADR 0005, section 6).

### 2. What the user sees and hears

- A **confirmation window** opens near the orb: who asks, the summary in the assistant's language, the
  exact detail (tool name and arguments, nothing cut), the class, and the **Approve** and **Refuse**
  buttons. It closes with the request.
- For a request born from a **spoken** turn, Jarvis says the summary aloud, followed by the question ("Do
  you confirm?"). For the "Critical" class, the sentence says that voice is not enough: a click is needed.
- For a request born from a **typed** turn (answer 3): Jarvis only says that an interaction is required,
  without the summary or the question, and listens to no answer. The window says the rest. The form Benoit
  wants is "task X of project Y needs your interaction"; in a plain conversation, with no task and no
  project, the sentence is "Your request needs your interaction".
- The summary is written by code from the tool and its arguments, never by the model.

### 3. The voice answer

- It exists only for the "Sensitive" class, in a spoken turn, with the microphone not muted.
- Right after its question, Jarvis opens **a single listening**. It ends at the end of the sentence, like a
  request (same end-of-speech detection, same Whisper).
- **Code recognizes the answer**, not the model. The whole transcription, without punctuation or capitals,
  must be one of the entries of a closed list per language:
  - yes: "oui", "oui Jarvis", "d'accord", "confirme", "je confirme", "approuve", "vas-y", "yes",
    "yes Jarvis", "confirm", "I confirm", "approve", "go ahead";
  - no: "non", "non Jarvis", "annule", "refuse", "arrête", "no", "no Jarvis", "cancel", "deny", "stop".
- **Any other answer is a refusal** (ADR 0009). If nothing is heard, the request stays open: the click
  remains possible.
- The text transcribed during this listening never goes to the model.

### 4. No timeout, and the channels

- **No timeout** (answer 2): Jarvis never decides in place of the user. A request stays open until it is
  answered. It only closes otherwise by an interruption, which is also the user's gesture. The turn waits
  with it.
- Accepted channels: "Sensitive", voice or click; "Critical", click only. An agreement that arrives through
  a channel the class does not accept is rejected and logged.
- An agreement is valid for **one** action: that of the request, with its exact arguments.

### 5. Log

- The logged decision (ADR 0017) now carries the channel of the answer: `voice`, `click`, or none for an
  interruption.
- A "Permission log" page of the settings shows the decisions, most recent first.

### 6. Slices and contracts

- Kernel: the condition, the two events, the look; an "ask for a confirmation" contract the decision point
  calls, and the messages between slices (request opened, question said, answer heard, request closed).
- `Permissions/Confirmation`: the request, the recognized words, the window.
- `Permissions/Decision`: calls the contract at step 4, logs the channel, carries the log page.
- `Voice/Speech` says the question or the announcement; `Voice/Transcription` opens the listening of the answer. No slice
  references another.

## Benoit's answers

Questions asked in the conversation on 2026-11-21; his answers, the same day:

1. **What does the orb show during a confirmation request?** "An 'interaction required' mode: question,
   approval, confirmation or manual action of the user." The recommendation was a look of its own for the
   confirmation.
2. **How long does Jarvis wait for an answer?** "None. He never decides in place of the user." The
   recommendation was 30 seconds, adjustable.
3. **A typed request needs a confirmation: does Jarvis speak?** "Jarvis just says 'task X of project Y
   needs your interaction'." The recommendation was the window alone.

## Options set aside

- **Going through the "Speaking" then "Listening" phases for the question and the answer.** The table of
  ADR 0005 would have to change: today the end of speech ends the turn, and "nothing heard" cancels it.
- **Letting the model understand the answer.** Against ADR 0009: only a human approves, and code recognizes
  the agreement.
- **Looking for "yes" anywhere in the sentence.** "Yes, well no, wait" contains "yes".
- **Several listenings while the request is open.** A single listening, the one that follows the question
  (ADR 0009): a background noise ten seconds later must not count.
- **A timeout that denies without an answer** (30 seconds, adjustable). Set aside by Benoit: it would
  decide in place of the user.

## Consequences

- The transition table of ADR 0005 does not change; its tests remain valid.
- Without a timeout, a turn can stay in "Thinking" for as long as the user does not answer, and no other
  turn starts without interrupting it. This is intended: nothing is decided without the user.
- The task and project names of the announcement will come with the sessions and the tasks (work item 12
  and later); a request born outside a turn says nothing until they exist.
- A hesitant answer ("uh, yes") is a refusal: the user asks again or clicks. That is the price of a closed
  list.
- During a request, the orb does not show the microphone level as it does while listening: the window says
  that Jarvis is waiting.
- **Not verified:** that Whisper transcribes a lone "yes" or "no" well with Benoit's microphone, and that
  the speaker is not picked up by the microphone right after the question. To check on his PC; if a lone
  "yes" is badly recognized, the list is completed without a new ADR.
- The window "near the orb" asks the Windows host to place a window: to check on Windows.
