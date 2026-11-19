# ADR 0011: colors and drawing of the orb

## Status

Accepted by Benoit on 2026-11-19 (proposed the same day, work item `spikes/14-orb-look-adr`). On the question
of the long task, he chose the slow rotation of the prototype (section 5). A first version of this page
said the opposite: it was corrected the same day, as soon as his answer was received.

- **Sources:** the [design catalog](../ui/catalog.md), validated by Benoit on 2026-11-19 on animated
  prototypes ([`design/prototypes/`](../../../../design/README.md)); his request of the same day to write
  this ADR from that catalog; the section "The orb and its seven states" of the
  [design](../plans/design.md); spike E6 ([results](../../../../spikes/e6-orb-window/RESULTS.md)).
- **Partly supersedes:** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md), on three points: the
  drawing technique (its section 5), the satellites of delegated tasks (its sections 2 and 6), and the list
  of looks (its section 2). Its state machine, its transitions and its idle budget still hold; that budget
  no longer applies during a long task (section 5).
- **Completes:** [ADR 0010](0010-task-fan-and-orb-click.md), whose section 9 referred here the values of the
  cyan and the amber and the signal of a pending decision.

## Context

- ADR 0005 set the states and the contract of the orb, and referred its colors and drawing to an ADR
  written from a mockup.
- Benoit designed and validated the orb on animated prototypes, in two versions: a "high quality" one and a
  "light" one. The catalog describes them in detail. This ADR does not copy those details: it says what is
  decided and refers to the catalog for the exact drawing.
- The prototypes do not follow every rule already accepted. They draw with Canvas 2D, SVG and CSS, while
  ADR 0005 says "Canvas 2D". They make the orb turn during a long task, while the idle budget forbids any
  continuous animation outside listening, thinking and speaking.
- Spike E6: in the transparent window of the Windows host, a still frame costs 1.3 % of one core, and an
  orb animated at 30 frames per second 14 to 18 %. The cost of the two new versions is not measured.

## Decision

### 1. Two versions, a single component

| Version | Drawing | For whom |
| --- | --- | --- |
| High quality | Plasma sphere, binary clouds on its surface, five gyroscope rings | Default version |
| Light | Still sphere, three open rings turning in opposite directions | Machines where the first one costs too much |

- The exact drawing of each version is the one of the catalog (its sections 2 and 3) and of its prototype.
- It is still **a single Razor component** (ADR 0005). The version is a setting of the orb slice, "orb
  quality", editable in the settings. Jarvis does not change version by itself.
- Both versions receive the same inputs and show the same looks.

### 2. Colors

| Color | Value | Use |
| --- | --- | --- |
| Cyan | `#5fe6ff` | Base color: idle, listening, speaking, frames |
| Violet | `#a08cff` | Thinking |
| Green | `#5fffb0` | Long task |
| Red | `#ff5f6d` | Error |
| Gray | `#7d8b99` | Microphone muted |
| Amber | `#ffb347` | Decision waiting for the user |

- These values are defined once and used by the orb, by the fan (ADR 0010) and by the other screens of the
  catalog.
- The amber of a decision and the red of an error are two distinct colors: the reservation of ADR 0010 is
  lifted.
- The fonts of the catalog (Rajdhani, Inter, JetBrains Mono) are shipped with the application: none is
  downloaded at run time. Their license is checked before shipping them.

### 3. Seven looks

| Look | Color | Behavior |
| --- | --- | --- |
| Idle | Cyan | Still, low brightness |
| Listening | Cyan | Gentle rotation. The voice raises the light gently, without changing the size of the orb |
| Thinking | Violet | Fast rotation |
| Speaking | Cyan | High quality: the light follows the voice. Light: each ring pulses on its own, at random |
| Long task | Green | Slow rotation and progress ring |
| Error | Red | A flash on entry, then still. The cause is shown on hover |
| Microphone muted | Gray | Still, with the label "microphone muted" |

- The rotation speeds of each look are those of the catalog (its section 4).
- Going from one look to another is a fade: the color and the speed change in 0.3 to 0.6 s.
- **Rejected by Benoit, not to be brought back:** a size pulse following the voice while listening, and
  waves coming out of the orb while speaking. The other rejected proposals are in the catalog (its
  section 7).

### 4. Look function

"Long task" becomes a look of its own. The pure function of ADR 0005 becomes:

1. if the phase is Listening, Thinking or Speaking: the look of that phase;
2. otherwise, if there is a fault: "error";
3. otherwise, if the microphone is muted: "microphone muted";
4. otherwise, if there is at least one delegated task: "long task";
5. otherwise: "idle".

Two elements are drawn over the look, whatever it is:

- **The progress ring**, as soon as there is a delegated task. It replaces the satellites of ADR 0005.
  Jarvis can therefore listen or speak while a task moves on, and the ring stays visible.
- **The amber badge**, with the number of pending decisions. It is the signal of a decision when the fan is
  closed (ADR 0010).

### 5. Animation

This table completes the one of ADR 0005 (its section 6). The idle budget does not change: under 2 % of
one core on Windows, when no delegated task is running.

| Look | Rule on Windows |
| --- | --- |
| Idle, microphone muted | Still frame, in both versions |
| Error | The entry flash, then a still frame |
| Long task | **Slow rotation, like the prototype**, during the whole task. The progress ring moves on with it |
| Listening, thinking, speaking | Full animation |
| Reduced motion | No rotation and no pulse; the color and the brightness still change |

- **Long task:** Benoit's choice, the question having been asked to him. The orb turns slowly as long as a
  delegated task is working, even for tens of minutes. According to spike E6, this can cost 6 to 18 % of
  one core during that time: this cost is accepted. The drawing rate of this look is tuned by measurement,
  to keep a smooth rotation at the lowest cost.
- The rotation stops as soon as the look changes: with an error or the microphone muted, the orb is still
  even if a task is working.
- **Animating the idle look** stays forbidden on Windows: the idle budget prevents it. The prototypes have
  a checkbox to try it; allowing it would need a measurement and a new ADR.
- **Drawing by script** (sphere and clouds of the high-quality version): at most 30 frames per second, as
  ADR 0005 says. The prototypes draw at the screen rate; the component caps it.
- **Ring rotation of the light version:** it is done by the browser engine, on layers already drawn, with
  no script on each frame. The 30 frames per second limit does not apply to it. Its cost in the transparent
  window is not known: it is measured.

### 6. Drawing technique

- ADR 0005 said: "It draws with Canvas 2D". The component now draws with **Canvas 2D, SVG and CSS**,
  depending on the version: Canvas 2D for the sphere and the clouds, SVG and CSS transforms for the rings,
  CSS gradients for the sphere of the light version.
- The rest does not change: the CSS and the JavaScript sit next to the component, and the JavaScript only
  draws. No state rule is written there.
- The prototypes are visual references, not product code. The component ports them; it does not reference
  them.

### 7. Component inputs

To the inputs of ADR 0005 are added:

- the version (high quality or light);
- the progress of the long task, from 0 to 1, or "unknown";
- the number of pending decisions.

The number of delegated tasks stays an input. The sound level is used as section 3 says: it drives the
light, never the size.

### 8. Measurement at stage 1

- Before the orb slice is declared done, both versions are measured on the real host, with the method of
  spike E6: idle, then listening, thinking, speaking and long task.
- When idle, both versions must stay within the budget of ADR 0005.
- If the high-quality version clearly exceeds, when animated, the 14 to 18 % of one core accepted by
  ADR 0005, the light version becomes the default version, and the project memory records it.

### 9. What this ADR does not decide

- **The progress ring when there are several tasks, or a task with no percentage** (a Claude Code session
  gives none). The delegated tasks ADR, at stage 2, sets it.
- **The fixed sign of listening and speaking with reduced motion.** They share the cyan with the idle look
  and then differ from it only by brightness. To be validated on a prototype during the orb slice.
- **The size of the orb on screen** and the area that receives clicks. The rings of the high-quality version
  extend far beyond the sphere: the orb window (ADR 0003) must contain them.
- **The full-screen "command post"** of the catalog. It will get its ADR with the stage that builds it.
- **Mobile.** The catalog only covers the computer; the version shown on the phone is chosen at stage 4.

## Consequences

- The orb has seven exclusive looks, instead of six looks and one overlay. The satellites are gone.
- Idle, listening and speaking have the same color. They differ by motion and light, not by hue. Thinking,
  long task, error and microphone muted each have their own.
- During a long task, the display costs like an animated phase, for as long as the task lasts. The budget
  of under 2 % of one core only applies with no delegated task. ADR 0005 planned still satellites: that
  rule is superseded.
- There are two drawings to keep current and to test. The demo page (US1.3) shows both versions and the
  seven looks.
- **Risk:** the high-quality version redraws a grid of 42 × 96 digits on every frame. Its cost in the
  transparent window may exceed what spike E6 measured. Section 8 covers that case.
- The section "The orb and its seven states" of the design described the former drawing (pulsing rings,
  satellites). It is updated with the acceptance of this ADR.
