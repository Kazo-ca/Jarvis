# Confirmation request

Work item `stage2/4-confirmation` (base of US2.10). Decisions:
[ADR 0009](adr/0009-permission-and-confirmation-model.md) (principle 2),
[ADR 0017](adr/0017-tools-classes-and-decision-point.md) (section 2) and
[ADR 0023](adr/0023-confirmation-request-in-a-turn.md), accepted with Benoit's three answers: "interaction
required" look, no timeout, spoken announcement for a typed request.

## What is built

```
ConversationEngine ─► IPermissionDecision (Permissions/Decision)
                          │ class "Sensitive" or "Critical"
                          ▼
                      IConfirmation (kernel) ◄── ConfirmationDesk (Permissions/Confirmation)
                                                   │ InteractionOpened / InteractionClosed ─► state (condition, orb look)
                                                   │ confirmation window (Approve, Refuse)
                                                   │ ConfirmationQuestionToSay ─► Voice/Speech says the question (spoken turn) or the announcement (typed turn)
                                                   │ spoken turn: ConfirmationQuestionSaid ─► Voice/Transcription, one listening
                                                   ◄─ ConfirmationAnswerHeard (text heard) ── closed list of words
```

Only a human answer approves. The model never sees the question or the answer: it gets the result of the
tool, or one sentence that says why the call is denied.

## Kernel contracts

| Type | Role |
| --- | --- |
| `IConfirmation` | "Ask the user to approve this action": returns approved, refused or interrupted, with the channel; waits as long as it takes |
| `ConfirmationRequest`, `ConfirmationOutcome`, `ConfirmationAnswer`, `ConfirmationChannels` | The request and its answer; channels `voice` and `click` |
| `ActionContext` | The turn the action is asked in, and whether it was spoken; carried by `ActionRequest.Context` |
| `PendingInteraction`, `InteractionKind` | The "interaction required" condition of `AssistantState.Interaction`: identifier and kind (question, approval, confirmation, manual action) |
| `InteractionOpened`, `InteractionClosed` | The two inputs of the state machine: they set and clear the condition, and touch neither the phase nor the turn |
| `ConfirmationQuestionToSay`, `ConfirmationQuestionSaid`, `ConfirmationAnswerHeard` | The messages between slices: sentence to say, sentence said, answer heard |
| `AssistantLook.InteractionRequired` | The eighth look, the same for every kind; it comes before the look of the phase and before any other condition |
| `ITool.Summarize` | The sentence that says what the call would do, written by the tool; without it, a general sentence that names the tool |

## Slice `Permissions/Confirmation`

`ConfirmationDesk` holds the requests, **one at a time**; the next ones wait.

1. It sets the condition, opens the confirmation window and publishes the sentence to say. **Spoken**
   turn: the summary, then "Do you confirm?". **Typed** turn: "Your request needs your interaction."
2. It waits for a click, a recognized spoken answer or an interruption. **No timeout**: Jarvis never
   decides in place of the user.
3. It closes the window and clears the condition, whatever the outcome.

| Case | What happens |
| --- | --- |
| Typed turn | Jarvis only says that an interaction is required; the window says the rest; click only. |
| Spoken turn, "Sensitive", microphone not muted | Question said, one listening, answer by voice or click. |
| Spoken turn, "Sensitive", microphone muted | Question said ("Approve or refuse in the window"), click only. |
| Spoken turn, "Critical" | The sentence says voice is not enough; click only. |
| Window closed by the user | Refusal. The desk hears it from `IDialogService.Closed`, not from the end of the component: a view that goes away (a page of the Web host leaving, a content built again for another language) answers nothing. |
| No answer | The request stays open, the turn waits. |
| Request outside a turn | The window only, nothing is said. |
| Wake word, hotkey or click on the orb | The turn is cancelled (ADR 0005), the request closes, cause `Interrupted`. |

- **Recognized words** (`ConfirmationWords`): the whole transcription, without punctuation or capitals, must
  be one entry of the list of the assistant's language. French: "oui", "oui Jarvis", "d'accord",
  "confirme", "je confirme", "approuve", "vas-y". English: "yes", "yes Jarvis", "confirm", "I confirm",
  "approve", "go ahead". **Any other answer is a refusal**, including "yes, well no". Nothing heard: the
  request stays open. The "Confirmations" page of the settings shows the words that approve.
- The text heard is compared to the list and then dropped: no log, no model.

## What the other slices do

- **`Permissions/Decision`** calls `IConfirmation` at step 4. It checks the channel again: "Critical" is
  approved by a click only, whatever the confirmation slice answers. An agreement without a channel is not
  an agreement. The log carries the channel (`voice`, `click`, or nothing). Without `IConfirmation` in the
  host: denied (`ConfirmationUnavailable`). The **"Permission log"** page of the settings shows the 200 most
  recent decisions of the last 90 days.
- **`Voice/Speech`** says the question or the announcement in the queue of the turn, so with the voice of
  the turn and after what the model already said. It is not a sentence of the answer: no
  `FirstSentenceReady`, the phase does not change. A request closed during the sentence cuts it, not the
  turn. In a typed turn the conversation ends the turn itself; the slice then only closes its queue. A
  broken voice does not fail the turn for this sentence: the window still asks.
- **`Voice/Transcription`** opens **a single listening** after the question (same end of speech, same
  Whisper). It publishes no turn message; what it hears only goes out as `ConfirmationAnswerHeard`.
- **`Assistant/Orb`**: "interaction required" look, amber, slow rotation and pulse (0.6 in high quality, 0.7 in the
  light version).
- **`Conversation/Dialogue`** gives the turn and its mode (spoken or typed) to the decision point. The 30 s
  limit of a call does not count the wait for the confirmation, which has no limit.

## Web host

Two dummy tools, to show the request before a real tool needs one: `write_note` ("Sensitive", "write a
note …") and `erase_notes` ("Critical", "erase the notes"). They keep their notes in memory. The "Phrase to
say" field and the "Say this phrase" button make a chosen text "heard": a request, or the answer to the
question.

## Provisional choices of this work item

The ADR does not settle them; to be confirmed by Benoit:

1. **Closing the confirmation window is a refusal**: it is the user's gesture.
2. **The announcement of a typed turn** says "Your request needs your interaction": a plain conversation
   has no task and no project to name. The form "task X of project Y" will come with the sessions and the
   tasks.
3. **The word list follows the assistant's language**: "yes" does not approve when Jarvis speaks French.
4. **The place of the window** is that of the other windows of the host; "near the orb" is still to do in
   the Windows host.
5. **The log has no filter and no search**: the 200 most recent decisions.
6. **While a request waits, speaking to Jarvis interrupts the turn** and closes the request (ADR 0005).
   Without a timeout, it is the only way to move on without answering.

## Not verified

- On Windows: the confirmation window, the question said by Piper, the answer heard by Whisper.
- The recognition of "oui" by Whisper in noise, and the echo of the question in the microphone.
- No real "Sensitive" or "Critical" tool exists yet: the first ones arrive with the next work items (files,
  home automation).

## Tests

- Unit: `AssistantStateMachineTests` and `AssistantLooksTests` (condition, look), `ConfirmationWordsTests`
  (closed list), `ConfirmationDeskTests` (window, question, announcement, channels, wait without a timeout,
  interruption, one request at a time), `PermissionDecisionPointTests` (channel checked again, log), `SpeechPlayerTests` (question said
  without the "Speaking" phase, question cut, announcement of a typed turn), `UtteranceListenerTests` (a single listening, no turn
  message).
- Playwright (`ConfirmationTests`): typed request announced then approved by a click, critical action
  refused, spoken "yes", spoken answer outside the list, critical action in a spoken turn, interruption;
  the log is checked each time.

## Windows checks

To do with Benoit, once a first "Sensitive" tool exists:

1. Spoken request: Jarvis says the summary and the question, the orb turns amber, "oui" approves.
2. "Non", then a sentence outside the list: a refusal in both cases.
3. Silence: the window stays open and the orb stays amber; the action does not happen without an answer.
4. The confirmation window does not take the focus of a game or a full-screen application.
