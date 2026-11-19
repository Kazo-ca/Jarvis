# Build and test

2026-11-19 · Work item `stage1/1-solution`. Based on [ADR 0001](adr/0001-target-dotnet-10-and-projects.md)
(projects) and [ADR 0002](adr/0002-vertical-slices-kernel-and-dependency-rules.md) (dependency rules).

## Prerequisites

- .NET SDK **10.0.402** or a newer .NET 10 feature band (pinned by `global.json`, `rollForward: latestFeature`).
- To run the Windows host: Windows 10 version 2004 or later, x64, and the WebView2 runtime.
- For the Playwright tests: Playwright's Chromium, installed once (below).

## Layout

```
Kazo.Jarvis.slnx
global.json
Directory.Build.props        product version, compiler options
Directory.Packages.props     package versions of src/ and tests/
src/Kazo.Jarvis.Kernel       kernel: messages, state machine, contracts
src/Kazo.Jarvis.Features     vertical slices: Features/<Domain>/<Slice>/
src/Kazo.Jarvis.Adapters     real implementations of the contracts (Windows host only)
src/Kazo.Jarvis.Windows      WPF + BlazorWebView host
src/Kazo.Jarvis.Web          Web test host, simulated services, Playwright target
tests/Kazo.Jarvis.Tests.Unit unit tests and architecture tests
tests/Kazo.Jarvis.Tests.E2E  Playwright tests against the Web host
.github/workflows/ci.yml     continuous integration on Linux
```

`Updater` comes with work item 16, `Mobile` at stage 4. The spikes under `spikes/` stay out of the solution
and keep their own package versions (`spikes/Directory.Packages.props`).

## Commands

From the repository root:

| Goal | Command |
| --- | --- |
| Build everything | `dotnet build Kazo.Jarvis.slnx` |
| Unit and architecture tests | `dotnet test tests/Kazo.Jarvis.Tests.Unit` |
| Install Playwright's browser (once, after a build) | `pwsh tests/Kazo.Jarvis.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install chromium` |
| Playwright tests | `dotnet test tests/Kazo.Jarvis.Tests.E2E` |
| Run the Windows host | `dotnet run --project src/Kazo.Jarvis.Windows` |
| Run the Web host | `dotnet run --project src/Kazo.Jarvis.Web` (http://localhost:5180) |

The Playwright tests start the Web host themselves on a free port: nothing to run first.

## Windows-only tests

A test that needs Windows (Windows vault, windows, real audio) carries `[Category("Windows")]`. Continuous
integration leaves them out (`--filter "TestCategory!=Windows"`); they run on Benoit's PC, where an agent
does each work item's Windows checks through Remote Control.

## Continuous integration

`.github/workflows/ci.yml` runs on Linux (answer no. 2 of the [stage 1 plan](plans/stage-1.md)) on every PR,
and on every push to `main` and to a `*/base` branch:

1. installs the SDK from `global.json`;
2. builds the whole solution in Release, Windows host included (`EnableWindowsTargeting`): it is compiled,
   not run;
3. runs the unit and architecture tests, except those marked `Windows`;
4. installs Chromium and runs the Playwright tests against the Web host;
5. keeps the test results for a week when something fails.

A PR is merged into a `*/base` branch only when this run is green (AGENTS.md, IV.5 and IV.7).

## Architecture tests

In `tests/Kazo.Jarvis.Tests.Unit/Architecture/`. They are part of the merge bar and are never disabled
(ADR 0002, section 7).

| Rule | Test |
| --- | --- |
| Direction of the project references; the Web host does not load `Adapters`; every project under `src/` has its rule | `ProjectReferenceTests` (`.csproj` files) |
| `Features` references neither `Adapters` nor a host; `Adapters` does not reference `Features`; the kernel references only the base library and the `Microsoft.Extensions` abstractions | `AssemblyReferenceTests` (compiled assemblies) |
| No package other than `Microsoft.Extensions.*.Abstractions` in the kernel; no project references a spike | `ProjectReferenceTests` |
| No Razor component nor style or script file in the kernel | `KernelContentTests` |
| A slice uses no type of another slice (`Features/Shared/` excepted), method bodies included; no type directly in a domain folder | `SliceIsolationTests` (NetArchTest) |
| No `<style>` nor `style="…"` in a `.razor` file (script is allowed there, ADR 0012) | `RazorMarkupTests` |

Each rule about code is also run on samples that break it (`SliceIsolationSamples.cs`, the cases of
`RazorMarkupTests`): it is proven able to fail while the projects are still nearly empty.

Script is allowed in markup, to manipulate the DOM without restriction
([ADR 0012](adr/0012-scripts-allowed-in-markup.md)); only style stays forbidden there.
