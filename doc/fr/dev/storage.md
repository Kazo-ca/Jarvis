# Réglages, stockage et coffre

2026-11-19 · Chantier `stage1/3-settings-storage-vault`. Construit d'après l'[ADR 0004](adr/0004-configuration-storage-and-secret-vault.md)
(emplacements, configuration, stockage, coffre) et l'[ADR 0002](adr/0002-vertical-slices-kernel-and-dependency-rules.md)
(contrats dans le noyau, implémentations dans `Adapters`).

Ce document dit comment une tranche se sert des réglages, de la base et du coffre, et note les choix de
détail que l'ADR 0004 ne tranche pas. Les règles elles-mêmes sont dans l'ADR.

## Contenu

```
src/Kazo.Jarvis.Kernel/
  Settings/        SettingsSection<T>, ISettingsStore, état de chargement, résultat d'écriture, migrations
  Storage/         IDocumentStore, IDocumentCollection<T>, DocumentId, StoredDocument<T>
  Secrets/         ISecretVault, ISecretVaultSelector, SecretRef, SecretValue, VaultSettings
src/Kazo.Jarvis.Adapters/
  Files/           JarvisPaths (dossiers de l'ADR 0004), écriture atomique, copies de sauvegarde
  Settings/        JsonFileSettingsStore (config.json)
  Storage/         SqliteDocumentStore (data\jarvis.db)
  Secrets/         CredentialManagerVault, EncryptedFileVault + DpapiSecretProtector, SelectedSecretVault
src/Kazo.Jarvis.Web/Simulated/
                   InMemorySettingsStore, InMemoryDocumentStore, InMemorySecretVault
```

L'hôte Windows crée `%USERPROFILE%\.kazo\jarvis` et ses dossiers au démarrage, puis appelle
`AddJarvisAdapters(paths)`. L'hôte Web appelle `AddJarvisSimulatedServices()`. Les tests passent un dossier
temporaire à `JarvisPaths`.

## Réglages

Une tranche déclare sa section une fois, avec ses valeurs par défaut et sa validation :

```csharp
public sealed record LanguageOptions
{
    public static SettingsSection<LanguageOptions> Section { get; } = new(
        "Settings.Language",
        () => new LanguageOptions(),
        value => value.Code is "fr" or "en" ? [] : ["La langue est fr ou en."]);

    public string Code { get; init; } = "fr";
}
```

- `settings.Read(LanguageOptions.Section)` rend la valeur enregistrée, ou les valeurs par défaut si la
  section manque, ne se lit pas ou n'est pas valide (le problème est journalisé, sans la valeur). Chaque appel
  rend une nouvelle instance.
- `await settings.WriteAsync(section, value)` valide, écrit le fichier de façon atomique, puis prévient les
  abonnés. Le résultat dit `Saved`, `Invalid` (avec les problèmes) ou `ReadOnly`.
- `settings.Watch(section, handler)` : le réglage s'applique sans redémarrer. Libérer l'abonnement dans
  `Dispose`.
- `settings.State` dit ce qui s'est passé au chargement : `Loaded`, `Missing` (premier lancement, l'assistant
  de premier lancement démarrera, US1.4), `Migrated`, `Unreadable`, `NewerVersion` ou `MigrationFailed`.
  Dans les trois derniers cas, `CanSave` est faux : le fichier n'est jamais réécrit et l'application tourne
  sur les valeurs par défaut. Le chantier 15 affichera la panne (`FaultCause.ConfigurationUnreadable`).

### Format de `config.json`

```json
{
  "schemaVersion": 1,
  "Secrets.Vault": { "kind": "CredentialManager" },
  "Settings.Language": { "code": "fr" }
}
```

- Les sections sont au premier niveau, à côté de `schemaVersion`, nommées `<Domaine>.<Tranche>`
  (lettres et chiffres, une majuscule au début de chaque partie). Un fichier reste lisible et modifiable à la main.
- Propriétés en camelCase, énumérations en texte, indentation. À la lecture, la casse des propriétés est
  ignorée, les commentaires et les virgules finales sont tolérés.
- Une section inconnue est gardée telle quelle à l'enregistrement.
- Écriture atomique : fichier temporaire dans le même dossier, vidé sur disque, puis déplacé sur
  `config.json`.
- Copies dans `backups\` : `config.v<version>.<aaaaMMjj-HHmmss>.json` avant une migration,
  `config.unreadable.<…>.json` pour un fichier illisible.
- Une modification du fichier à la main pendant que Jarvis tourne n'est pas relue : elle sera écrasée au
  prochain enregistrement d'une section. Fermer Jarvis avant de modifier le fichier.

### Migrations

`SettingsSchema.CurrentVersion` vaut 1. Pour changer le format d'une section : monter cette constante, et
enregistrer dans le conteneur un `ISettingsMigration` dont `FromVersion` est l'ancienne version. La
migration reçoit tout le fichier (`JsonObject`) et doit garder les sections qu'elle ne connaît pas. Une
migration absente ou en échec laisse le fichier intact (`MigrationFailed`).

## Base de documents

```csharp
var turns = store.Collection<Turn>("Conversation.Turns");
var added = await turns.AddAsync(turn);                       // heure : maintenant
var today = await turns.ReadPeriodAsync(debut, fin);          // début inclus, fin exclue, du plus ancien au plus récent
await turns.DeleteAsync(added.Id);
await turns.ClearAsync();
await store.EraseAllAsync();                                  // conversation, mémoire et journal ; pas les réglages ni les secrets
```

- Une collection s'appelle `<Domaine>.<Nom>`. Une tranche n'ouvre que ses propres collections.
- Les documents sont enregistrés en JSON compact. Leur heure est gardée à la milliseconde : le document rendu
  par `AddAsync` porte déjà l'heure arrondie, celle qu'une lecture rendra.
- Identifiants : GUID version 7, ceux des documents ajoutés plus tard se trient après.

### Détails de la base SQLite

- Une seule table, `documents` (`seq`, `id`, `collection`, `at` en millisecondes Unix UTC, `content`), et un
  index sur `(collection, at, seq)`.
- Version du schéma : `PRAGMA user_version`. Les étapes sont dans `SqliteDocumentStore.SchemaSteps` ; une
  étape publiée ne se modifie plus, on en ajoute une. Avant une migration, la base est copiée dans
  `backups\jarvis.v<version>.<…>.db` (sauvegarde SQLite, qui tient compte du journal WAL).
- Une base écrite par une version plus récente, ou dont la migration a échoué, n'est pas touchée : chaque
  opération lève `StorageUnavailableException`.
- Journal WAL ; `secure_delete` est activé, et `EraseAllAsync` fait aussi `VACUUM` et vide le journal WAL :
  un contenu effacé ne reste pas lisible dans les fichiers (règle 9, vérifié par un test).
- Pas de réserve de connexions : le fichier est relâché après chaque opération, on peut le copier ou le
  supprimer à tout moment.
- Paquet : `Microsoft.Data.Sqlite` (avec sa bibliothèque SQLite native), dans `Adapters` seulement.

## Coffre

```csharp
var key = new SecretRef("llm.anthropic.apikey");      // c'est ce que les réglages gardent
await vault.SetAsync(key, new SecretValue(typed));
var secret = await vault.GetAsync(key);                // null s'il n'y en a pas
client.ApiKey = secret?.Reveal();
```

- `SecretValue.ToString()` rend `(secret)` : un secret glissé par erreur dans un journal ou un message
  d'erreur reste caché. Seul `Reveal()` donne la valeur, pour le service qui en a besoin.
- Les messages de `SecretVaultException` nomment la référence, jamais la valeur.
- `ISecretVault` travaille toujours sur le coffre choisi. Le choix est la section `Secrets.Vault`
  (`VaultSettings`, dans le noyau parce que l'adaptateur la lit). Seul `ISecretVaultSelector.SwitchToAsync`
  la change.
- **Changer de coffre** : copie de chaque secret vers le nouveau coffre, enregistrement du choix, puis
  suppression de l'ancien coffre. Si une copie ou l'enregistrement échoue, les copies faites sont retirées
  (sauf un secret qui existait déjà dans le coffre visé) et rien ne change. Un secret qui ne se supprime pas
  de l'ancien coffre après coup est journalisé (sa référence seulement).
- **Gestionnaire d'identification Windows** (par défaut) : une identification générique par secret, nommée
  `Kazo.Jarvis/<référence>`, conservée sur cette machine pour cet utilisateur (`CRED_PERSIST_LOCAL_MACHINE`),
  valeur en UTF-8, **2 560 octets au plus** (limite de Windows ; au-delà, l'erreur conseille le fichier
  chiffré).
- **Fichier chiffré** `secrets.json` : `{ "schemaVersion": 1, "secrets": { "<référence>": "<base64>" } }`.
  Chaque valeur est chiffrée seule par DPAPI pour l'utilisateur Windows courant, avec une entropie propre à
  Jarvis. Un fichier illisible ou plus récent n'est jamais réécrit : l'opération échoue et le dit. Une valeur
  qui ne se déchiffre pas (autre utilisateur, autre machine) donne une erreur sans détail.
- Paquet : `System.Security.Cryptography.ProtectedData`, dans `Adapters` seulement. Les deux coffres ne
  s'enregistrent que sous Windows.

## Hôte Web

Les services simulés gardent les contrats des vrais : réglages stockés en JSON (un premier lancement à chaque
démarrage de l'hôte), documents en JSON à la milliseconde, coffre en mémoire où changer de coffre ne change
que `Current`. Leurs tests sont dans `tests/Kazo.Jarvis.Tests.E2E/Simulated/`, parce que le projet de tests
unitaires ne référence pas l'hôte.

## Tests

| Sujet | Tests | Où ils tournent |
| --- | --- | --- |
| Sections, références, noms de collection | `Kernel/Settings`, `Kernel/Secrets`, `Kernel/Storage` | partout |
| `config.json` : premier lancement, section inconnue gardée, fichier illisible ou plus récent jamais réécrit, migration avec copie, écriture atomique, abonnés | `Adapters/Settings/JsonFileSettingsStoreTests` | partout |
| SQLite : période, suppression, effacement complet sans trace dans le fichier, base plus récente, migration avec copie, migration en échec | `Adapters/Storage/SqliteDocumentStoreTests` | partout |
| Fichier chiffré (avec un chiffrement simulé), changement de coffre | `Adapters/Secrets/EncryptedFileVaultTests`, `SelectedSecretVaultTests` | partout |
| Gestionnaire d'identification, DPAPI, enregistrement des coffres | `CredentialManagerVaultTests`, `With_dpapi_…`, `On_windows_…` | **Windows** (PC de Benoit) |
| Services simulés de l'hôte Web | `Tests.E2E/Simulated/SimulatedServicesTests` | partout |

Les tests Windows portent `[Category("Windows")]` et `[SupportedOSPlatform("windows")]` : NUnit les ignore
ailleurs. Ceux du gestionnaire d'identification utilisent un préfixe à eux, supprimé après chaque test.
