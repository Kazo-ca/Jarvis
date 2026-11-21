# The orb

Work items `stage1/6-orb` (light version) and `stage1/7-orb-high-quality` (high-quality version and "orb
quality" setting) of the [stage 1 plan](plans/stage-1.md). Decisions:
[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md) (orb contract, animation rule) and
[ADR 0011](adr/0011-orb-colors-and-drawing.md) (two versions, colors, seven looks, drawing). Reference
drawings: sections 2 and 3 of the [catalog](ui/catalog.md) and the prototypes
[`orb-high-quality.html`](../../../design/prototypes/orb-high-quality.html) and
[`orb-light.html`](../../../design/prototypes/orb-light.html).

## The slice

`src/Kazo.Jarvis.Features/Assistant/Orb/`:

| File | Role |
| --- | --- |
| `Orb.razor` (+ `.razor.css`, `.resx`) | The component. It only shows its inputs: the looks (color and light), the progress ring, the badge, the "mic muted" label, and the drawing of the chosen version |
| `OrbHighQuality.razor` (+ `.razor.css`, `.razor.js`) | The high-quality drawing: plasma sphere and binary clouds in a canvas, five gyroscope rings in SVG and CSS 3D |
| `OrbLight.razor` (+ `.razor.css`, `.razor.js`) | The light drawing: still sphere in CSS gradients, three open rings |
| `OrbDrawing.cs` | Base of both drawings: loads their script, hands it the motion and the sound level |
| `OrbView.razor` | The orb as the hosts show it: fed by the state machine (`AssistantStateChanged`), the sound level and the quality setting; its click publishes the activation |
| `OrbMotion.cs` | The motion rule of each look and each version (speed, pulses), in C# |
| `OrbGeometry.cs` | The arcs, ticks and "teeth" of the three light rings, in a 200 × 200 box |
| `OrbGyroscopeGeometry.cs` | The five gyroscope rings (degree scale, arcs, dashes and nodes, band, crown) and their speeds, in a 1,000 × 1,000 box |
| `OrbQuality.cs`, `OrbOptions.cs`, `OrbQualityKeeper.cs` | The "orb quality" setting (section `Assistant.Orb` of `config.json`), watched so it applies without a restart |
| `OrbSettingsPage.razor` (+ `.razor.css`, `.resx`) | The "Orb" page of the settings window |
| `OrbActivation.cs` | The click on the orb (US1.6; ADR 0010): a single click publishes `ActivationRequested(Click)` after the double-click delay of the host, a double click publishes `TaskFanToggleRequested` (see [task-fan.md](task-fan.md)) |
| `OrbSlice.cs` | Registration |

### Inputs and outputs of the `Orb` component

| Input | Source in `OrbView` |
| --- | --- |
| `Look` | `AssistantState.Look`, the kernel's look function |
| `Quality` | The "orb quality" setting: high quality by default (ADR 0011, section 1) |
| `DelegatedTasks` | `AssistantState.DelegatedTasks`; the progress ring is drawn above zero |
| `Progress` (0 to 1, or unknown) | Unknown for now: delegated tasks come at stage 2 |
| `PendingDecisions` | 0 for now (stage 2); the demonstration page simulates it |
| `Fault` | The cause of the fault condition, shown on hover |
| `ReducedMotion` | Option; the system's "reduce animations" setting applies as well |
| `AnimateIdle` | The host's rule: `false` on Windows (ADR 0005, section 6) |
| `SetLevelAsync(level)` | The sound level: the microphone while listening, the voice while speaking, at most 30 times per second |

Output: `OnActivate`, the click. The start of a move (ADR 0005) stays with the WPF window of the Windows
host, which catches the press to tell a click from a move ([hosts](hosts.md#orb-window)).

The component exposes its look in `data-look` and its version in `data-quality`; the script sets
`data-motion` (`full` or `reduced`), and the high-quality script sets `data-loop` (`running` or `stopped`) on
its drawing. These are what the Playwright tests rely on.

## The "orb quality" setting

- Section `Assistant.Orb` of `config.json`, value `Quality`: `High` (default) or `Light`. Any other value is
  refused when saving.
- "Orb" page of the settings window, after "Language". The change applies at once to the shown orb:
  `OrbQualityKeeper` watches the section and `OrbView` swaps the drawing without a restart.
- Jarvis never changes the version on its own (ADR 0011, section 1).

## The eight looks

| Look | Color | Speed high quality / light | Light |
| --- | --- | --- | --- |
| Idle | cyan | 0 / 0 (still image) | dim |
| Listening | cyan | 0.8 / 1 | rises gently with the voice, never changes size |
| Thinking | violet | 2.4 / 3.5 | |
| Speaking | cyan | 1 / 1.2; the light version pulses at random, the high quality follows the voice by light | full |
| Long task | green | 0.45 / 0.5 | progress ring |
| Error | red | 0 / 0 (still image) | a flash on entry; the cause on hover |
| Microphone muted | grey | 0 / 0 (still image) | dim, "mic muted" label |
| Interaction required | amber | 0.6 / 0.7; both versions pulse | almost full; comes before the look of the phase ([ADR 0023](adr/0023-confirmation-request-in-a-turn.md)) |

- The color (`--orb-color`) and the light (`--orb-light`) of each look are in `Orb.razor.css`, for both
  versions. They are registered with `@property`: they fade (0.5 and 0.4 s), and the high-quality canvas
  reads them as a computed color and number.
- **Light version**, speed 1: one turn in 24 s for the inner ring, 36 s for the middle one (reversed), 60 s
  for the outer one (catalog, section 3).
- **High quality**, speed 1: each ring spins at the catalog's speed (section 2: +0.12, −0.35, +0.5, −0.7 and
  +0.9 rad/s), the whole gyroscope turns at 0.18 rad/s and rocks between −0.5 and −0.1 rad. The sphere
  spins and its clouds drift at the same pace.
- The speed eases (about 0.6 s for the light version, 0.7 s for the high quality), then nothing turns once it
  reaches zero.
- The amber decision badge and the progress ring are drawn over any look, in both versions.

## Cost

- **Both versions**, at speed 0 (idle, microphone muted, error): the animations are paused, no timer and no
  `requestAnimationFrame`. The sound level reaches the script only while listening (microphone) and speaking
  (voice): a still orb receives no call at all.
- **Light version:** the sphere and the glow are still CSS gradients. Only the three ring layers turn, with
  the Web Animations API on `transform`: the browser turns them with no script per frame.
- **High quality:** the five rings and the gyroscope also turn with the Web Animations API. Only the sphere
  (plasma, 18 filaments, 42 × 96 cloud cells) is drawn by script, in a canvas that covers the sphere only (a
  third of the orb's side), **at most 30 images per second** (ADR 0005, section 5). The loop runs only during
  a rotation, a speed change or the 0.7 s fade of a change of look; then it draws one last image and stops.
  The 0 and 1 of the clouds are small images prepared once per color and size, not text drawn each time. The
  halo is a still CSS gradient.
- **Indicative measure** (Linux container without a graphics card, headless Chromium, 160-pixel orb, main
  thread time): idle 0 % for both versions; listening, thinking or long task about 21 % for the high quality
  (7 % of it script) against under 1 % for the light one. Rendering there is entirely in software: only the
  measure on Windows (ADR 0011, section 8) counts.

## Provisional choices

Easy to change; to be confirmed by Benoit.

### Work item 6 (light version)

1. **Error:** the flash, then a still image (ADR 0011, section 5). The catalog said "almost still, speed
   0.25", written before the idle budget: not kept.
2. **Reduced motion** (ADR 0011, section 9, leaves it open): no rotation, no pulse, not even the easing of a
   speed change. Listening and speaking differ from idle by light only: dim when idle, brighter when
   listening, full when speaking. To be checked by eye.
3. **Progress ring without a percentage:** a still dashed green circle. The delegated tasks ADR (stage 2)
   will settle it, with the case of several tasks.
4. **Size:** the light orb's body takes 80 % of the 160-pixel window, leaving room for the glow.
5. **The badge** shows "99+" above 99 decisions.

### Work item 7 (high quality)

1. **The prototype's proportions are kept:** the sphere is 30 % of the orb's side, the rings reach out to
   96 %. In the 160-pixel window the sphere is therefore about 48 pixels and the cloud digits 1 to 3 pixels:
   they read as a texture, no longer as 0s and 1s. Enlarging the orb window will be decided after seeing it on
   Benoit's screen.
2. **Light shared by both versions:** each look's light values are the light version's (`Orb.razor.css`),
   not the high-quality prototype's (listening 1, speaking 1.15, muted 0.35). The differences are small.
3. **Error and reduced motion:** same rules as the light version (flash then still image; no rotation).
   Colors and light still fade.
4. **Gyroscope rocking:** a 25 s back-and-forth animation between −0.5 and −0.1 rad, instead of the
   prototype's sine tied to the rotation; the browser runs it with no script. The rings start at fixed angles
   (random in the prototype), so the orb looks the same at every start.
5. **Halo** as a still CSS gradient rather than drawn in the canvas; digits in JetBrains Mono 500 (the
   prototype's 600 weight is not embedded).
6. **"Orb quality" setting** on its own "Orb" settings page, after "Language". The "reduced motion" option is
   not there yet: only the system setting applies on the hosts.

## Demonstration page

`/orb` in the Web host (US1.3), linked from the home page. It drives the component directly by its inputs,
with no microphone and no model: both versions (high quality first), the eight looks, the number of tasks and
the progress (known or not), pending decisions, the cause of a fault, the sound level, reduced motion and the
animated idle look (to try only: forbidden on Windows). A counter shows the "click" output.

## Tests

- Unit: `OrbMotionTests` (speed of each look in both versions, animated idle as an option only, reduced
  motion), `OrbGeometryTests` and `OrbGyroscopeGeometryTests` (paths, ring speeds, numbers independent of the
  culture), `OrbQualityKeeperTests` (high quality by default, saved choice, change announced once, unknown
  value refused), `OrbActivationTests` (the click goes through the state machine), `OrbDoubleClickTests` (delayed
  single click, double click that is never a single click).
- Playwright (`OrbTests`): each look in `data-look`; light version: listening = three rotations, idle,
  muted and error = none, animated idle as an option, reduced motion from the system and the option; high
  quality: listening = seven rotations and a running drawing loop, idle, muted and error = no rotation and a
  stopped loop, speaking without pulses, reduced motion; version swapped in place; "orb quality" setting
  applied to the home page orb without reloading; badge and progress ring over speaking; cause of the fault on
  hover; click on the component; click on the home page orb = listening then cancel.

## Windows checks

To do at the next session with Benoit (the Web host does not prove them). Method of spike E6, WPF and
WebView2 processes together; results go into the project memory.

1. **Mandatory budget (ADR 0005, section 6):** idle, microphone muted and error under 2 % of a core, **for
   each of the two versions**.
2. **Measure of ADR 0011, section 8, for both versions:** cost of listening, thinking, speaking and the long
   task. If the high quality clearly exceeds 14 to 18 % of a core, or stutters, the light version becomes the
   default (one line in `OrbOptions`), and the choice is discussed again with Benoit.
3. The orb shows in the transparent window with no background or border, glow and rings included, in both
   versions, and clicks around it still reach the application below.
4. A click on the orb starts listening (the orb moves), a second one cancels it; a drag moves the orb without
   activating it.
5. With "Animation effects" off in Windows accessibility settings, the orb no longer turns while listening,
   in both versions.
6. In the settings, "Orb" page: switching from "High quality" to "Light" changes the orb at once, without a
   restart, and the choice stays after a relaunch.
7. The high quality is readable and beautiful at 160 pixels on Benoit's screen (work item 7 provisional
   choice 1); otherwise, note the wanted size.
