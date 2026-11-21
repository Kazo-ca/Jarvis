# ADR 0023: the confirmation request within a turn (completes 0005)

## Status

Proposed on 2026-11-21 (work item `stage2/confirmation-adr`). Three questions are asked to Benoit in the
conversation; they are repeated in the "Questions to Benoit" section.

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

- The assistant state gains a fourth condition: **confirmation pending** (`PendingConfirmation`). It
  carries the identifier of the request and its class.
- Two input events: "request opened" sets the condition, "request closed" clears it. The machine stays a
  pure function. Only one request is open at a time; the others wait for their turn.
- **The phase does not change during a request.** A request born in a turn keeps the phase the turn
  is in: "Thinking" most of the time, "Speaking" when the model already started to speak before it called
  the tool. The tool loop waits. The question said and the answer listened to enter neither "Speaking"
  nor "Listening".
- An activation during a request (wake word, hotkey, click on the orb) remains the interruption of
  ADR 0005: the turn is cancelled, the request is closed by a denial, logged "interrupted".
- **Look** (question 1): while the condition is set, the orb shows a "confirmation" look, which comes
  before the look of the phase. The animation follows the rule of the active phases (ADR 0005, section 6).

### 2. What the user sees and hears

- A **confirmation window** opens near the orb: who asks, the summary in the assistant's language, the
  exact detail (tool name and arguments, nothing cut), the class, the time left, and the **Approve** and
  **Refuse** buttons. It closes with the request.
- For a request born from a **spoken** turn, Jarvis says the summary aloud, followed by the question ("Do
  you confirm?"). For the "Critical" class, the sentence says that voice is not enough: a click is needed.
- For a request born from a **typed** turn (question 3): the window only, without voice. Jarvis speaks only
  when spoken to.
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
- **Any other answer is a refusal** (ADR 0009). If nothing is heard, the request stays open until its
  timeout: the click remains possible.
- The text transcribed during this listening never goes to the model.

### 4. Timeout and channels

- Timeout of a request (question 2): **30 seconds**, adjustable from 10 to 120. Without an answer: denied,
  logged "timed out".
- Accepted channels: "Sensitive", voice or click; "Critical", click only. An agreement that arrives through
  a channel the class does not accept is rejected and logged.
- An agreement is valid for **one** action: that of the request, with its exact arguments.

### 5. Log

- The logged decision (ADR 0017) now carries the channel of the answer: `voice`, `click`, or none for a
  timeout or an interruption.
- A "Permission log" page of the settings shows the decisions, most recent first.

### 6. Slices and contracts

- Kernel: the condition, the two events, the look; an "ask for a confirmation" contract the decision point
  calls, and the messages between slices (request opened, question said, answer heard, request closed).
- `Permissions/Confirmation`: the request, its timeout, the recognized words, the window, the timeout
  setting.
- `Permissions/Decision`: calls the contract at step 4, logs the channel, carries the log page.
- `Voice/Speech` says the question; `Voice/Transcription` opens the listening of the answer. No slice
  references another.

## Questions to Benoit

Asked in the conversation on 2026-11-21. The recommendation is the one this ADR describes.

1. **What does the orb show during a confirmation request?** Recommended: a "confirmation" look of its own,
   plus the window. Other choice: the thinking look unchanged, the window alone.
2. **How long does Jarvis wait for an answer?** Recommended: 30 seconds, adjustable. Other choices:
   60 seconds, 15 seconds.
3. **A typed request needs a confirmation: does Jarvis speak?** Recommended: no, the window and the click
   only. Other choice: he also asks the question aloud and listens to the answer.

## Options set aside

- **Going through the "Speaking" then "Listening" phases for the question and the answer.** The table of
  ADR 0005 would have to change: today the end of speech ends the turn, and "nothing heard" cancels it.
- **Letting the model understand the answer.** Against ADR 0009: only a human approves, and code recognizes
  the agreement.
- **Looking for "yes" anywhere in the sentence.** "Yes, well no, wait" contains "yes".
- **Several listenings until the timeout.** A single listening, the one that follows the question
  (ADR 0009): a background noise ten seconds later must not count.

## Consequences

- The transition table of ADR 0005 does not change; its tests remain valid.
- A hesitant answer ("uh, yes") is a refusal: the user asks again or clicks. That is the price of a closed
  list.
- During a request, the orb does not show the microphone level as it does while listening: the window says
  that Jarvis is waiting.
- **Not verified:** that Whisper transcribes a lone "yes" or "no" well with Benoit's microphone, and that
  the speaker is not picked up by the microphone right after the question. To check on his PC; if a lone
  "yes" is badly recognized, the list is completed without a new ADR.
- The window "near the orb" asks the Windows host to place a window: to check on Windows.
