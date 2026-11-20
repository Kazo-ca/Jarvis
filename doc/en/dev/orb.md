# The orb, light version

Work item `stage1/6-orb` of the [stage 1 plan](plans/stage-1.md). Decisions:
[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md) (orb contract, animation rule) and
[ADR 0011](adr/0011-orb-colors-and-drawing.md) (colors, seven looks, drawing). Reference drawing: section 3
of the [catalog](ui/catalog.md) and the prototype [`orb-light.html`](../../../design/prototypes/orb-light.html).
The high-quality version comes with work item 7.

## The slice

`src/Kazo.Jarvis.Features/Assistant/Orb/`:

| File | Role |
| --- | --- |
| `Orb.razor` (+ `.razor.css`, `.razor.js`, `.resx`) | The component. It only shows its inputs. The SVG paths are computed in C# (`OrbGeometry`) |
| `OrbView.razor` | The orb as the hosts show it: fed by the state machine (`AssistantStateChanged`) and the sound level; its click publishes the activation |
| `OrbMotion.cs` | The motion rule of each look (ring speed, pulses), in C# |
| `OrbGeometry.cs` | The arcs, ticks and "teeth" of the three rings, in a 200 × 200 box |
| `OrbActivation.cs` | The click on the orb (US1.6): publishes `ActivationRequested(Click)` |
| `OrbSlice.cs` | Registration |

### Inputs and outputs of the `Orb` component

| Input | Source in `OrbView` |
| --- | --- |
| `Look` | `AssistantState.Look`, the kernel's look function |
| `DelegatedTasks` | `AssistantState.DelegatedTasks`; the progress ring is drawn above zero |
| `Progress` (0 to 1, or unknown) | Unknown for now: delegated tasks come at stage 2 |
| `PendingDecisions` | 0 for now (stage 2); the demonstration page simulates it |
| `Fault` | The cause of the fault condition, shown on hover |
| `ReducedMotion` | Option; the system "reduce animations" setting applies as well |
| `AnimateIdle` | The host's rule: `false` on Windows (ADR 0005, section 6) |
| `SetLevelAsync(level)` | The sound level: the microphone while listening, the voice while speaking, at most 30 times per second |

Output: `OnActivate`, the click. The start of a move (ADR 0005) stays with the WPF window of the Windows host,
which catches the press to tell a click from a move ([hosts](hosts.md#orb-window)).

The component exposes its look in `data-look`, and the script sets `data-motion` (`full` or `reduced`): these
are what the Playwright tests read.

## The seven looks

| Look | Color | Ring speed | Light |
| --- | --- | --- | --- |
| Idle | cyan | 0 (still image) | dim |
| Listening | cyan | 1 | rises gently with the voice, size unchanged |
| Thinking | violet | 3.5 | |
| Speaking | cyan | 1.2, and each ring pulses at random | full |
| Long task | green | 0.5 | progress ring |
| Error | red | 0 (still image) | a flash on entry; the cause on hover |
| Microphone muted | grey | 0 (still image) | dim, "mic muted" label |

- Speed 1: one turn in 24 s for the inner ring, 36 s for the middle one (opposite direction), 60 s for the
  outer one (catalog, section 3).
- The color fades from one look to the next in 0.5 s (`@property --orb-color`); the speed eases over about
  0.6 s, then nothing turns once it reaches zero.
- The amber decision badge and the progress ring are drawn over any look.

## Cost

- The sphere and the glow are still CSS gradients. Only the three ring layers rotate, with the Web Animations
  API on `transform`: the browser turns them with no script per frame.
- At speed 0 the animations are paused: no timer and no `requestAnimationFrame`. A `requestAnimationFrame`
  only runs during the 0.6 s of a speed change.
- The sound level reaches the script only while listening (microphone) and speaking (voice): a still orb
  receives no call.

## Provisional choices of this work item

Easy to change; for Benoit to confirm.

1. **Error:** the flash, then a still image (ADR 0011, section 5). The catalog said "almost still, speed
   0.25", written before the idle budget: not kept.
2. **Reduced motion** (left open by ADR 0011, section 9): no rotation, no pulse, not even the easing of a
   speed change. Listening and speaking differ from idle by light only: dim when idle, brighter when
   listening, full when speaking. To be validated by eye.
3. **Progress ring without a percentage:** a still dashed green circle. The delegated tasks ADR (stage 2)
   decides it, with the case of several tasks.
4. **Size:** the orb body takes 80 % of the 160 pixel window, leaving room for the glow.
5. **The badge** shows "99+" above 99 decisions.
6. **The "orb quality" setting** (high or light) does not exist yet: it comes with the high-quality version in
   work item 7. The component has no "version" input yet.

## Demonstration page

`/orb` in the Web host (US1.3), linked from the home page. It drives the component directly by its inputs,
with no microphone and no model: the seven looks, the number of tasks and the progress (known or not), the
pending decisions, the cause of an error, the sound level, reduced motion, and the animated idle look (for
trying only: forbidden on Windows). A counter shows the "click" output.

## Tests

- Unit: `OrbMotionTests` (speed of each look, animated idle only as an option, reduced motion),
  `OrbGeometryTests` (paths, culture-independent numbers), `OrbActivationTests` (the click goes through the
  state machine).
- Playwright (`OrbTests`): each look in `data-look`; listening = three running rotations; idle, muted and
  error = none; animated idle as an option; reduced motion from the system and from the option; badge and
  progress ring over speaking; error cause on hover; click output of the component; a click on the home
  page orb starts listening, a second one cancels it.

## Windows checks

For the next session with Benoit (the Web host does not prove them):

1. **Mandatory budget (ADR 0005, section 6):** idle, microphone muted and error under 2 % of a core with the
   light version, WPF and WebView2 processes together, method of spike E6.
2. Cost measured for listening, thinking, speaking, and the long task that rotates slowly for the whole task
   (ADR 0011, section 5); write them in the project memory.
3. The orb shows in the transparent window with no background or border, glow included, and clicks around
   it still reach the application below.
4. A click on the orb starts listening (the orb moves), a second one cancels it; a drag moves the orb
   without activating it.
5. With "Animation effects" turned off in the Windows accessibility settings, the orb no longer rotates
   while listening.
