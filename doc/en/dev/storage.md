# Settings, storage and vault

2026-11-19 · Work item `stage1/3-settings-storage-vault`. Built from [ADR 0004](adr/0004-configuration-storage-and-secret-vault.md)
(locations, configuration, storage, vault) and [ADR 0002](adr/0002-vertical-slices-kernel-and-dependency-rules.md)
(contracts in the kernel, implementations in `Adapters`).

This document says how a slice uses the settings, the database and the vault, and records the detail
choices ADR 0004 does not make. The rules themselves are in the ADR.

## Contents

```
src/Kazo.Jarvis.Kernel/
  Settings/        SettingsSection<T>, ISettingsStore, load state, write result, migrations
  Storage/         IDocumentStore, IDocumentCollection<T>, DocumentId, StoredDocument<T>
  Secrets/         ISecretVault, ISecretVaultSelector, SecretRef, SecretValue, VaultSettings
src/Kazo.Jarvis.Adapters/
  Files/           JarvisPaths (folders of ADR 0004), atomic writing, backup copies
  Settings/        JsonFileSettingsStore (config.json)
  Storage/         SqliteDocumentStore (data\jarvis.db)
  Secrets/         CredentialManagerVault, EncryptedFileVault + DpapiSecretProtector, SelectedSecretVault
src/Kazo.Jarvis.Web/Simulated/
                   InMemorySettingsStore, InMemoryDocumentStore, InMemorySecretVault
```

The Windows host creates `%USERPROFILE%\.kazo\jarvis` and its folders at start-up, then calls
`AddJarvisAdapters(paths)`. The Web host calls `AddJarvisSimulatedServices()`. Tests give `JarvisPaths` a
temporary folder.

## Settings

A slice declares its section once, with its default values and its validation:

```csharp
public sealed record LanguageOptions
{
    public static SettingsSection<LanguageOptions> Section { get; } = new(
        "Settings.Language",
        () => new LanguageOptions(),
        value => value.Code is "fr" or "en" ? [] : ["The language is fr or en."]);

    public string Code { get; init; } = "fr";
}
```

- `settings.Read(LanguageOptions.Section)` returns the saved value, or the default values when the section is
  missing, cannot be read or is not valid (the problem is logged, without the value). Each call returns a new
  instance.
- `await settings.WriteAsync(section, value)` validates, writes the file atomically, then notifies the
  watchers. The result says `Saved`, `Invalid` (with the problems) or `ReadOnly`.
- `settings.Watch(section, handler)`: the setting applies without a restart. Dispose the subscription in
  `Dispose`.
- `settings.State` says what happened on load: `Loaded`, `Missing` (first run, the first-run wizard will
  start, US1.4), `Migrated`, `Unreadable`, `NewerVersion` or `MigrationFailed`. In the last three cases
  `CanSave` is false: the file is never rewritten and the application runs on default values. Work item 15
  will show the fault (`FaultCause.ConfigurationUnreadable`).

### Format of `config.json`

```json
{
  "schemaVersion": 1,
  "Secrets.Vault": { "kind": "CredentialManager" },
  "Settings.Language": { "code": "fr" }
}
```

- Sections sit at the top level, next to `schemaVersion`, named `<Area>.<Slice>` (letters and digits, a
  capital at the start of each part). The file stays readable and editable by hand.
- camelCase properties, enums as text, indented. On read, property case is ignored, comments and trailing
  commas are tolerated.
- An unknown section is kept as it is on save.
- Atomic writing: temporary file in the same folder, flushed to disk, then moved over `config.json`.
- Copies in `backups\`: `config.v<version>.<yyyyMMdd-HHmmss>.json` before a migration,
  `config.unreadable.<…>.json` for an unreadable file.
- A hand edit of the file while Jarvis runs is not read again: it is overwritten by the next save of a
  section. Close Jarvis before editing the file.

### Migrations

`SettingsSchema.CurrentVersion` is 1. To change the format of a section: raise that constant, and register in
the container an `ISettingsMigration` whose `FromVersion` is the old version. The migration receives the whole
file (`JsonObject`) and must keep the sections it does not know. A missing or failing migration leaves the
file untouched (`MigrationFailed`).

## Document store

```csharp
var turns = store.Collection<Turn>("Conversation.Turns");
var added = await turns.AddAsync(turn);                       // time: now
var today = await turns.ReadPeriodAsync(from, to);            // from included, to excluded, oldest first
await turns.DeleteAsync(added.Id);
await turns.ClearAsync();
await store.EraseAllAsync();                                  // conversation, memory and log; not settings or secrets
```

- A collection is named `<Area>.<Name>`. A slice only opens its own collections.
- Documents are stored as compact JSON. Their time is kept to the millisecond: the document returned by
  `AddAsync` already carries the rounded time, the one a read will return.
- Identifiers: version 7 GUIDs; those of documents added later sort after.

### SQLite details

- A single table, `documents` (`seq`, `id`, `collection`, `at` in UTC Unix milliseconds, `content`), and an
  index on `(collection, at, seq)`.
- Schema version: `PRAGMA user_version`. The steps are in `SqliteDocumentStore.SchemaSteps`; a published step
  is never edited, a new one is added. Before a migration the database is copied to
  `backups\jarvis.v<version>.<…>.db` (SQLite backup, which takes the WAL journal into account).
- A database written by a newer version, or whose migration failed, is not touched: every operation throws
  `StorageUnavailableException`.
- WAL journal; `secure_delete` is on, and `EraseAllAsync` also runs `VACUUM` and empties the WAL journal:
  erased content does not stay readable in the files (rule 9, checked by a test).
- No connection pooling: the file is released after each operation and can be copied or deleted at any time.
- Package: `Microsoft.Data.Sqlite` (with its native SQLite library), in `Adapters` only.

## Vault

```csharp
var key = new SecretRef("llm.anthropic.apikey");      // this is what the settings keep
await vault.SetAsync(key, new SecretValue(typed));
var secret = await vault.GetAsync(key);                // null when there is none
client.ApiKey = secret?.Reveal();
```

- `SecretValue.ToString()` returns `(secret)`: a secret slipped by mistake into a log or an error message stays
  hidden. Only `Reveal()` gives the value, to the service that needs it.
- The messages of `SecretVaultException` name the reference, never the value.
- `ISecretVault` always works on the chosen vault. The choice is the `Secrets.Vault` section (`VaultSettings`,
  in the kernel because the adapter reads it). Only `ISecretVaultSelector.SwitchToAsync` changes it.
- **Switching vaults:** each secret is copied to the new vault, the choice is saved, then the old vault is
  emptied. If a copy or the save fails, the copies made are removed (except a secret that already existed in
  the target vault) and nothing changes. A secret that cannot be deleted from the old vault afterwards is
  logged (its reference only).
- **Windows Credential Manager** (default): one generic credential per secret, named
  `Kazo.Jarvis/<reference>`, kept on this machine for this user (`CRED_PERSIST_LOCAL_MACHINE`), value in
  UTF-8, **2,560 bytes at most** (Windows limit; beyond it, the error advises the encrypted file).
- **Encrypted file** `secrets.json`: `{ "schemaVersion": 1, "secrets": { "<reference>": "<base64>" } }`. Each
  value is encrypted on its own with DPAPI for the current Windows user, with an entropy specific to Jarvis.
  An unreadable or newer file is never rewritten: the operation fails and says so. A value that cannot be
  decrypted (other user, other machine) gives an error without details.
- Package: `System.Security.Cryptography.ProtectedData`, in `Adapters` only. Both vaults are only registered
  on Windows.

## Web host

The simulated services keep the contracts of the real ones: settings stored as JSON (a first run at each start
of the host), documents as JSON to the millisecond, an in-memory vault where switching only changes
`Current`. Their tests are in `tests/Kazo.Jarvis.Tests.E2E/Simulated/`, because the unit test project does not
reference the host.

## Tests

| Subject | Tests | Where they run |
| --- | --- | --- |
| Sections, references, collection names | `Kernel/Settings`, `Kernel/Secrets`, `Kernel/Storage` | everywhere |
| `config.json`: first run, unknown section kept, unreadable or newer file never rewritten, migration with copy, atomic write, watchers | `Adapters/Settings/JsonFileSettingsStoreTests` | everywhere |
| SQLite: period, deletion, erasing everything without a trace in the file, newer database, migration with copy, failed migration | `Adapters/Storage/SqliteDocumentStoreTests` | everywhere |
| Encrypted file (with a simulated encryption), switching vaults | `Adapters/Secrets/EncryptedFileVaultTests`, `SelectedSecretVaultTests` | everywhere |
| Credential Manager, DPAPI, registration of the vaults | `CredentialManagerVaultTests`, `With_dpapi_…`, `On_windows_…` | **Windows** (Benoit's PC) |
| Simulated services of the Web host | `Tests.E2E/Simulated/SimulatedServicesTests` | everywhere |

Windows tests carry `[Category("Windows")]` and `[SupportedOSPlatform("windows")]`: NUnit skips them
elsewhere. The Credential Manager tests use a prefix of their own, deleted after each test.
