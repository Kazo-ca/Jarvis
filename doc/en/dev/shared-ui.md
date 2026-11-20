# Shared UI and language

Work item `stage1/5-shared-ui-language` of the [stage 1 plan](plans/stage-1.md). Decisions:
[ADR 0008](adr/0008-languages-and-localization.md) (languages) and [ADR 0011](adr/0011-orb-colors-and-drawing.md),
section 2 (colors and fonts).

## Shared style

- `Kazo.Jarvis.Features/wwwroot/css/jarvis.css`, served at `_content/Kazo.Jarvis.Features/css/jarvis.css` and linked
  by every host page (`index.html` and `orb.html` of the Windows host, `App.razor` of the Web host).
- It defines once the colors of ADR 0011 and of the catalog as CSS variables (`--jarvis-cyan`,
  `--jarvis-violet`, `--jarvis-green`, `--jarvis-red`, `--jarvis-grey`, `--jarvis-amber`, `--jarvis-text`,
  `--jarvis-text-muted`, `--jarvis-background`, lines and panels) and the three font families
  (`--jarvis-font-display`, `--jarvis-font-body`, `--jarvis-font-mono`). Components write no color value: they
  use these variables in their `.razor.css`.
- **Embedded fonts** in `wwwroot/fonts/`, never downloaded at run time: Rajdhani 500 and 600 (titles), Inter
  400 and 600 (text), JetBrains Mono 400 and 500 (labels, figures). `woff2` files of the Latin subset (it holds
  the French accents), taken from the `@fontsource` 5.3.0 packages. **License checked:** all three are under
  the SIL Open Font License 1.1, which allows shipping them with an Apache 2.0 application; each license text
  sits next to the files (`OFL-*.txt`).

## Translations

- `.resx` resources next to the component or class that uses them: `Name.resx` in English (neutral language,
  `<NeutralLanguage>en</NeutralLanguage>`), `Name.fr.resx` in French. The manifest name is the full name of the
  type, so the folder follows the namespace.
- Components inject `IStringLocalizer<T>`. `AddJarvisFeatures` replaces it with `LanguageStringLocalizer<T>`
  (`Features/Shared/Localization`), which reads **the assistant language** at each lookup, never the thread
  culture. Dates and numbers therefore keep the Windows regional settings (ADR 0008, section 1), and a
  language change applies to every window at the next render.
- Text missing in French: the English text shows and a `Debug` log line says so. Key missing everywhere: the
  key name shows, with a warning.
- Texts shared by three slices or more are in `Features/Shared/SharedTexts.resx`.
- The Windows host texts (tray menu, error messages) are in `Kazo.Jarvis.Windows/Desktop/HostTexts.resx`. Those
  shown before the services exist (WebView2 missing) follow the Windows language, the others the assistant
  language; the menu reads its texts again each time it opens.
- Hosts publish only `en` and `fr` (`SatelliteResourceLanguages`).
- **Key test** (`Architecture/ResourceKeysTests`): each `.resx` under `src/` has its `.fr.resx` with the same
  keys, and the reverse, with no empty text. It is part of the merge bar.

## Language setting (slice `Settings/Language`)

- Section `Settings.Language` of `config.json`: `{ "code": "fr" }` or `"en"`. Absent or `null`: the language
  proposed from Windows (French when the Windows display is in French, whatever the region; English
  otherwise). Any other code is refused by the validation.
- `LanguageKeeper` provides `IAssistantLanguageReader` (kernel) and publishes `AssistantLanguageChanged` after
  each change, once `Current` is up to date. The other slices (transcription, voice, conversation) will read
  the language through this contract.
- The host provides `ISystemLanguage`: the Windows display language read at start-up (Windows host), or
  `Simulated:SystemLanguage` from the configuration, English by default (Web host).
- **Reload without restart:** each window is wrapped in `LanguageScope` (`Features/Shared/Localization`). When
  the language changes, its content is built again under a new key, so every text is read again. What was not
  saved (a half-typed field) is lost, as when the window is reopened. Proven in the Web host by Playwright;
  **to check on Windows** in `BlazorWebView` (see [hosts](hosts.md#windows-checks)). If it is not reliable,
  a new ADR will say that the interface changes at the next start (ADR 0008).
- The title bar of a window already open keeps the language it was opened in (the title is set by
  `DialogRequest`); its content changes.

## Settings window (slice `Settings/Window`)

- It handles `SettingsWindowRequested`: the "Settings" entry of the tray menu is no longer greyed out. One
  window only (key `Settings.Window`).
- Pages come from the slices, through a kernel `SettingsPage` registration (key, component, title resource
  and key, order). The window knows no slice. Language is the first page (order 0).
- The selected page is kept by `SettingsWindowState`, so that a language change leaves the user on the same
  page.
- In the Web host, the "Open the settings" button publishes the same event as the menu.

## Shared components (`Features/Shared/`)

| Component | Role |
| --- | --- |
| `SettingsFrame` | Frame of a settings page: Rajdhani title, description, fields |
| `SettingsField` | One field: label, control, help sentence; a `fieldset`, readable by a screen reader |
| `SecretField` | Types a key or a token into the vault (ADR 0004, section 4) |
| `LanguageScope` | Builds the content of a window again when the language changes |

The secret field never reads the stored value back: it only learns whether a key exists, from the list of
vault references. It trims the spaces around a pasted key, empties its input as soon as the key is in the
vault, and says "vault unavailable" when the vault refuses. Its logic is in `SecretEntry`, tested without a
browser. It will serve the providers (work item 11), the cloud voices (work item 12) and later the connectors.
