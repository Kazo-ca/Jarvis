# Pitfalls

Pitfalls met during development and how they were solved. Add an entry as soon
as a problem took time to diagnose.

## sherpa-onnx 2.0 no longer ships espeak-ng (spike E4)

With `org.k2fsa.sherpa.onnx` 2.0.x, Piper and non-English Kokoro fail: "phoneme_codepoints is empty, no
lexicon, and no frontend available". Version 2.0 removed espeak-ng and piper-phonemize (PR 4055). Stay on
**1.13.8**, or provide the phonemes yourself. espeak-ng is GPLv3: keep it in mind for the license.

## Multilingual Kokoro: no `en-gb` language (spike E4)

The British voice `bm_george` is used with `Lang = "en-us"` and the `lexicon-us-en.txt` lexicon.
`en-gb` makes espeak-ng fail ("Failed to set eSpeak-ng voice").

## qwen2.5-coder writes its tool calls as text (spike E7)

The model returns `{"name": ..., "arguments": ...}` in `content` instead of `tool_calls`. Use qwen3 (with
`think: false`) or a lenient parser that only accepts a whole JSON object naming a declared tool.

## An animated transparent WPF window is expensive (spike E6)

`AllowsTransparency=True` creates a layered window: every changed frame is copied by the CPU. With
`WebView2CompositionControl`, the capture adds 4 to 5 % of one core as soon as the page moves. Keep the
orb still when idle.

## `requestAnimationFrame` wakes the engine even without drawing (spike E6)

A loop that skips frames still wakes the engine 60 times a second. Below 10 frames per second, use a timer.

## The command-line Claude Code sign-in expires on its own (spike E8)

Claude Desktop sessions stay signed in while `claude -p` answers "OAuth session expired". Kazo.Jarvis must
detect that message and ask to run `/login` again, instead of failing silently.

## NAudio 3 uses `Span<float>`

`ISampleProvider.Read` takes a `Span<float>`. Samples written for NAudio 2 no longer compile.

## NAudio 3: `WasapiCapture` and `WasapiOut` are obsolete (work item stage1/8-audio-devices)

They raise warning CS0618, and the build must stay free of warnings. Use `WasapiRecorderBuilder` and
`WasapiPlayerBuilder`. `DataAvailable` then receives a `ReadOnlySpan<byte>` directly (no more
`WaveInEventArgs`); `MMDevice.AudioClient` is obsolete too (`WasapiPlayer.DeviceMixFormat` gives the mix
format). The events of a recorder or player go through the synchronization context of the thread that
creates it: create it on a pool thread, never on the UI thread.

## An NUnit `[SetUpFixture]` only covers its own namespace (work item stage1/1-solution)

Placed in `Kazo.Jarvis.Tests.E2E.Drivers`, the Web host start-up did not run for the tests of
`Kazo.Jarvis.Tests.E2E.Scenarios`: the address stayed the default one and Chromium answered
`ERR_CONNECTION_REFUSED`. Put it in the root namespace of the test assembly.

## `WebApplicationFactory.UseKestrel(0)` does not give the chosen address (work item stage1/1-solution)

After `StartServer()`, `ClientOptions.BaseAddress` stays `http://localhost/`. Read the real address from
`IServer.Features.Get<IServerAddressesFeature>()`.

## A root `Directory.Packages.props` also applies to the spikes (work item stage1/1-solution)

Central package management breaks the spikes' `.csproj` files, which give their own versions.
`spikes/Directory.Packages.props` turns it off for them (`ManagePackageVersionsCentrally` set to `false`).

## The Windows host dies when it starts on a locked session (work item stage1/1-solution)

`WebView2CompositionControl` creates a Direct3D 9 device when it loads. On a locked desktop Direct3D 9 is not
available: `COMException 0x8876086A` (D3DERR_NOTAVAILABLE) in `Direct3DHelper.CreateD3D9Device`, uncaught,
and the process ends with code `0xC000041D`, without a window. Unlocked, the host starts normally. Starting
with Windows and waking from sleep can hit this case: work item `stage1/4-hosts` must wait for the unlock (or
retry) instead of dying. `LogonUI.exe` also runs in a Remote Desktop session: it is not a good lock signal.
**Fixed in work item `stage1/4-hosts`**: the host waits until the session is unlocked (`WTSSessionInfoEx`; the
input desktop stays `Default` on the Windows 10 and 11 lock screen) before creating the orb, and catches `D3DERR_NOTAVAILABLE` or `D3DERR_DEVICELOST` by opening the orb again later
([hosts](hosts.md#locked-session)).

## An `OperatingSystem.IsWindows()` guard does not cover a lambda (work item stage1/3-settings-storage-vault)

Analyzer CA1416 flags a `[SupportedOSPlatform("windows")]` type created in a registration lambda
(`services.TryAddSingleton(provider => new CredentialManagerVault())`), even inside an
`if (OperatingSystem.IsWindows())`: the lambda runs later, outside the guard. Put those registrations in a
method marked `[SupportedOSPlatform("windows")]`, called under the guard.

## NUnit 4 skips `[SupportedOSPlatform("windows")]` tests elsewhere (work item stage1/3-settings-storage-vault)

On Linux, a test or a class marked `[SupportedOSPlatform("windows")]` is skipped, not failed. Windows tests
also carry `[Category("Windows")]`, so that continuous integration leaves them out explicitly and the agent on
Benoit's PC can run them alone (`--filter "TestCategory=Windows"`).

## A Playwright click on a prerendered Blazor page is lost (work item stage1/4-hosts)

An `InteractiveServer` page is first rendered on the server: the buttons are there, but their `@onclick`
answer only once the SignalR circuit is up. An earlier click does nothing, and the test fails at random.
Wait for a sign of interactivity: the Web host home page exposes
`data-interactive="@RendererInfo.IsInteractive"`, and the tests wait for `true` before clicking.

## Windows Forms and WPF in one project: `Application` becomes ambiguous (work item stage1/4-hosts)

`UseWindowsForms` (for the tray icon) adds the global usings `System.Windows.Forms` and `System.Drawing`,
whose `Application`, `MessageBox` and `Color` clash with WPF's. Remove them in the `.csproj`
(`<Using Remove="System.Windows.Forms" />`, same for `System.Drawing`) and write the usings by hand in the few
files that need them.

## `BackgroundService` starts `ExecuteAsync` on another thread since .NET 10 (work item stage1/16-updates)

Since .NET 10, `BackgroundService.StartAsync` starts `ExecuteAsync` on a pool thread instead of running it up
to its first `await`. With `FakeTimeProvider`, the first `Task.Delay` may not exist yet when the test moves
the clock, and the wait is shifted. After `StartAsync`, let the loop reach its wait (a few real
milliseconds) before calling `Advance`.

## WPF position and screens of different scales (work item stage1/4-hosts)

On Benoit's PC (screens at 150 % and 100 %), the orb position saved from `Window.Left`/`Top` (-639, 387) came
back as (-959, 581) after a restart: × 1.5 at each launch. Before the window shows, WPF converts `Left`/`Top`
with the scale of the primary screen, not the target one. Save the position in physical pixels
(`GetWindowRect`) and restore it with `SetWindowPos` in `SourceInitialized`, before the window shows: Windows
then sends the scale change and WPF resizes the window.

## `@page.Key` in a Razor file is read as the `@page` directive (work item stage1/5-shared-ui-language)

A loop variable named `page` gives `@page.Key` in the markup, which Razor reads as a misplaced `@page`
directive (errors RZ2005 and RZ9979). Name the variable otherwise (`item`).

## Starting the Web host by hand without a profile: `Development` environment required (work item stage1/5-shared-ui-language)

`dotnet run --no-launch-profile` starts in `Production`, where `MapStaticAssets` looks for the published files
(`blazor.web.js`, `_content/...`) in `wwwroot` and does not find them: the page never becomes interactive.
Add `ASPNETCORE_ENVIRONMENT=Development` and start from the project folder (content root).

## Two native `onnxruntime`: sherpa-onnx's and `Microsoft.ML.OnnxRuntime`'s (work item stage1/9-wake-word)

`org.k2fsa.sherpa.onnx` 1.13.8 ships its own `onnxruntime.dll` / `libonnxruntime.so` (version 1.28.2), and
`Microsoft.ML.OnnxRuntime` 1.32.0 another one, under the same name. In one output folder, the second
overwrites the first. On Linux, `libsherpa-onnx-c-api.so` then needs the `OrtGetApiBase@VERS_1.28.2` symbol
that 1.32 lacks: Piper and Silero no longer load ("Unable to load shared library 'sherpa-onnx-c-api'").
Fix of work item 9: the wake word takes `Microsoft.ML.OnnxRuntime.Managed` **1.28.0** (the .NET part only)
and runs on sherpa-onnx's 1.28.2 native engine; one `onnxruntime` in the process. A managed version newer
than the native one fails (`GetApi` returns null): raising one means raising the other.

## A dependency circle through factories freezes the start with no error (stage 2 acceptance)

The container only detects a circle between constructors. With factories
(`provider => new X(provider.GetRequiredService<Y>())`), it goes down until the stack is full, moves to
another thread and waits for the lock of the first one: `host.StartAsync()` never returns, with no exception
and no log line. Seen at the acceptance: local server → `IClaudeApprovals` → session runner → `IClaudeCode` →
local server. The wake word, the MCP servers and the first run never started, while the orb answered. The Web
host did not show it: its simulated `IClaudeCode` does not need the local server.

Rule: an adapter that needs a service given by a slice asks for it **at the time of the call** (`Func<T?>`),
not when it is built. The test `AdaptersRegistrationTests` builds the background services with the real
adapters and a time limit.

Same source: an object handed out by several factory registrations (itself, its interface, the hosted
service) is disposed once per registration. Its `DisposeAsync` must accept several calls.

## The end of a component is not a gesture of the user (stage 2, fan test failing on CI only)

The confirmation window refused its request in `Dispose`, reading the end of the component as "the user closed
the window". In the Web host the dialogs live in the host: **every** connected page draws them, also a page
whose browser is gone and whose connection the server has not dropped yet. When the server ended such a
connection while a request was open, its components were disposed and the request was refused with no click.
On GitHub the pages of the earlier tests were dropped later than locally, so a test failed there only: the
window "never appeared" or was gone a moment later. The same would happen when `LanguageScope` builds the
content again for another language.

Rule: a component never answers for the user in `Dispose`. What the user closes is told by the host
(`IDialogService.Closed`). And in the Web host, remember that a component can exist once per connected page.
