# Conversation: the full turn

Work item `stage1/13-conversation` (US1.9, US1.12, US1.14, US1.15, ADR [0005](adr/0005-assistant-state-machine-and-orb-contract.md),
[0007](adr/0007-llm-providers-and-tool-format.md) and [0008](adr/0008-languages-and-localization.md)): Jarvis
answers. The spoken or typed request goes to the default local model, the answer arrives in a stream and each
complete sentence goes to the voice at once. The wake word, the hotkey or a click interrupts him. The
conversation window keeps the history; the "Personality" page sets the system message.
Français : [../../fr/dev/conversation.md](../../fr/dev/conversation.md).

## What is built

```
wake word / hotkey / click ─► ActivationRequested ─► state machine ─► TurnStarted (voice)
                                                                       │
UtteranceListener (work item 10) ─► UtteranceEnded ─► UtteranceTranscribed ─┤
conversation window ─► TextSubmitted ─► state machine ─► TurnStarted (text)
                                                                       ▼
                                 ConversationEngine (slice Conversation/Dialogue)
                 IAssistantModel (work item 11) ◄─ system message + history + request
                                                                       │ streamed answer
                   SentenceSplitter ─► SentenceToSpeak … AnswerSentencesEnded ─► voice (work item 12)
                   typed request ─► AnswerFinishedWithoutSpeech (no voice)
                   ConversationJournal ─► window + collection Conversation.Turns (SQLite)
```

No slice references another: everything goes through the kernel's messages. The kernel did not change.

Since work item `stage2/3-tool-loop`, the engine also gives the enabled tools to the model and runs its
calls through the permission decision point: see [tools.md](tools.md).

### Slice `Features/Conversation/Dialogue`

- **`ConversationEngine`** runs the turn:
  - **Typed request** (`TurnStarted` with `TurnInput.Text`): written answer only, then
    `AnswerFinishedWithoutSpeech`. **Jarvis speaks only when spoken to** (answer 3 of the plan).
  - **Spoken request** (`UtteranceTranscribed`): each complete sentence is published as `SentenceToSpeak` as
    soon as it is written, then `AnswerSentencesEnded`. The voice publishes `FirstSentenceReady` and
    `SpeechEnded`.
  - **Nothing heard** (empty Whisper text): `AnswerFinishedWithoutSpeech`, without calling the model.
  - **Interruption** (`TurnCancelled`, US1.12): the generation stops (the stream token is cancelled); the
    voice cuts itself. The wake word stays active while thinking and speaking (work item 9).
  - **Model failure** (`LlmException`): `TurnFailed` with the fault `ModelMissing` (model missing) or
    `ModelUnreachable` (any other cause), and the precise cause as detail. Before the call,
    `IAssistantModel.EnsureLoadedAsync` reloads the model if Ollama freed it, and says so.
  - **Delay to the first sound**: from `UtteranceEnded` to `FirstSentenceReady`, written to the log and shown
    in the window.
- **`SentenceSplitter`** cuts the answer: a sentence ends at `.`, `!`, `?` or `…` followed by a space, or at a
  line break. An abbreviation ("M.", "Dr."), a number ("3.5"), an initial or a list number does not end it. A
  sentence longer than 250 characters goes up to its last comma. Markdown marks (`**`, `#`, back quotes,
  bullets) are not said.
- **`SystemPrompt`**: the personality of the language (the user's, else the butler of the `PromptTexts`
  resources), then the **fixed rules**: answer in the language of the turn, a text made to be said (no
  Markdown, lists, emoji or code, short sentences), the local date and time.
- **History sent to the model**: the last answered turns that fit in about half of the provider's context
  (3 characters per token), 20 turns at most and, when the memory is in use, no turn from before a silence
  of 2 hours. The memory block is added to the system message after the fixed rules
  ([conversation-memory.md](conversation-memory.md)).
- **`ConversationJournal`**: the turns of the collection `Conversation.Turns` (read once, when first needed)
  and the turn in progress. A turn is stored **when it ends**: answered, interrupted (with what was written by
  then) or failed (with the cause). What is stored (ADR 0007, section 7): request, answer, language, voice or
  text, provider, model, tokens in and out, duration, delay to the first sound. The technical log never holds
  the text (ADR 0004, section 6).
- **Conversation window** (`ConversationWindow`, key `Conversation.Window`), opened by the "Conversation"
  entry of the tray menu (`ConversationWindowRequested`, enabled since this work item): history (newest at the
  bottom, no script), answer written live, model and duration of each turn, "the model is loading" while
  Ollama reloads, input field (Enter sends; greyed out while Jarvis is not idle), **"Erase everything"** with a
  confirmation inside the window: conversation, memory and log of the database; settings and keys kept
  (ADR 0004).
- **"Personality" settings page** (`Conversation.Personality`, after "Language models"): the message of each
  language, the assistant language first, "Save" and "Reset". Setting `Conversation.Dialogue` of
  `config.json`: only the changed texts, by language code.

### Slice `Features/Voice/Speech` (touch-up)

`SpeechPlayer` also listens to `TurnFailed`: when the model fails in the middle of an answer, the voice stops
at once instead of finishing the sentences already received.

### Order of the settings pages

Language (0), Orb (5), Language models (10), Personality (15), Audio (20), Wake word (25), Transcription (30),
Voices (35). Wake word, Transcription and Voices all had rank 25.

### Web host

The "Open the conversation (tray menu)" button publishes the same event as the tray menu. With "Say “Hey
Jarvis”" or the hotkey, then "Say a phrase", the full turn runs on the simulations: transcription, model
(`SimulatedLlmConnector`, a two-sentence answer, word by word) and voice.

## Provisional choices of this work item

The ADRs do not settle them; for Benoit to confirm:

1. **End of a sentence**: `.`, `!`, `?`, `…` followed by a space, or a line break; a list of French and English
   abbreviations; a sentence cut at a comma beyond 250 characters.
2. **Markdown removed from what is said**, kept in the written answer.
3. **History**: last answered turns, 20 at most, in half of the context; interrupted or failed turns are not
   sent back to the model; **no age limit** (a turn from yesterday counts) until the summary of stage 2. *(Lifted by work item
   `stage2/8-memory`: silence of 2 hours, can be set.)*
4. **A turn is stored when it ends**. A cancelled listening, or one without words, is not stored.
5. **Model failure in the middle of a spoken answer**: the voice stops, the orb shows the error.
6. **Personality**: both languages on the same page; saving the butler's text or an empty text is a reset;
   4,000 characters at most. Default texts taken from spike E4 ("You are Jarvis, the butler from the film.
   Polite, concise, with a dry sense of humour…").
7. **Date and time** in the fixed rules: local time in the long format of the language, without time zone.
8. **Typed request** in the language of the assistant; spoken request in the language of the transcription.
9. **Window**: 500 turns in memory (older ones stay in the database), sending only when idle, confirmation of
   "Erase everything" inside the window rather than a Windows dialog.

## Tests

- Unit: `ConversationEngineTests` (typed request without voice and stored in SQLite, spoken request sentence
  by sentence, system message and history, interruption, named failures, failure in the middle of an answer,
  nothing heard, history window, erase everything; with the real state machine), `SentenceSplitterTests`,
  `ConversationOptionsTests`, and in `SpeechPlayerTests` the voice cut by a model failure.
- Playwright (`ConversationTests`): full spoken turn, **interruption by the wake word while speaking**, typed
  request without voice, "Erase everything", personality changed then reset. `TranscriptionTests` now waits
  for the answer.
- Screenshots of the Web host: `stage1-13/` in the project files.

## Windows checks

For the next session with Benoit (**his voice and his ears**), Ollama on port 11435 set in "Language models":

1. **Delay between the end of the sentence and the first sound**, real microphone and speaker, warm: target
   under 2 s (stage 1 plan). Three tries; the figure is in the window ("first sound after … s") and in the
   log. Write the values in the memory.
2. **"Hey Jarvis" while Jarvis speaks**: the voice stops and Jarvis listens. If it is not recognized reliably,
   interruption while speaking goes through the hotkey and the click only, and **a new ADR says so**
   (ADR 0005).
3. Hotkey and click on the orb while thinking and while speaking.
4. "Conversation" window from the tray menu: history kept after a restart, model of each turn, typed request
   answered in writing without voice, "Erase everything".
5. French personality changed (for instance "answer in one word"), a question, then "Reset".
6. Ollama stopped: the turn fails with "the model server does not answer" and the error orb; model freed
   (`ollama stop qwen3:14b`): "the model is loading", then the answer.
