# UI design catalog

Designs Benoit validated on 19 November 2026, from mock-ups and animated prototypes. This document
describes them so they can be reproduced in the application. The HTML prototypes are in
[`design/prototypes/`](../../../../design/README.md). They are visual references, not product code: the
application will port them to Razor components, with their CSS and JavaScript next to each component
(AGENTS.md, section II).

Architecture decisions stay in the ADRs. The task fan is decided by
[ADR 0010](../adr/0010-task-fan-and-orb-click.md). The colours and the drawing of the orb are decided by
[ADR 0011](../adr/0011-orb-colors-and-drawing.md), which builds on this catalog.

Everything below is for **the PC only**. The mobile app is not covered.

## 1. Shared style

| Token | Value | Use |
| --- | --- | --- |
| Background | `#040a12`, `#0f3550` glow at the top | Full-screen window background |
| Cyan | `#5fe6ff` | Jarvis's base colour: idle, listening, speaking, frames |
| Violet | `#a08cff` | Thinking |
| Green | `#5fffb0` | Long task, done |
| Red | `#ff5f6d` | Error |
| Amber | `#ffb347` | A decision waiting for Benoit (badge, cards, buttons) |
| Grey | `#7d8b99` | Mic muted |
| Text | `#d3f3fb`, secondary `#7fa6b5` | |

- **Fonts:** Rajdhani for titles (spaced capitals), Inter for text, JetBrains Mono for labels and figures.
- **"Hologram" details:** square corner brackets on frames, thin horizontal scan lines (3 to 6 % opacity),
  translucent gradient backgrounds, a glow in the state colour.

## 2. The orb, high-quality version

Prototype: [`orb-high-quality.html`](../../../../design/prototypes/orb-high-quality.html).
Image: [`orb-high-quality.jpg`](../../../../design/images/orb-high-quality.jpg).

Three stacked layers:

1. **Plasma sphere** (Canvas 2D). Radial gradient (white highlight top left, state colour, bright rim),
   18 filaments: great circles of the sphere warped by a sine wave, turning slowly, drawn additively at low
   opacity. Small white core in the centre, glow around it.
2. **Binary planet clouds** (same canvas). 0s and 1s laid on a 42 latitude × 96 longitude grid on the
   sphere's surface. Fractal noise decides where clouds are. Latitude bands drift at different speeds, as on
   Jupiter, and the sphere spins on an axis tilted by 0.38 rad. Towards the edge digits shrink and fade; the
   back face is not drawn. Digits change slowly. With the clouds on, the sphere and filaments are darker and
   more transparent so the clouds stand out.
3. **Gyroscope rings** (CSS 3D with SVG). Five translucent rings, each tilted on a different axis and spinning
   at its own speed; the whole set slowly pivots on another axis, like a gyroscope:

   | Ring | Size (side of the stage) | Tilt | Spin (rad/s at speed 1) |
   | --- | --- | --- | --- |
   | Degree scale (0° to 360°, numbers every 30°) | 96 % | `rotateX(68°)` | +0.12 |
   | Thick arcs and dotted inner track | 82 % | `rotateX(62°) rotateY(24°)` | −0.35 |
   | Dashes and four nodes | 70 % | `rotateY(68°) rotateX(10°)` | +0.5 |
   | Gradient band with 24 marks | 58 % | `rotateX(−48°) rotateY(−32°)` | −0.7 |
   | Inner crown (facing the screen) | 36 % | none | +0.9 |

   Square sighting brackets frame the sphere.

## 3. The orb, light version

Prototype: [`orb-light.html`](../../../../design/prototypes/orb-light.html).
Image: [`orb-light.jpg`](../../../../design/images/orb-light.jpg).

For machines where the high-quality version costs too much. It follows Benoit's sketch: a core and three
open rings, like the dials of a combination lock.

- **Still sphere** made of stacked CSS gradients: white highlight, bright rim, shadow bottom right, state
  colour. Still glow around it. Nothing is redrawn per frame.
- **Three open rings** in SVG (200 × 200 viewBox), each with a gap and a "tooth" at each end:

  | Ring | Radius | Stroke | Gap | Direction | Full turn at speed 1 |
  | --- | --- | --- | --- | --- | --- |
  | Inner | 44 | thick (6), gradient, fine scale | 60° | clockwise | 24 s |
  | Middle | 60 | two arcs (3.5) of 130° and 155°, dotted track | two gaps | anticlockwise | 36 s |
  | Outer | 78 | thin (1.6), marks every 5° and 30°, cursor | 80° | clockwise | 60 s |

- **Animation:** only the rings rotate, through the Web Animations API on `transform` (done by the graphics
  card). Speed changes between states go through `playbackRate`, eased over about 0.6 s, with no jump. In
  idle and mic muted, the animations are paused.
- **Speaking:** each ring pulses on its own, at random: it grows by 2.5 to 7 % and brightens, for 220 to
  480 ms, every 140 to 660 ms. This is what tells speaking apart from listening.

## 4. Orb states

| State | Colour | Speed (high quality / light) | Behaviour |
| --- | --- | --- | --- |
| Idle | cyan | 0 / 0 | Still (accepted rule: no cost when idle) |
| Listening | cyan | 0.8 / 1 | Gentle rotation. The voice gently raises the light, size never changes |
| Thinking | violet | 2.4 / 3.5 | Fast rotation |
| Speaking | cyan | 1 / 1.2 | High quality: the light follows the voice. Light: random ring pulses |
| Long task | green | 0.45 / 0.5 | Slow rotation and a progress ring (percentage) |
| Error | red | 0.25 / 0.25 | One flash on entry, then almost still |
| Mic muted | grey | 0 / 0 | Still, "mic muted" label |

- Moving between states cross-fades colour and speed over 0.3 to 0.6 s.
- A **pending decision** adds an amber badge with the count, on top of any state.
- **What Benoit dislikes:** a size pulse following the voice while listening (it looked like the orb was
  moving back and forth), and ripples coming out of the orb while speaking. Both were removed.
- **Open question:** animating the orb while idle. The prototypes have a checkbox to try it; the cost must
  be measured on the real host before allowing it.

## 5. The task fan

Prototype: [`task-fan.html`](../../../../design/prototypes/task-fan.html) (parameters `?v=A&s=open&c=br`,
`c` = corner `tl`, `tr`, `bl` or `br`). Decided by ADR 0010, the gesture by ADR 0027: a right click on the orb fans the windows of
running tasks out in an arc, towards the centre of the screen whichever corner the orb sits in; a second
right click pulls them back. Desktop dimmed, thin lines from the orb to each window, a task waiting for a decision in amber with
its buttons, animation only while opening and closing.

## 6. Full screen ("command post")

Prototype: [`full-screen.html`](../../../../design/prototypes/full-screen.html).
Image: [`full-screen.jpg`](../../../../design/images/full-screen.jpg).

- **Status bar** at the top: Jarvis's state, wake word active, local model, Claude sessions, monthly cloud
  budget, date and time.
- **Left, "To handle":** only what needs Benoit, sorted by urgency: decisions (amber), errors (red), unread
  done items (green). Each card has its buttons to act without opening it. The same count shows as a badge
  on the orb. At the bottom, a link to the running tasks.
- **Centre, the open item:** the small orb with the title, tabs (conversation, log, files, details), the
  conversation with approval requests in amber, and a field at the bottom to talk or type.
- **Right, "Find":** one search (Ctrl+K or by voice) across conversations, tasks and Claude sessions; filter
  chips (type, pinned, period); results grouped by day, with the search term highlighted in the excerpt.

## 7. Rejected proposals

Kept for the record, not to be reused without asking Benoit: orbit and projection fans (ADR 0010's choice);
"reactor", plasma-only and wireframe "gyroscope" orbs as still images; vertical binary rain (replaced by the
clouds); speaking ripples. The version
[`orb-plasma-gyroscope.html`](../../../../design/prototypes/orb-plasma-gyroscope.html) (plasma and rings, no
clouds) is kept aside at Benoit's request.
