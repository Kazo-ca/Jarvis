# AGENT OPERATING GUIDELINES

**Project:** Kazo.Jarvis is a voice assistant for Windows written in C# on .NET 10,
with no Python at run time. It shows as an animated orb, answers first with a
local model, and hands off to Claude when a task is beyond it. An Android
satellite app (MAUI Blazor Hybrid) lets the user talk to it away from the PC.

The behavioral reference is OpenJarvis (fork `Kazo-ca/OpenJarvis`, read-only).
Never copy Python code: port the behavior.

**Before you start:** read the project memory
([`doc/fr/memory/README.md`](doc/fr/memory/README.md), English:
[`doc/en/memory/README.md`](doc/en/memory/README.md)) and the pitfalls
([`doc/en/dev/pitfalls.md`](doc/en/dev/pitfalls.md)). The design document is
[`doc/fr/dev/plans/design.md`](doc/fr/dev/plans/design.md)
(English: [`doc/en/dev/plans/design.md`](doc/en/dev/plans/design.md)).

---

## I. ARCHITECTURE

1. **Vertical Slice Architecture (VSA).** Code is organized by feature, not by
   technical layer. A slice is a folder under `Kazo.Jarvis.Features/` that holds
   everything one feature needs: its Razor components, its request/handler, its
   options and its tests (same path in the test project).
2. **Dependency rules** (ADR 0002):
   * A slice never references another slice. Slices talk through messages
     defined in the kernel.
   * A slice depends on the kernel and on contracts, never on a concrete adapter.
   * Prefer a little duplication between two slices over an early shared
     abstraction. What serves three slices or more moves to the kernel.
   * The kernel holds no business logic, no UI component and no native dependency.
3. **Hosts** (modeled on Kazo Bar): `Kazo.Jarvis.Windows` (WPF + BlazorWebView),
   `Kazo.Jarvis.Web` (test host with simulated services, Playwright target),
   `Kazo.Jarvis.Mobile` (MAUI Blazor Hybrid, Android). Hosts only hold start-up
   and platform code. Native services sit behind interfaces; real
   implementations live in `Kazo.Jarvis.Adapters`, loaded by the Windows host only.
   **Never reference Kazo Bar** (no project reference, package or submodule). Its code
   may be copied into this repository; the copy then belongs here and evolves
   separately (ADR 0014).
4. **Spikes.** Throwaway experiments live in `spikes/<name>/`, each with a
   `README.md` (how to run) and a `RESULTS.md` (what was proven, with evidence).
   Spike code is never referenced by product code.

## II. STYLE

1. **Collocated component styles.** The CSS of a component lives in
   `MyComponent.razor.css` next to it. No inline `style="..."` or `<style>` in
   markup. Script is allowed in markup (ADR 0012); move a component's JavaScript
   to `MyComponent.razor.js` once it grows past a few lines.
2. **C# first.** A feature's logic is written in C#. JavaScript for browser APIs
   C# cannot reach (DOM manipulation, canvas, WebGL, audio worklets).
3. **English file and folder names**, whatever the conversation language.
4. **Comments.** XML doc comments on public APIs; explain the *why* of
   non-obvious code.
5. **Secrets never in the repository.** API keys, OAuth tokens,
   device tokens go to the vault (Windows Credential Manager or an encrypted
   file). Local secrets and throwaway artifacts used while developing stay
   outside the repository.

## III. DOCUMENTATION

1. Docs live under `doc/` **in two mirrored languages**: French first in
   `doc/fr/`, English translation in `doc/en/`, same folders and file names.
   **Every doc change updates both languages in the same change.** Root readmes:
   `README.md` in English, `readme-fr.md` in French, each linking to the other.
2. Every folder under `doc/` has a `README.md` index; adding, renaming or
   removing a doc updates that index in the same change.
3. **ADRs** in `doc/<lang>/dev/adr/NNNN-title.md`, one per decision, written and
   merged **before** the code they describe. An accepted ADR is never edited:
   a new one supersedes it.
4. [`doc/fr/features.md`](doc/fr/features.md) is the product overview. Every PR
   to `main` that ships, changes or plans a feature updates it in both languages.
5. Documentation is part of "done": a change that makes an existing doc wrong
   updates that doc in the same change.

## IV. WORKING WITH THE MAINTAINER

1. **Language:** talk to the maintainer (Benoit) in **French**. Docs French first.
2. **Questions:** when a decision is open, ask it directly in the conversation,
   with 2 to 4 choices, their implications, and the recommended choice marked.
   A question left only inside a document does not count. "ok" or a blank
   answer takes the recommendation.
3. **"Add it to the to-do list"** means a GitHub issue labelled `TODO`, ending
   with a **"Dépendances"** section (blocked by / unblocks / related).
4. **Branches:** descriptive names only (`spikes/e4-voice-chain`,
   `orb/2-states`), never generic ones. A series uses `<series>/base` from
   `main`, one `<series>/<n>-<topic>` branch per work item, PRs to the base.
5. **Never merge into `main`.** Only the maintainer does, or an agent he
   explicitly asks for that PR. An agent may merge its own work-item PRs into
   a `<series>/base` once checks are green.
6. **ADR numbers:** take the next free number (check `doc/fr/dev/adr/` on
   `main` and open PRs), and record it in the memory README.
7. **Merge bar:** green local build and tests. Never skip, disable or
   quarantine a test to get green.
8. **Reporting:** lead with the result, name what was checked, say plainly
   what failed or was skipped.
