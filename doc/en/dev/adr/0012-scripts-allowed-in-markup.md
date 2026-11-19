# ADR 0012: script is allowed in markup

## Status

Accepted by Benoit on 2026-11-19 (answer to the question raised in the PR of work item `stage1/1-solution`).

- **Partly supersedes:** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md), section 7, for
  script only. The style rule does not change.
- **Changes:** section II of `AGENTS.md` (rules 1 and 2).

## Context

- ADR 0002 (section 7) fails the tests when a `.razor` file holds `<style>`, `<script>` or `style="…"`. The
  rule came from Kazo Os (its ADR 0007).
- Work item `stage1/1-solution` showed that the Web host cannot do without it: its document (`App.razor`)
  must load `blazor.web.js`. An exception limited to that file had been written into the test.
- Benoit's answer of 2026-11-19: "Script can be used without problems. That restriction was for Kazo Os.
  Script can be used to manipulate a page's DOM without restrictions."

## Decision

1. **Script is allowed in Razor markup**, as a `<script>` tag as well as a file loaded with `src`, and to
   manipulate the DOM without restriction.
2. **Style stays forbidden in markup:** no `<style>`, no `style="…"`. A component's CSS goes in
   `MyComponent.razor.css`, next to it.
3. The architecture test `RazorMarkupTests` only checks style now. The list of host documents allowed to
   load a script (`RazorMarkupRule.HostDocuments`) goes away.
4. Putting a component's JavaScript in `MyComponent.razor.js` is still advised once it grows past a few
   lines: it is easier to read, cache and test. This is advice, not a checked rule.
5. "C# first" does not change: a feature's logic is written in C#. JavaScript serves the browser APIs C#
   cannot reach, DOM manipulation included.

## Consequences

- No exception to keep up to date for each host (Web today, Mobile at stage 4).
- A component may write a small script straight in its markup, for instance for the orb or animations,
  without a separate file.
- Nothing checks where JavaScript lives: code review takes care of it.
