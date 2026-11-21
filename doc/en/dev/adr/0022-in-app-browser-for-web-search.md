# ADR 0022: web search in a browser internal to Jarvis (partly supersedes 0017)

## Status

Proposed on 2026-11-20 (work item `stage2/in-app-browser-adr`). The open questions are in the "Questions for
Benoit" section.

- **Partly supersedes:** [ADR 0017](0017-tools-classes-and-decision-point.md), section 3: the "Driven
  browser" and "Spike E15" points, Benoit's answer 2 as it is read there, and the two consequences about the
  browser. The rest of ADR 0017 holds: two search providers behind a contract, Brave Search as soon as a key
  is supplied, `web_search` and `fetch_url` disabled by default, the refused addresses, read-only.
- **Builds on:** [ADR 0003](0003-hosts-and-native-services.md) (WebView2 is already required by the Windows
  host), [ADR 0009](0009-permission-and-confirmation-model.md) (data is never an instruction; what leaves the
  machine).

## Context

- Benoit asked, on 2026-11-20, whether the search could be done "in a browser accessible to the user". ADR
  0017 read it as the installed Edge or Chrome, driven by Playwright.
- Benoit corrected it the same day: "I meant a browser internal to the application. Not a native browser on
  the computer that can conflict with what the user is doing".
- The E15 spike started on Edge was stopped before it returned a result. Nothing is kept from it.
- The Windows host already shows its interface in WebView2 (`BlazorWebView`). The engine is therefore on the
  machine, with nothing to install.

## Decision

### 1. An internal browser, in a Jarvis window

- The default provider of `web_search`, and the means of `fetch_url`, is an **internal browser**: a WebView2
  control in a window of the Windows host, "Jarvis's browser".
- **Jarvis never starts or drives Edge, Chrome or any other browser of the user.** It opens no tab there,
  does not read their profile and does not depend on what they do there.
- The window never takes the focus when Jarvis uses it, and does not show in the taskbar until the user has
  opened it. When and how it shows: question 1.
- **The user can open it** to see the page Jarvis is reading, and browse there themselves: address, back,
  reload, close. Closing the window does not close the browser; it stops by itself after a time without use,
  adjustable.
- The browser is created on the first search, not when Jarvis starts.

### 2. Separate from Jarvis's interface and from the rest of the machine

- **A data folder of its own**, under Jarvis's data: neither Edge's, nor that of the WebView2 that shows the
  interface. A web page shares neither cookies nor processes with Jarvis's interface.
- **No bridge to the application:** no host object exposed to pages, messages from pages to the host are
  ignored. A page can call nothing in Jarvis.
- Jarvis drives it through the WebView2 API (navigation, end of loading, reading the text with a short
  script). **No Playwright, no open debugging port.**
- What the browser keeps from one time to the next (cookies, sign-ins made by the user): question 2.

### 3. Read-only, in code

- Jarvis only does two things there: open a search engine's results page for a query, and read the text of a
  page. It fills in no other form, clicks nothing other than links to read and signs in to nothing.
- Refused by the host, whatever the page asks: downloads, new windows (the link opens in the same view),
  camera, microphone, location and notification requests, launching another application from a link, saving
  passwords.
- **The addresses refused by ADR 0017 (machine and local network) are refused on every navigation, redirects
  and page resources included.**
- The text read is data, truncated like any tool result (ADR 0017, section 4). What the user does themselves
  in the window is not an action of the model and does not go through the decision point.

### 4. Search engine

- The default engine is chosen by spike E15 (section 6); it is adjustable.
- If the engine blocks (CAPTCHA, refusal page), the tool returns the cause to the model. Jarvis does not get
  around a block. The user can open the window and solve it themselves; the tools page also offers Brave
  Search.

### 5. Contract and hosts

- Kernel contract: "reading browser" (search, read a page, state). Implementation: Windows host.
- Web host: a simulated version, with fixed pages, for the product's Playwright tests.
- Mobile: not at this stage.

### 6. Spike E15, redefined

On Benoit's PC, before work item `stage2/5-basic-tools`, with a WebView2 in a WPF window like the product's:

- blocks (CAPTCHA, consent wall) on several engines, over about twenty queries each;
- delay of a cold and a warm search; memory taken by the open browser;
- whether rendering and reading work when the window is hidden;
- the window takes the focus neither when created nor when navigating;
- reading the text of a few result pages; which engine has the most stable page to read.

**If the spike fails, a new ADR says what to change**; Brave Search then remains the only provider.

## Questions for Benoit

Asked in the conversation on 2026-11-20.

1. **When does the browser window show?** Recommendation: it stays hidden during a search; the conversation
   shows the pages read with a "See the page" button that opens it.
2. **What does the browser keep from one time to the next?** Recommendation: its cookies (fewer consent walls
   and blocks); the user can sign in to a site there themselves, behind a warning; a button clears
   everything.

## Options set aside

- **Installed Edge or Chrome, driven by Playwright.** Set aside by Benoit: it conflicts with what the user is
  doing.
- **The user's usual profile.** No longer makes sense: the internal browser has its own data folder. What
  replaces it is question 2.
- **Reading pages with plain HTTP requests, without a browser.** Many pages and engines return nothing useful
  without script, and the user would not see what Jarvis reads.
- **Playwright attached to the WebView2 through a debugging port.** A local port through which any program on
  the machine could drive the browser.

## Consequences

- No extra dependency: no Playwright in the product, no browser to download.
- A second group of WebView2 processes runs when the browser is open. Its memory cost is not measured; spike
  E15 measures it, and stopping after a time without use bounds it.
- The search depends on an engine's page, which changes without notice and may block. Brave Search remains the
  second choice.
- An accepted part of ADR 0017 is superseded the same day: it rested on the agent's misreading of Benoit's
  request.
