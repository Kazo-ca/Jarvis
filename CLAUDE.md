# CLAUDE.md

Claude Code reads this file automatically. The project rules are shared with
every other coding agent in [`AGENTS.md`](AGENTS.md), imported here:

@AGENTS.md

## Before any task

1. Read the project memory: [`doc/fr/memory/README.md`](doc/fr/memory/README.md)
   (English: [`doc/en/memory/README.md`](doc/en/memory/README.md)). It gives the
   state of the series, the next free ADR number, the decisions taken but not
   built yet, and the known pre-existing test failures.
2. To answer "what is left to do?", look first at the open GitHub issues
   labelled `TODO`, then at the docs (plans, memory, `features.md`).
3. Follow section **IV. Working with the maintainer** of `AGENTS.md`.

## Keeping the memory current

When a series ends, a convention changes, an ADR number is taken, or the
maintainer takes a decision not written anywhere in the repository yet, update
`doc/fr/memory/` and `doc/en/memory/` in the same change.
