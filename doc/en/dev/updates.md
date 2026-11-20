# Updates

Work item `stage1/16-updates` (US1.18, [ADR 0001](adr/0001-target-dotnet-10-and-projects.md), section 3).
A mechanism of Kazo.Jarvis's own, copied from Kazo Bar's and adapted ([ADR 0014](adr/0014-license-kazo-bar-rule-and-jarvis-py.md)):
the application checks the versions and downloads, `Kazo.Jarvis.Updater` replaces the files and restarts.

## Journey

1. The user opens **Updates…** in the tray menu (or clicks the notice of a new version). The menu publishes
   `UpdatesWindowRequested`; the slice opens its window through `IDialogService`.
2. **Check for updates** reads the published releases of `Kazo-ca/Jarvis` on GitHub, without a token (the
   repository is public). The newest one above the running version with a package
   `Kazo.Jarvis-<version>-win-x64.zip` is offered, with a link to its notes.
3. **Install version X** downloads the package into `%USERPROFILE%\.kazo\jarvis\updates\`, compares it with
   the SHA-256 digest given by GitHub, then unpacks it. Jarvis stays open meanwhile: a slow or failed download
   takes nothing away.
4. The Windows host starts `Kazo.Jarvis.Updater.exe` **from the unpacked folder of the new version**, then
   quits as through the menu (10 seconds at most).
5. The updater waits for Jarvis to end (30 seconds, then ends it if it runs from the installation folder),
   moves the files of the running version into `.kazo-update-backup-<date>` in the installation folder,
   copies the new version, then starts Jarvis again. On any failure it puts the previous version back,
   starts it and shows a message.
6. At the next start, Jarvis deletes the copy of the previous version and the `updates\` folder.

The updater's log is `%USERPROFILE%\.kazo\jarvis\logs\updater.log`. Settings and data
(`%USERPROFILE%\.kazo\jarvis`) are never touched: the updater refuses an installation folder overlapping them.

## Where the code is

| Part | Location |
| --- | --- |
| Contracts `IReleaseSource`, `IUpdateLauncher`, data `PublishedRelease`, `ReleasePackage` | `Kernel/Updates` |
| Messages `UpdatesWindowRequested`, `UpdateFound` | `Kernel/Messages/Updates` |
| Slice: versions, choice of the version, service, automatic check, window, texts | `Features/Maintenance/Updates` |
| Reading GitHub, download, digest, unpacking | `Adapters/Updates/GitHubReleaseSource` |
| Starting the updater, notice, clean-up | `Windows/Updates` |
| Simulations (published versions, network cut, recorded launch) | `Web/Simulated` |
| Updater (no project reference) | `src/Kazo.Jarvis.Updater` |
| Release script | `build/Publish-Release.ps1` |

The code of `Kazo.Jarvis.Updater/Installing` uses .NET only: it is also compiled into the unit tests, which
run on Linux.

## Choices ADR 0001 does not settle

- **Automatic check off by default** (setting `Maintenance.Updates`, `Automatic`): with the default settings
  nothing leaves the PC (stage 1 plan, work item 17). Once on, it runs a minute after start then every
  6 hours, and a tray notice announces each new version once. **Provisional choice, asked to Benoit on
  November 20, 2026**; changing the default is one line (`UpdateOptions.Automatic`).
- **Pre-releases offered** (`IncludePreReleases`, on) while Kazo.Jarvis is in 0.x and only publishes
  pre-releases.
- **The updater starts from the new version**, not from the installed folder: it never replaces the files it
  runs, and the new version installs with its own updater. Kazo Bar copies its whole folder elsewhere for the
  same reason; here, the unpacking done by the application gives that folder for free.
- **No window for the updater**: the download, the only long step, shows in Jarvis's window; replacing the
  files takes a few seconds. A message box only on failure.
- **A development build never installs** (run from `bin\Debug` or `bin\Release`): the window says so and
  nothing is downloaded.
- **Named failure**: GitHub unreachable (no network, 60 requests an hour without a token), damaged file,
  updater that does not start. A failed automatic check is only logged.

## Publishing a version

On Windows (or Linux with PowerShell 7):

```powershell
./build/Publish-Release.ps1 -Version 0.2.0
gh release create v0.2.0 artifacts/Kazo.Jarvis-0.2.0-win-x64.zip --prerelease --title "Kazo.Jarvis 0.2.0"
```

The script publishes the application and the updater, self-contained, `win-x64`, into one folder, then
zips it. The tag is `v<version>` and the package name does not change: the check reads both.

## Windows check

What the Web host does not prove (stage 1 plan, work item 16): an end-to-end update between two test
versions.

1. Publish two test versions to local folders: `./build/Publish-Release.ps1 -Version 0.0.2-test.1 -Output
   C:\jarvis\temp\release-1`, then the same with `0.0.2-test.2` into `release-2`.
2. Unpack the first one into `C:\jarvis\temp\jarvis-installed` and start it.
3. Publish the second one on GitHub as a pre-release (`gh release create v0.0.2-test.2 … --prerelease`), or
   serve it from a fake server.
4. Updates… → Check → Install. Expected: Jarvis closes, comes back as `0.0.2-test.2` within 30 seconds,
   settings intact, `updater.log` without errors.
5. Start again: the `.kazo-update-backup-*` copy and `updates\` are gone.
6. Delete the test pre-release on GitHub.
