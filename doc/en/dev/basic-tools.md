# Basic tools: files, web search, reading a page, "Tools" page

Work item `stage2/5-basic-tools` (US2.1, continued). Decisions:
[ADR 0017](adr/0017-tools-classes-and-decision-point.md) (sections 3 and 6) and
[ADR 0022](adr/0022-in-app-browser-for-web-search.md). The tool loop and the decision point are described in
[tools.md](tools.md).

## What is built

```
Tools/Files ─► list_directory, read_file ─► ILocalFiles ─► Adapters/Files/LocalFiles (disk)
Tools/Web   ─► web_search ─► IWebSearchApi (Brave, if a key is in the vault) ─► Adapters/Web/BraveSearchApi
            │            └► IReadingBrowser (browser of Jarvis) ─► Windows host: WebViewReadingBrowser
            └► fetch_url ─► IReadingBrowser
            └► IWebAddressPolicy (local addresses refused) ◄─ also asked by the browser
Tools/Catalog ─► "Tools" page (one switch per tool)
```

A slice touches neither the disk nor the network: it goes through a kernel contract, implemented in
`Adapters` or in a host.

## Tools

| Tool | Slice | Enabled by default | Class | What it returns |
| --- | --- | --- | --- | --- |
| `list_directory` | `Tools/Files` | yes | Read; Forbidden outside the allowed folders | Without `path`: the allowed folders. With `path`: the entries of the folder (name, kind, size) |
| `read_file` | `Tools/Files` | yes | Read; Forbidden outside the allowed folders | The text of the file from `start`, and `next_start` when some is left |
| `web_search` | `Tools/Web` | **no** | Read | The pages found: title, address, snippet |
| `fetch_url` | `Tools/Web` | **no** | Read; Forbidden for a local address | The title and the text of the page |

Each result is cut to a size that can be set, from 200 to 2,000 characters (2,000 by default). A result
returns to the model as data.

## Files (`Tools/Files`)

- Settings section `Tools.Files`: `Folders` (50 at most), `ResultCharacters`. **No folder is allowed at the
  start**: both tools are offered to the model, but every path is refused until the user adds one.
- `AllowedFolders.Check` resolves the path through `ILocalFiles.Resolve` (symbolic links and junctions
  followed) before it compares it with the allowed folders, resolved too. A link that leaves an allowed
  folder is therefore refused. `C:\Notes` does not hold `C:\NotesPrivate`.
- Verdicts: allowed, unusable (empty or relative path), outside, secret. "Secret" covers the files that
  ADR 0021 protects: `.claude\sessions\*.key` and `.claude\ide\*.lock`, even in an allowed folder.
- A path that is outside or secret gives the "Forbidden" class: the decision point refuses and writes it to
  the permission log.
- `LocalFiles` (adapter) reads 5 MB at most per file, recognizes UTF-8 and UTF-16 by their mark, and refuses
  a binary file.
- "Allowed folders" page: list, add a typed path, remove, size of a result. A folder that no longer exists
  is marked.

## Web (`Tools/Web`)

- Settings section `Tools.Web`: `Engine` (`DuckDuckGo` by default, `Startpage`, `Bing`), `Results` (1 to
  10, 5 by default), `ResultCharacters`, `LocalExceptions` (20 at most).
- `web_search` goes through Brave Search when the key `tools.web.brave.apikey` is in the vault, otherwise
  through the browser of Jarvis. A rejected key is told to the model; Jarvis does not silently fall back
  on the browser.
- `fetch_url` reads a page through the browser of Jarvis. Only `http` and `https` are accepted.
- `WebAddressPolicy` refuses the addresses of the machine and of the local network (loopback, private
  ranges, link-local, `localhost`, names without a dot), except the hosts listed in `LocalExceptions`.
- An engine that asks for a human check gives "blocked": the model says so, Jarvis works around nothing.
- "Web search" page: what leaves the machine, engine, Brave key (secret field), number of results, size,
  local exceptions, "Open the browser of Jarvis" and "Erase the data of the browser".

## Browser of Jarvis (Windows host)

`src/Kazo.Jarvis.Windows/Browser/` implements `IReadingBrowser` as ADR 0022 says:

- `ReadingBrowserWindow`: a WPF window never shown during a search (no activation, not in the taskbar),
  with the WebView2 controller kept visible so that the page is drawn. "See the page" shows it with an
  address bar, Back, Reload and a warning. Closing it hides it.
- `WebViewReadingBrowser`: its own data folder (`%USERPROFILE%\.kazo\jarvis\browser`), no download, no new
  window, no permission granted. Every navigation and every resource goes through `IWebAddressPolicy`,
  after DNS resolution. The browser closes after 5 minutes without use. The technical log never writes the
  words of a search.
- `SearchPages`: the search addresses of the three engines and the scripts that read the results and the
  text of a page. Pure code, tested on Linux.

In the conversation window, a tool call whose result holds addresses shows one "See the page" button per
address.

## "Tools" page (`Tools/Catalog`)

One switch per known tool, with its class, its origin and, where it applies, what leaves the machine
(`ITool.Discloses`). The page estimates the weight of the enabled tools in the context of the model
(characters divided by 4) and compares it with the context size of the default provider.

## Kernel contracts added

| Type | Role |
| --- | --- |
| `ILocalFiles` (`Kernel/Files`) | Resolve a path, list a folder, read an excerpt of text |
| `IReadingBrowser` (`Kernel/Web`) | Search, read a page, show the window, erase the data |
| `IWebSearchApi` | A search through an API with a key |
| `IWebAddressPolicy` | Is this address allowed? Before and after DNS resolution |
| `ITool.Discloses` | What leaves the machine, in one sentence; `null` by default |

## Web host

`SimulatedLocalFiles` (folder `/home/demo/Documents`, link `lien-prive` that leaves it) and
`SimulatedReadingBrowser` (`https://pages.jarvis.example/meteo`, `/crepes`, `/long`; a search that holds
"captcha" is blocked). The simulated model asks for a tool from the words: "liste le dossier …", "lis le
fichier …", "cherche sur le web …", "lis la page …". The `#browser` element of the home page shows what the
simulated browser received.

## Provisional choices of this work item

The ADRs do not settle them; to be confirmed by Benoit:

1. **A folder is added by typing its path**: no Windows folder picker yet.
2. **No "stricter" setting per tool** (ask for a confirmation for a read tool): it exists
   for the tools of the MCP servers ([mcp.md](mcp.md)), not yet for the built-in tools.
3. **The browser closes after 5 minutes without use**, a fixed value.
4. **A search by Jarvis replaces the page the user is looking at** in the browser window: there is only one
   tab.
5. **"See the page" lists the addresses found in the result of a tool**, 10 at most.
6. **The `.key` and `.lock` files of Claude Code** are refused even in an allowed folder.
7. **The token estimate** is rough: characters divided by 4.

## Known limits

- Between the DNS check and the connection, a name can change address ("DNS rebinding"): WebView2 does not
  allow pinning the address.
- The reading scripts depend on the markup of the engines: a change on their side gives "nothing read", not
  a false result.

## Not verified

- **Nothing has run on Windows.** The WebView2 browser compiles on Linux; its scripts are tried in Chromium
  against hand-written pages, never against the real engines from Jarvis (spike E15 proved them separately).
- The Brave Search API: tested against hand-written responses.
- NTFS junctions: `LocalFiles` is tested with symbolic links on Linux.

## Tests

- Unit: `FileToolsTests`, `WebToolsTests`, `ToolCatalogTests`, `LocalFilesTests`, `BraveSearchApiTests`,
  `SearchPagesTests`.
- Playwright: `BasicToolsTests` ("Tools" page, refusal without an allowed folder and log, folder listed and
  read, link that leaves it refused, search offered only once turned on, page read, local address refused,
  blocked search, "Web search" page) and `SearchPageScriptsTests` (the scripts of the three engines and the
  one for the text of a page).

## Windows checks

To do with Benoit:

1. Turn on `web_search`, ask "cherche sur le web …": no window appears, the foreground does not change, the
   results come from DuckDuckGo. Do it again with Startpage and Bing.
2. "See the page": the window opens on the page, the warning is visible; close it then run a search again.
3. `fetch_url` on a real page, then on `http://localhost:11435` and on the address of the router: refused.
4. "Erase the data of the browser": the `browser` folder is emptied, a search still works.
5. After 5 minutes without a search: no `msedgewebview2` process of the browser of Jarvis is left.
6. Allowed folders: a real folder, an NTFS junction that leaves it (refused), a UTF-16 file, a large file
   read in pieces.
7. With `qwen3:14b`: the model picks the right tool and does not follow an instruction written in a page.
8. With a Brave key: a search goes through the API; with a wrong key, the cause is told.
