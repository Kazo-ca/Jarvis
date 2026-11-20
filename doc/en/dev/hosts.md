# Hosts

2026-11-19 · Work item `stage1/4-hosts`. Builds on [ADR 0003](adr/0003-hosts-and-native-services.md)
(hosts and native services) and [ADR 0013](adr/0013-relais-readiness.md), section 4 (.NET generic host).

## Desktop contracts

In the kernel, `Kazo.Jarvis.Kernel.Desktop`. None names a platform type.

| Contract | Used to | Windows host | Web host |
| --- | --- | --- | --- |
| `IOrbWindow` | Show, hide, toggle the orb | `OrbWindowService`: transparent WPF window | `SimulatedOrbWindow`: the orb in the page |
| `IDialogService` | Open a component in its window, one per key | `WindowDialogService`: one window and web view per key, same service provider | `OverlayDialogService`: a layer over the page |
| `IGlobalHotkey` | Register the global hotkey and receive its presses | `Win32GlobalHotkey`: `RegisterHotKey` on a message-only window | `SimulatedGlobalHotkey`: a button of the page |
| `ILaunchAtLogin` | Start with Windows | `RegistryLaunchAtLogin`: value `Kazo.Jarvis` of the user's `Run` key | `InMemoryLaunchAtLogin` |

`HotkeyGesture` reads and writes a hotkey as in the settings (`Ctrl+Alt+J`). Ctrl, Alt or Win is required,
so that typing in another application never triggers it by accident.

The tray icon has no contract: no slice needs one (ADR 0003, section 5, "a contract is added when a slice
needs it"). Its menu publishes `SettingsWindowRequested` and `ConversationWindowRequested`
(`Kernel.Messages.Desktop`); an entry stays greyed out while no slice handles its event. "Mute the
microphone" stays greyed out until work item 8.

## Slices

| Slice | Setting | Role |
| --- | --- | --- |
| `Features/Desktop/Hotkey` | `Desktop.Hotkey`: `gesture`, default `Ctrl+Alt+J` | Registers the hotkey at start-up and on each change of the setting; a press publishes `ActivationRequested(Hotkey)` |
| `Features/Desktop/LaunchAtLogin` | `Desktop.LaunchAtLogin`: `enabled`, default `true` (assumption no. 3) | Applies the setting at start-up and on each change |

Both are background services (`IHostedService`): the Windows host starts them after the orb is shown, the
Web host when its application starts.

## Windows host

### Start-up

1. **Single instance**: a named mutex `Local\Kazo.Jarvis.Instance` (per Windows session). A second launch
   signals the event `Local\Kazo.Jarvis.ShowOrb`, which shows the orb of the running instance, then ends.
2. **Generic host**: `Host.CreateApplicationBuilder` without the defaults (no `appsettings.json`, no console:
   `config.json` stays the only settings file, ADR 0004). The technical log goes to
   `%USERPROFILE%\.kazo\jarvis\logs\jarvis-YYYY-MM-DD.log`, kept 14 days. The lifetime is the WPF
   application's (`DesktopLifetime`), not a console's.
3. **WebView2**: when it is missing, a message box says so and offers the download page, then the
   application ends.
4. **Tray icon**: shows at once, even on a locked session, so the user can quit.
5. **Orb**: opened as soon as the session desktop can show it (see below).
6. **Background services**: `IHost.StartAsync` runs on a worker thread when the orb is first drawn, without
   blocking the UI thread.

**Quit** hides the icon, closes the windows, stops the background services with `StopAsync` within 10
seconds, then ends the application.

### Locked session

On a locked session, Direct3D 9 is not available and `WebView2CompositionControl` cannot create its device
([pitfalls](pitfalls.md)). So the host does not create the orb while the desktop receiving the input is not
`Default` (on the lock screen it is `Winlogon`). It checks every second, waits until the desktop has been
ready for 2 seconds, then opens the orb. While waiting:

- a warning line in the log says why the orb is not there;
- the tray icon tooltip says "waiting for the session to be unlocked".

If the web view still loses Direct3D later (lock while drawing, resume from sleep: `D3DERR_NOTAVAILABLE` or
`D3DERR_DEVICELOST`), the error is caught and logged, the orb window is closed and a new one opens when the
desktop is ready again. Any other unhandled error is logged, then a message box gives the log path before
the application stops: never a silent crash.

### Orb window

- Transparent (`AllowsTransparency`), borderless, always on top, out of the taskbar and of Alt+Tab
  (`WS_EX_TOOLWINDOW`), never takes the focus (`ShowActivated = false`, `WS_EX_NOACTIVATE`).
- 160 × 160 device-independent pixels; the provisional orb is a 96 pixel disc in the centre. The transparent
  pixels around it let clicks through.
- Pressing and dragging moves the window (`DragMove`); the position is saved in the `Desktop.OrbWindow`
  section of `config.json`, owned by the host. At start-up, a position outside every screen is replaced by
  the bottom right corner of the work area.
- Pressing without moving (under 4 pixels) is a click: it publishes `ActivationRequested(Click)`. This is
  provisional: the orb slice (work item 6) takes the click over in its component.
- The web view has a transparent background (`DefaultBackgroundColor` and
  `WEBVIEW2_DEFAULT_BACKGROUND_COLOR`).
- The WebView2 profile of every window goes to `%USERPROFILE%\.kazo\jarvis\webview`, not next to the
  executable (the installation folder may not be writable). This folder is not in the list of ADR 0004:
  WebView2 creates it.

### Start with Windows

Value `Kazo.Jarvis` of `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, rewritten at start-up when the
executable moved. **A development build** (run from `bin\Debug` or `bin\Release`) **does not register
itself**, so that trying a branch never leaves it starting at every sign-in. To check the registration from
such a build: environment variable `KAZO_JARVIS_REGISTER_STARTUP=1`. Turning the setting off removes the
value in every case.

## Web host

The home page shows the provisional orb and three buttons: simulate the hotkey, show or hide the orb, open a
sample dialog. It exposes the assistant phase in `data-phase`, and `data-interactive="true"` once the
interactive circuit is ready (the page is first rendered on the server: an earlier click is lost). Dialogs
show below the buttons, which stay usable.

## Windows checks

What the Web host does not prove (stage 1 plan, work item 4):

1. Clicks around the orb reach the application below.
2. The hotkey works while another application has the focus.
3. A second launch shows the orb already open, without a second icon.
4. The position comes back after a restart.
5. Cost of the still window (method of spike E6).
6. Started on a locked session, the host waits and opens the orb at the unlock.
7. Quit ends the process in under 10 seconds.
