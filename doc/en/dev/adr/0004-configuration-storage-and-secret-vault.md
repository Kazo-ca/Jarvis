# ADR 0004: configuration, storage and secret vault

## Status

Proposed on 2026-11-18, waiting for Benoit's approval (work item `spikes/11-stage-1-adrs`).

- **Sources:** stories US1.8, US1.14 and US1.16 of the [design](../plans/design.md); security rules 2, 3
  and 9; assumptions 1 and 2; Kazo Bar's configuration, storage and vault.
- **Completes:** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (contracts are in the
  kernel, implementations in `Adapters`).

## Context

- Kazo Bar has a single configuration file per user (`config.json` with a `schemaVersion`), migrations with
  a backup copy, and a choice of vault: Windows Credential Manager or a DPAPI-encrypted file. The
  configuration only keeps a reference to each secret.
- Jarvis must keep the conversation (US1.14), then a memory (US2.4) and a log of runs (US3.14). Everything
  must be viewable and erasable (rule 9).
- With vertical slices, each slice owns its settings and its data.
- Voice models weigh from a few MB to several hundred MB. They do not belong in a roaming profile.

## Decision

### 1. Locations

```
%APPDATA%\Kazo\Jarvis\
  config.json          settings
  backups\             copies taken before a migration, or of an unreadable file
  data\jarvis.db       conversation, then memory and log
  secrets.json         only when the "encrypted file" vault is chosen

%LOCALAPPDATA%\Kazo\Jarvis\
  models\              downloaded models (wake word, recognition, voices)
  logs\                technical logs
```

- One user per installation: everything is kept in the Windows user's profile (assumption 1).
- Tests receive a temporary folder in place of these roots.

### 2. Configuration

- **A single file**, `config.json`, with a `schemaVersion`. It holds no secret.
- **One section per slice**, named `<Area>.<Slice>`. The slice declares its options class, its default
  values and its validation. A slice does not read another slice's section.
- The kernel gives the contract: read a section, change it, be notified of a change. A changed setting
  applies without restarting the application.
- An unknown section is preserved on save. A file written by a removed slice or by a newer version loses
  nothing.
- **Migrations** as in Kazo Bar: an older file is copied to `backups\`, migrated, then written back.
- **A file that cannot be read, or that was written by a newer version, is never overwritten.** The
  application starts on default values, saves nothing, and names the problem (US1.17).
- Writing is atomic: temporary file, then replace.
- A missing `config.json` starts the first-run setup wizard (US1.4).

### 3. Data storage

- Settings are JSON. **Data that grows lives in a SQLite database**, `data\jarvis.db`: the conversation from
  stage 1, then the memory and the log.
- Reason: add a turn, read a period again, erase one item or everything without rewriting a whole file, and
  without loss if the application stops in the middle of a write.
- The kernel gives a **document store by collection** contract: add, read by identifier, read a period,
  delete a document, empty a collection. It does not expose SQL.
- Each slice owns its collections and the format of its documents. A slice does not read another slice's
  collection: it asks through a message.
- The SQLite implementation is in `Adapters`. The Web host uses an in-memory store.
- The database schema has a version number. Before a migration, the file is copied to `backups\`.
- History is kept with no time limit (assumption 2). One button erases everything: conversation, memory and
  log. Settings and secrets are not touched by that button.
- The database is not encrypted. It is protected by the user's Windows profile, like Kazo Bar's data.

### 4. Secret vault

- Kernel contract, taken from Kazo Bar: create or replace, read, delete, list the references (never the
  values).
- **Two vaults, chosen by the user** in the settings: Windows Credential Manager (default) or a
  DPAPI-encrypted `secrets.json` file. Switching vaults moves the secrets.
- The configuration only keeps a **reference** (`secretRef`): 1 to 100 letters, digits, `.`, `-` or `_`.
- A secret that was typed is never shown again. It appears in no log and no error message.
- Stage 1 secrets: API keys of cloud models and cloud voices. Later come OAuth tokens, the Home Assistant
  token, and the tokens of paired devices and webhooks.
- The Web host uses a simulated in-memory vault.

### 5. Downloaded models

- Models go to `%LOCALAPPDATA%\Kazo\Jarvis\models`. They are neither in the repository nor in the install
  package. The list and the download are described in [ADR 0006](0006-voice-chain.md).

### 6. Logs and privacy

- Technical logs go to `%LOCALAPPDATA%\Kazo\Jarvis\logs`. They hold no audio and no secret. At the normal
  level, they do not hold the text of conversations.
- **The listening buffer stays in memory. No captured sound is written to disk** (rule 2).

## Consequences

- We take from Kazo Bar what has proven itself (single file, migrations, file never overwritten, choice of
  vault) without taking its code as is: the contracts are rewritten for the kernel.
- SQLite adds a native dependency, isolated in `Adapters`. The kernel and the slices do not see it.
- The document store is deliberately simple. If the stage 2 memory needs full-text search, a new ADR will
  add it.
- Models and logs do not follow a roaming profile; settings and data do.
- The choice of SQLite rather than JSON files like Kazo Bar is put to Benoit in the conversation. This ADR
  keeps the recommended option.
