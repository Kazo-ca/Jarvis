# ADR 0027: gestures on the orb (replaces section 3 of 0010)

## Status

Accepted by Benoit on 2026-11-22 (work item `stage2/16-orb-gestures-adr`). The three gestures are his request;
the points marked "provisional" are application choices he can change.

- **Source:** Benoit's message of 2026-11-22: "Interaction with the orb: I have a few changes to make:
  1- a right click shows the fan; 2- a double click shows the projects view we are designing; 3- one left
  click does what the wake word does, Jarvis listens to everything we say."
- **Replaces:** section 3 of [ADR 0010](0010-task-fan-and-orb-click.md) ("Single click and double click on
  the orb"). The rest of ADR 0010 (the fan, its panels, its window, its animation) does not change.
- **Completes:** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md): the orb gains a "right click"
  output; its transition table does not change.
- **Does not decide:** the projects view itself (its ADR is being written); the gestures on the phone (the
  mobile ADR, at stage 4).

## Context

ADR 0010 gave the double click to the task fan. Benoit is now designing a view of the projects and
conversations, which becomes the main view: it takes the double click, the most natural gesture to "open".
The fan, which is read at a glance, moves to the right click.

## Decision

### 1. Left click: Jarvis listens

- A left click on the orb does what the wake word does: Jarvis listens. This is the behaviour of ADR 0005,
  which does not change: at rest the click starts listening, while Jarvis thinks or speaks it interrupts
  him and starts listening (US1.6, US1.12).
- **Provisional:** a click while listening still cancels it, as ADR 0005 says. The wake word is ignored
  while listening. Without this there would be no way left to stop, with the mouse, a listening started by
  mistake.

### 2. Right click: the task fan

- A right click on the orb opens the fan. A second right click puts it away.
- It is also put away by the Escape key and by a click on the darkened desktop, as before.
- A right click waits for no delay and never starts listening.
- The orb has no context menu: the menu of Jarvis stays the one of the notification area icon.
- The fan is still not a state of the assistant (ADR 0010, section 3, last point, kept here).

### 3. Double click: the projects view

- A double click on the orb asks for the projects view, through a kernel message, `ProjectsViewRequested`.
  The slice of the projects view answers it once it exists.
- A double click is never also a single click: the orb waits for the double-click delay of the system before
  it treats a left click, as ADR 0010 said. The wake word, the hotkey and the right click are not delayed.
- **Provisional: as long as no slice answers this message**, there is neither double click nor delay: the
  left click acts at once. This is the rule ADR 0010 applied before the fan was built. The delay comes back
  by itself the day the projects view is registered.

### 4. In the hosts

- **Windows:** the orb window catches the right click as it already catches the left press, and gives it to
  the orb slice.
- **Test Web host:** the orb component receives the right click of the browser and holds its menu back.
- **Mobile:** nothing changes at this stage; there is no fan on the phone (ADR 0010, section 4).

## Consequences

- The `TaskFanToggleRequested` message now comes from the right click. The fan slice does not change.
- As long as the projects view does not exist, the left click is immediate again: the half-second wait
  disappears for now.
- The fan tests open the fan with a right click. The double click stays covered by unit tests, with a
  stand-in slice that answers the message.
- The documents that describe the double click (orb, fan, hosts, features) are updated with the code.
