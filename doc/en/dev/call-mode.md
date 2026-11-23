# The "Call" mode: a conversation that goes on

Work item `stage2/20-call-mode`. Decision: [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 12
and 13 (point 3). The agents are described in [agents.md](agents.md), the projects in [projects.md](projects.md).

This work item builds what the "Call" mode ("Appel") means for the agents on the PC: it starts, the agent keeps
the hand, Jarvis listens to what follows without "Hey Jarvis", it ends. **Still to build:** the calls in the
Android app (stage 5 of the design document).

## What is built

```
"On doit discuter de…"  or  the "Call" button ──► CallMode.Start()
        │
        ▼
ConversationEngine ── during the call: the agent of the turn before gets the request (CallMode.Agent),
        │             with the project of the call (CallMode.Project)
        │             "goodbye": Jarvis answers "Au revoir." without the model, CallMode.End()
        ▼
CallListener ── a spoken turn ends, Jarvis is at rest ──► ActivationRequested(Call) ──► listening
```

Everything lives in the `Conversation/Dialogue` slice. The kernel gains one thing only: the activation source
`ActivationSource.Call`.

## Starting (`CallMode.Opens`)

- **The "Call" button** of the conversation window. Jarvis listens right away, when the microphone allows it.
- **A sentence**, spoken or typed: "on doit discuter", "on doit parler", "il faut qu'on parle", "il faut qu'on
  discute", "j'aimerais discuter", "je veux discuter", and in English "we need to talk", "we need to discuss",
  "let's discuss"… Compared by code, whatever the case and the accents, never by a model. The sentence is then
  handled like any request: the Jarvis agent sorts and hands over.

## During the call

- **The agent that answered keeps the hand** for the next turn, spoken or typed, with the project of the turn.
  The Jarvis agent no longer sorts while another agent has the hand.
- **Going back to Jarvis**: the agent calls `return_to_jarvis`, as outside a call. The call goes on with the
  Jarvis agent, which may hand over again.
- An agent chosen in the window for a typed request goes before the agent of the call.
- **Jarvis listens to what follows without "Hey Jarvis"** after a spoken turn that got its answer. It is an
  ordinary activation: refused when the microphone is muted or missing (ADR 0005), and the orb shows listening.
- `ActivationSource.Call` only starts listening at rest: it never cancels listening and never interrupts Jarvis.
- **Jarvis does not insist**: a listening the user cancels (click, hotkey, muted microphone), a typed turn, an
  announcement or a fault do not start listening again. The user resumes with "Hey Jarvis" or a click.
- **After three listenings in a row with nothing heard**, Jarvis no longer listens by itself. The call stays open.
- The conversation window says "Call in progress with the X agent".

## Ending

- **"Goodbye"**, alone in the request ("Au revoir", "Au revoir Jarvis", "Bye", "Goodbye", "À plus tard", "À la
  prochaine"). Jarvis answers "Au revoir." without calling the model.
- The **"Hang up"** button of the window.
- Jarvis stopping: the call is not kept.

## Provisional choices of this work item

To be confirmed by Benoit:

1. The sentences that start a call and those that end it are fixed lists in the code.
2. After three silences in a row, Jarvis no longer listens by itself, but the call stays open.
3. A typed turn during the call does not start listening again.
4. The "Call" button is in the conversation window; nothing on the orb says a call is in progress, apart from
   listening itself.
5. Going back to Jarvis keeps the project of the call.
6. "Goodbye" outside a call is an ordinary request, sent to the model.
7. A request handed to Claude ("Demande à Claude…") during a call does not change the agent that has the hand.

## Checks on Windows

Nothing of this work item ran on Windows, and listening without "Hey Jarvis" was never tried with a real
microphone.

1. "On doit discuter de Kazo OS", then two questions in a row without "Hey Jarvis": does the "Project" agent
   answer both?
2. **Does listening resume too early?** Jarvis must not hear itself at the end of its answer.
3. Three silences: does listening stop, and does "Hey Jarvis" resume it within the same call?
4. "Au revoir" ends the call; the next request goes through the Jarvis agent again.
5. Microphone muted during a call: no listening starts.
