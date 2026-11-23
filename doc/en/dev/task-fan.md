# Task fan and gestures on the orb

Work item `stage2/14-task-fan`. Decisions: [ADR 0010](adr/0010-task-fan-and-orb-click.md) (the fan),
[ADR 0027](adr/0027-orb-gestures.md) (left click, right click and double click on the orb; replaces section 3
of ADR 0010), [ADR 0013](adr/0013-relais-readiness.md), section 6 (one panel per task, its
sub-tasks inside, amber when it waits), [ADR 0011](adr/0011-orb-colors-and-drawing.md) (cyan, amber, badge)
and spike E11 ([results](../../../spikes/e11-task-fan-window/RESULTS.md)).

## What the user sees

- **A right click on the orb** makes the panels of the tasks in progress come out of the orb, on an arc,
  toward the centre of the screen. A dashed line links each panel to the orb; the desktop behind is darkened.
- **A second right click, the Escape key or a click on the darkened desktop** puts them back in.
- A panel shows a task: who works, on which project, who started it (Jarvis or the user), its title, its
  state, the time of its last change, its last message and its sub-tasks.
- A panel that waits for the user is **amber**, and so is its line.
- When a session started by Jarvis asks for a permission, its panel shows the request and the buttons
  **Allow**, **Refuse** and **Details**. The confirmation window stays open: both do the same thing.
- When a session works in a folder that belongs to no project, its panel shows the button
  **Folder with no project: choose**. It closes the fan and opens the "Projects" page, where the request waits
  ([projects.md](projects.md)). The button does not show during a permission request.
- Near the orb, a line counts the tasks and the decisions that wait. Without a task, the fan opens anyway and
  says "No task in progress.".
- **A left click on the orb** starts listening, as the wake word does; while listening, it cancels it.
- **A double click on the orb** opens the projects view ([projects-view.md](projects-view.md)). The left click
  therefore waits for the double-click delay of Windows (half a second by default) before it starts listening;
  the wake word, the hotkey and the right click are never delayed. In a host with no projects view there is
  neither double click nor delay.
- **The amber badge of the orb** gives the number of decisions that wait, fan open or closed. The orb takes the
  "long task" look as long as a task works.

The fan is not a state of the assistant: Jarvis can listen, think or speak while it is open.

## Kernel messages and contracts

| Element | Role |
| --- | --- |
| `TaskFanToggleRequested` (`Kernel/Messages/Desktop`) | The right click on the orb: the fan slice opens the fan or puts it away |
| `ProjectsViewRequested` (`Kernel/Messages/Desktop`) | The double click on the orb: the slice `Projects/View` answers it by opening the projects view |
| `IPointerTimings` (`Kernel/Desktop`) | The double-click delay, given by the host; without it, no double click and no delay |
| `ITaskFanSurface`, `FanAnchor` (`Kernel/Desktop`) | The surface the fan is drawn on: where the centre of the orb is, show, hide |
| `IOpenConfirmations`, `OpenConfirmation` (`Kernel/Permissions`) | The open confirmation request and its click, for whoever shows it besides the window |
| `ConfirmationRequest.TaskId`, `SessionAction.TaskId` | The task that asks, to place the request in its panel |
| `DelegatedTaskCountChanged` (already in ADR 0005) | The number of tasks that work, now published by the board |

## The click on the orb (`Assistant/Orb`)

`OrbActivation.ClickAsync` is called once per left click and `OrbActivation.RightClickAsync` once per right
click, by the component (Web host) or by the orb window (Windows host).

- A right click publishes `TaskFanToggleRequested` at once. The component holds the menu of the browser back;
  the orb window keeps the right click for itself, the web view does not receive it.

- The first click waits for the delay of `IPointerTimings`, then publishes `ActivationRequested(Click)`.
- A second click inside that delay publishes `ProjectsViewRequested`; the first one publishes nothing. **A
  double click is never also a single click.**
- Without `IPointerTimings` (mobile), or as long as no slice is registered for `ProjectsViewRequested`,
  there is no delay: the click acts at once.
- `OrbView` computes the badge: the tasks that wait for the user, plus the open confirmation request when it
  does not come from one of those tasks.

## Slice `Tasks/Fan`

| File | Role |
| --- | --- |
| `TaskFan` | Open, closing or closed; receives the right click; shows and hides the surface |
| `FanContent` | What the fan shows, from the board of the tasks and the open request |
| `FanLayout` | Where the panels go: the side of the centre of the screen, angles per number of panels |
| `TaskFanView` | The drawing, in one surface |

- **Content.** One panel per task that is not over (sub-tasks aside), the ones that wait first, then the most
  recently changed. **Five panels at most**; the others are counted in the line near the orb. Two sub-tasks at
  most per panel, then "and N more".
- **Folder with no project.** `TaskFanView` asks `IProjectCatalog.IsWithoutProject(task.Folder)` (kernel;
  false with no catalog) and draws again when the catalog changes. The button publishes
  `SettingsWindowRequested(IProjectCatalog.SettingsPageKey)`: the slice of the fan does not know the one of
  the projects. The panel does not turn amber and the counting line does not change.
- **Confirmation request.** It is shown in the panel of the task that asks (`TaskId`). A request that comes
  from no task (a tool of the local model) has an amber panel of its own. The task that asks always has its
  panel, even beyond five.
- **Layout.** Lengths are in `em`; the text size follows the smaller side of the surface (16 px at most), so
  the same arc fits a small screen. Outside a corner, the fan takes the arrangement of the nearest corner.
- **Drawing.** An SVG whose origin is the centre of the orb: lines, panels (`foreignObject`) and counting line
  are in one group. Opening and closing scale that group around the orb.
- **Animation** (ADR 0010, section 6): 400 ms to open in 12 steps, 300 ms to close in 9 steps, hence 30 images
  per second at most. Open, nothing moves. Reduced motion (Windows or browser): no animation.
- **A task that changes** redraws the fan at most once per second. A confirmation request that opens or closes
  is drawn at once.
- **Closed**, the component draws nothing, does not read the board and holds no timer.

## Slice `Tasks/Board`

`TaskCount` publishes `DelegatedTaskCountChanged`: the number of `pending` or `working` tasks, sub-tasks aside.
A task that waits is shown by the badge; a task whose turn ended no longer works.

## Windows host

| File | Role |
| --- | --- |
| `Fan/FanWindow` | Transparent, borderless window, always on top, out of the taskbar |
| `Fan/FanWindowService` | `ITaskFanSurface`: places the window over the work area of the screen of the orb |
| `Fan/FanShell.razor`, `wwwroot/fan.html` | The root of the web view of the fan, transparent background |
| `Desktop/SystemPointerTimings` | The double-click delay set in Windows (`GetDoubleClickTime`) |

- The window is **created once, after the orb, and kept hidden** (spike E11). For its web view to start, it is
  shown once, empty, then hidden as soon as the web view exists.
- When the fan opens, the window takes the keyboard focus (for Escape), then the orb is put back above. At each
  activation of the fan window, the orb is put back above (pitfall of spike E11).
- Escape is caught by the WPF window, whatever has the focus in the page.
- The orb window is not enlarged.
- If the desktop is lost (locked session), the fan window is closed with the orb window and created again
  afterwards.

## Web host

- The orb is now in a corner of the page, above everything, as on the desktop. The button "Move the orb to the
  next corner" changes its corner.
- `SimulatedTaskFanSurface`: simulated surface and double-click delay (300 ms).
- `TaskFanView` is drawn over the page. `#task-fan` tells whether the surface is shown and which corner the
  orb is in.
- "Make it start a sub-agent" adds a sub-task to the session started by hand.

## Provisional choices of this work item

ADR 0010 (section 9) leaves these points open. To be confirmed by Benoit; each can change without touching the
others.

1. Five panels at most; the other tasks are counted in one line.
2. No ✕ on the panels: a task is stopped from the "Claude Code tasks" page.
3. A task whose turn ended (`idle`) or that is blocked keeps its panel until it is over.
4. The orb counts as "long task" the `pending` and `working` tasks only.
5. The fan opens over the work area of the screen the orb is on; nothing on the other screens.
6. A triple click opens the fan, then counts as a single click.

## Not verified

- **Nothing ran on Windows.** The fan window, its hidden start, the focus, the orb put back above and the way
  clicks pass were only checked by the compiler.
- The measures of ADR 0010 (section 7) are not redone on the real host: spike E11 measured an HTML page, not
  the Razor views.
- The smoothness of the animation and the comfort of the delayed single click, for Benoit to judge.

## Tests

- Unit: `OrbDoubleClickTests` (right click, delayed single click, double click, two clicks apart, immediate click
  without a projects view), `TaskFanTests` (opening
  and closing, opening again while closing, corners, panels that never overlap, content, request in the panel
  of its task, request apart, more than five tasks, count of the tasks that work).
- End to end: `TaskFanTests` (4 scenarios on the Web host).

## Windows checks

To do with Benoit, on his PC:

1. Right click on the orb: the fan comes out with no visible delay, the desktop darkens, Jarvis does not
   listen.
2. Second right click, Escape, click on the darkened desktop: the fan goes back in; the application below does
   not receive the click.
3. Fan closed: clicks reach the application below, anywhere on the screen.
4. Click on a panel, then right click on the orb: the orb stayed above.
5. A session started by Jarvis asks for a write: "Allow" in its panel lets it go on.
6. The orb in each of the four corners, then on another screen: the fan opens toward the centre.
7. Left click: listening starts at once; no menu opens on a right click.
8. Cost (method of spike E6): fan closed, open and still, while opening and closing.
9. At start-up, the prepared fan window is not seen and takes no click.
