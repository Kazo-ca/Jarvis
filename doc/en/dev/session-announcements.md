# Announcements of the Claude Code sessions: end, wait, quiet hours, "where is Claude?"

Work item `stage2/13-session-announcements` (US2.12, US2.13). Decisions:
[ADR 0021](adr/0021-claude-code-sessions.md), section 9 (what is announced, per-folder setting, quiet hours,
"I am going to sleep"), and [ADR 0024](adr/0024-announcement-turn.md) (the announcement turn). ADR 0024 was accepted by
Benoit on 21 November 2026.

## What the user sees and hears

- When a Claude Code session **waits for a permission**, **asks a question**, **ends** or **is blocked**,
  Jarvis says so: "Claude Code is waiting for your permission on kazo-bar.", "Claude Code is done on
  kazo-bar. The menu is fixed."
- The announcement is also written in the conversation, as a line of Jarvis with no request above it.
- It never interrupts: it waits until Jarvis is at rest. The wake word, the hotkey or a click cut it like an
  answer.
- The "Announcements" page (settings, order 55): what happens right now (announcements said, quiet hours,
  quiet period and when it ends), a button to stay silent for 8 hours or to end the silence, the quiet hours
  of every day, and a choice per folder: everything, only the waits, nothing.
- "I am going to sleep": Jarvis offers 8 hours of silence and only starts after the yes.
- "Where is Claude on Kazo Bar?": Jarvis answers from the state of the session and its last message.

A session started by Jarvis and a session started by hand (seen through its hooks,
[hook-server.md](hook-server.md)) are announced the same way.

## The announcement turn (kernel and `Conversation/Dialogue`)

```
slice that announces ── AnnouncementToSay(text, language, aloud) ──► AnnouncementDesk (conversation)
                                                                        │ writes in the journal, at once
                                                                        │ aloud: waiting line
                                        at rest: AnnouncementSubmitted  ▼
                         state machine ── TurnStarted (announcement, text) ──► AnnouncementDesk
                                                                        │ SentenceToSpeak, AnswerSentencesEnded
                                                                        ▼
                                                         Voice/Speech says the sentences of the turn
```

| Element | Role |
| --- | --- |
| `AnnouncementToSay` (`Kernel/Messages/Assistant`) | What a slice wants to announce; it alone decides whether it is said |
| `AnnouncementSubmitted` | Input event of the state machine: new turn at rest, ignored otherwise |
| `TurnInput.Announcement` | Third input of a turn: neither microphone nor model |
| `AssistantState.Announcing` | The current turn is an announcement: its end does not clear the fault |
| `AnnouncementDesk` (`Conversation/Dialogue`) | Writes the announcement, waits for the rest, submits it, gives its sentences to the voice |

- An announcement is written **as soon as it arrives**, said or not.
- Announcements are said one by one. **At most five wait**; an announcement that has waited for more than
  **ten minutes** is no longer said. In both cases it stays written.
- An announcement cut by an activation is not said again.
- **An announcement is never sent to a model**: neither in the history of the next turns, nor to the
  conversation memory, nor in the silence that makes an exchange "old". Its text may come from a session.
- During an announcement, the conversation window takes no text, as during an answer.

## Slice `ClaudeCode/Announcements`

| File | Role |
| --- | --- |
| `SessionAnnouncer` | Watches the board of the tasks, decides what is news, publishes `AnnouncementToSay` |
| `ClaudeAnnouncementsOptions` | Section `ClaudeCode.Announcements` of `config.json` |
| `QuietTime` | When nothing is said: quiet hours and quiet period |
| `QuietPeriodTool` | The tool `start_quiet_period` |
| `SessionStatusTool` | The tool `get_claude_sessions` |
| `ClaudeAnnouncementsPage` | The "Announcements" page |

### What is news

The announcer compares the status of each Claude Code task (sub-tasks aside) with the one before. It listens
to no hook itself: the board of the tasks is its only source, whatever the origin of the task.

| Change of status | Announcement |
| --- | --- |
| To `waiting_approval` | "is waiting for your permission" |
| To `waiting_answer` | "has a question for you" |
| From `working` or from a wait to `idle` or `done` | "is done", with the last message |
| From `working`, from a wait, from `idle` or from `paused` to `blocked` | "is blocked", with the cause |
| To `abandoned` | Nothing: the user stopped it |
| From `pending` to `blocked` | Nothing: the turn that started it already said so |
| From `idle` to `done` | Nothing: the end of its turn was already announced |
| Task already on the board when Jarvis starts | Nothing |

- The name said is the last part of the folder of the session; failing that, the name of the project.
- The language is the one of the assistant at the time of the announcement.

### The last message of a session

The last message is **a piece of data**: it is never executed.

- 160 characters or fewer: said as it is.
- Longer, announcement said, **local model already in memory**: shortened by the local model, without tools,
  the message being a single content with the instruction to follow nothing it holds. 20 seconds at most,
  300 characters at most.
- Otherwise (no model, not loaded, failing, too slow): cut at 300 characters, at the end of a sentence when
  there is one.
- Silent announcement: cut at 500 characters, without a model.
- The cause of a block is never reworded, only cut.

An announcement does not make the local model load.

### Per-folder setting

`Everything` (default), `Only when it waits for me`, `Nothing`. "Nothing" writes nothing either. The page
lists the allowed folders, those of the sessions seen and those already set.

### Silence

- **Quiet hours**: ranges from `HH:mm` to `HH:mm`, local time, every day; a range may pass midnight. **None
  by default.** Six at most.
- **Quiet period**: starts now, lasts from 1 to 24 hours (8 by default), **changes no setting and ends by
  itself**. It is kept in memory only.
- During the silence, the announcement is published "silent": written in the conversation, not said, and not
  said later.

### Tools of the local model

| Tool | Class | Role |
| --- | --- | --- |
| `start_quiet_period` (`hours`, 8 by default) | Everyday effect | Starts a quiet period |
| `get_claude_sessions` (`folder`, optional) | Read | The state of the sessions and their last message |

- The description of `start_quiet_period` tells the model to offer 8 hours of silence when the user says they
  are going to sleep, and to call the tool only after their yes. **When quiet hours are set, the description
  changes**: the model no longer offers it by itself.
- `start_quiet_period` is **refused once the turn already holds a content that comes from outside** (a page,
  a file, the last message of a session): a text the model just read cannot make Jarvis silent (ADR 0019,
  section 3).
- `get_claude_sessions` returns at most five sessions, the most recent first: folder, title, status, started
  by Jarvis or by the user, minutes since the last change, last message (600 characters at most). The name
  given is compared regardless of case, accents and separators. The result is marked as a content that comes
  from outside the conversation (ADR 0019, section 3).

## Settings (`ClaudeCode.Announcements`)

| Setting | Default |
| --- | --- |
| `Folders` (folder, level) | none: everything is announced |
| `QuietHours` (from, to) | none |

## Web host

- `SimulatedHandSession`: three buttons of the test page drive a session started by hand (it works, waits for
  a permission, ends its turn), by moving the board the way the hook routes do.
- The simulated model calls `get_claude_sessions` on "where is Claude on …" and `start_quiet_period` on "stay
  silent for N hours", and returns "Résumé simulé de N caractères." when asked to shorten a last message.

## Deviations from ADR 0021

1. **The amber badge of the orb** during the silence (ADR 0021, section 9) was not in this work item: it
   came with the fan (work item 14, [task-fan.md](task-fan.md)), which counts the tasks that wait.
2. **"I am going to sleep" goes through the description of the tool**, not through code: the local model
   recognizes the sentence and makes the offer. No spike with the real model measured whether it does it well.
3. **The announcement of a session started by Jarvis is said at the end of its turn** (status `idle`), not at
   the end of the process: that is when its last message is known.

## Provisional choices of this work item

To be confirmed by Benoit; each can change without touching the others.

1. At most five announcements waiting, ten minutes of waiting at most.
2. The last message is only shortened by the local model when it is over 160 characters and the model is
   already in memory.
3. The quiet period is lost when Jarvis closes.
4. A session stopped by the user is not announced.
5. Every wait for a permission is announced, even several in a row for the same session.
6. An announcement does not count as an activity in the delay that makes an exchange "old".

## Not verified

- **Nothing ran on Windows, and no announcement was said by a real voice.**
- No session started by hand was announced through real hooks.
- The quality of the last message shortened by the local model, and how long it takes.
- Whether the local model does offer the silence on "I am going to sleep".

## Tests

- Unit: `AssistantStateMachineTests` (row of ADR 0024), `AnnouncementDeskTests` (written then said, silent,
  waiting for the rest, one at a time, cut, waited too long, never sent to the model),
  `SessionAnnouncerTests` (table of the news, folder, silence, last message shortened or cut, the two tools,
  settings).
- End to end: `SessionAnnouncementsTests` (4 scenarios on the Web host).

## Windows checks

To do with Benoit, on his PC:

1. A session started by Jarvis asks for a write: Jarvis says it waits, the confirmation window is there.
2. It ends: Jarvis says it is done, with one or two sentences on what it did.
3. A session started by hand, hooks installed: same announcements.
4. An announcement while Jarvis answers: it waits for the end of the answer.
5. The wake word during an announcement: it is cut, Jarvis listens.
6. Quiet hours: nothing is said, the announcement is in the conversation.
7. "I am going to sleep": Jarvis offers 8 hours of silence; after "yes", the "Announcements" page shows the
   period and when it ends.
8. "Where is Claude on *folder*?": the answer matches the "Claude sessions" page.
