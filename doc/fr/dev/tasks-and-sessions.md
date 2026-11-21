# Tâches et sessions Claude Code : forme des tâches, tableau, liste des sessions

Chantier `stage2/11-tasks-and-sessions`, **première partie** (US2.11, US2.14). Décisions :
[ADR 0013](adr/0013-relais-readiness.md), section 6, et [ADR 0021](adr/0021-claude-code-sessions.md),
sections 6, 7 et 10. La seconde partie du chantier, le serveur local des routes `/hook/…` et l'installation
des hooks chez l'utilisateur, est dans [hook-server.md](hook-server.md).

## Ce qui est construit

```
%USERPROFILE%\.claude\sessions\<pid>.json ─► Adapters/ClaudeCode/ClaudeSessionRegistry
   (lecture seule, jamais *.key ni ide/*.lock)        │
                                                      ▼
                                   IClaudeSessionRegistry (noyau)
                                                      │
ClaudeCode/Sessions ─► ClaudeSessionList : lecture toutes les 5 s, regroupement par projet ─► page « Sessions Claude »
                                                      │
Tasks/Board ─► ITaskBoard (noyau) ─► collection Tasks.Board de jarvis.db
   la tâche qu'une session porte, quand Jarvis la suit
```

Les tranches `Tasks/Board` et `ClaudeCode/Sessions` ne se connaissent pas : `ITaskBoard` les relie.

## Forme d'une tâche (`Kernel/Tasks`)

Celle de Relais (ADR 0013, section 6). Le type s'appelle `WorkTask`, pour ne jamais être pris pour un `Task`
de .NET.

| Champ | Rôle |
| --- | --- |
| `Id` | Identifiant (`task-…`), donné à une session par `RELAIS_TASK_ID` |
| `ParentId` | Vide pour une tâche, rempli pour une sous-tâche. **Un seul niveau** |
| `Project`, `Topic` | Clé de projet et clé de sujet |
| `Title`, `Summary` | Titre ; ce que la tâche dit d'elle-même, ou la cause quand elle est bloquée |
| `Operator` | Compte (`claude-code`) et session |
| `Source` | `harness`, `hook`, `mcp`, `orchestrator` |
| `Status` | Un des neuf statuts |
| `CreatedAt`, `UpdatedAt` | Dates |
| `Folder` | Dossier du travail. **Ajout de Jarvis** à la forme, pour la liste des sessions |

**Les neuf statuts, et pas d'autres :** `pending`, `working`, `paused`, `idle`, `waiting_answer`,
`waiting_approval`, `blocked`, `done`, `abandoned`. `waiting_answer` et `waiting_approval` attendent l'humain
(ambre dans l'éventail) ; `done` et `abandoned` terminent la tâche.

### Clés (`TaskKeys`)

- **Projet :** l'adresse du remote Git, normalisée en `hôte/propriétaire/nom` en minuscules, quelle que soit
  sa forme (`https://github.com/Kazo-ca/Jarvis.git`, `git@github.com:Kazo-ca/Jarvis`). Un nom d'utilisateur ou
  un mot de passe dans l'adresse n'entre jamais dans la clé. Sans remote : le nom du dossier.
- **Sujet :** `branch:<nom>` ; `_inbox` pour `main`, `master`, une tête détachée ou un dossier sans Git. Les
  clés `pr:<n>` et `issue:<n>` ne sont pas trouvées à l'étape 2.

## Tableau des tâches (`Tasks/Board`, `ITaskBoard`)

- `CreateAsync` : une tâche naît `pending` (ADR 0021, section 5). La sous-tâche d'une sous-tâche se rattache
  à la tâche.
- `ChangeAsync` : statut, résumé, session de l'opérateur, titre ; ce qui n'est pas nommé reste. **Une tâche
  terminée ne bouge plus** : un hook en retard est ignoré. Un changement qui ne change rien n'est pas annoncé.
- `Find`, `FindBySession`, `Tasks` (la plus récemment changée d'abord), `RemoveEndedAsync` (les tâches
  terminées avant une date, avec leurs sous-tâches ; jamais une tâche en cours).
- Rangement : un document par tâche dans la collection `Tasks.Board` de `jarvis.db`, relu au démarrage. Un
  résumé est coupé à 2 000 caractères, un titre à 200.
- Les tâches en mémoire font foi tant que Jarvis tourne : « Tout effacer » vide la base, et une tâche encore
  en cours reste au tableau jusqu'à l'arrêt.

Les hooks créent et font bouger les tâches ([hook-server.md](hook-server.md)) ; le lancement d'une session
(chantier `stage2/12`) le fera aussi.

## Registre des sessions (`IClaudeSessionRegistry`, `Adapters/ClaudeCode`)

- Lit `%USERPROFILE%\.claude\sessions\<pid>.json` (essais E2 et E3). Une session de code de Claude Desktop
  est un processus Claude Code comme les autres (`entrypoint: claude-desktop`).
- **N'ouvre que les fichiers `.json` placés directement dans `sessions`.** Jamais `sessions/*.key`, jamais
  `ide/*.lock`, jamais rien d'autre du dossier `.claude` : un test passe chaque ouverture de fichier par un
  témoin et le prouve. **N'écrit rien.**
- Le format est interne à Claude Code : seuls `sessionId`, `pid` et `cwd` sont exigés ; tout autre champ peut
  manquer ; un état inconnu s'affiche « État inconnu » ; un fichier illisible ou à moitié écrit est sauté.
- Une session dont le processus est terminé n'est pas listée.
- Le remote `origin` et la branche viennent du dossier `.git` du dossier de travail ou d'un parent, lus
  directement (`HEAD`, `config`), sans lancer Git ; les arbres de travail (`gitdir:`, `commondir`) sont suivis.

## Liste des sessions (`ClaudeCode/Sessions`)

Section `ClaudeCode.Sessions` :

| Réglage | Défaut | Rôle |
| --- | --- | --- |
| `Enabled` | oui | Lire le registre. Non : Jarvis ne regarde pas le dossier de Claude Code |
| `PollSeconds` | 5 | Délai entre deux lectures, de 2 à 60 secondes |

- `ClaudeSessionList` lit à intervalle régulier, regroupe **par projet et non par chemin** (deux clones du
  même dépôt font un projet), met en premier les projets et les sessions qui attendent, puis trie par dernière
  activité. La page n'est prévenue que d'un changement.
- Une session que Jarvis suit comme tâche (`FindBySession`) porte cette tâche : son statut s'affiche, et une
  tâche `waiting_…` fait passer la session en attente même si le registre dit « au travail ».
- Pas de registre (Claude Code n'a jamais tourné) et registre illisible sont dits à part. **Jamais une panne
  de l'assistant.**
- **Jarvis ne fait que regarder** ces sessions : il ne répond jamais à leur place (US2.15).

### Page « Sessions Claude »

Après « Home Assistant ». L'interrupteur, ce que Jarvis fait et ne fait pas de ce dossier, la liste par projet
(nom, état et raison de l'attente, dossier, origine « Lancée à la main », « Claude Desktop » ou « Lancée par
Jarvis », sujet, dernière activité, statut de la tâche), le délai entre deux lectures.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `WorkTask`, `NewWorkTask`, `TaskOperator`, `WorkTaskStatus`, `TaskSource`, `TaskWire` (`Kernel/Tasks`) | La forme d'une tâche et les noms de la spec |
| `TaskKeys` | Clé de projet, clé de sujet |
| `ITaskBoard`, `TaskChange` | Créer, changer, trouver, lister les tâches |
| `IClaudeSessionRegistry`, `ClaudeSession`, `ClaudeSessionListing`, `ClaudeSessionState` (`Kernel/ClaudeCode`) | Les sessions en cours, service natif de l'hôte |

## Hôte Web

`SimulatedClaudeSessions` : trois sessions (deux clones de `Kazo-ca/bar` dans deux dossiers, dont une de
Claude Desktop, et un dossier sans Git). Le bouton « Make the Claude Code sessions move » de la page de test
fait avancer le scénario : une session attend, puis plus aucune session, puis plus de registre.

## Seconde partie du chantier

Faite sur la branche `stage2/11-hook-server` : le serveur local des routes `/hook/…`, avec jeton, et
l'installation des hooks chez l'utilisateur. Voir [hook-server.md](hook-server.md).

## Choix provisoires de ce chantier

L'ADR ne les tranche pas ; à confirmer par Benoit :

1. **La lecture du registre est activée au départ** : elle est locale, n'écrit rien et ne demande aucun
   réglage (recommandation de l'essai E2). Un interrupteur la coupe.
2. **Lecture toutes les 5 secondes**, réglable, plutôt qu'une surveillance du dossier : plus simple, et une
   lecture de 14 sessions a pris 73 ms.
3. **Une session sans tâche ne devient pas une tâche** : elle s'affiche avec l'état du registre (ADR 0021,
   section 6), et n'apparaîtra pas dans l'éventail tant que ses hooks ne sont pas installés.
4. **Le dernier message d'une session n'est pas lu** dans cette partie : la transcription sert au résumé du
   chantier `stage2/13`.
5. **Les tâches non terminées d'une exécution précédente restent telles quelles au démarrage.** Le chantier 12
   décidera de celles dont le processus n'existe plus.

## Non vérifié

- **Rien n'a tourné sur Windows** : le vrai registre n'a pas été lu par ce code (l'essai E2 l'a lu avec un
  autre code). L'état `waiting` n'a jamais été observé en direct, même à l'essai.
- Le test de vie d'un processus compare le seul numéro de processus, pas `procStart` : un numéro réutilisé par
  un autre programme ferait paraître vivante une session terminée dont le fichier serait resté.

## Tests

- Unitaires : `TaskKeysTests`, `TaskBoardTests`, `ClaudeSessionRegistryTests` (dont « les fichiers qui
  contiennent des secrets ne sont jamais ouverts »), `ClaudeSessionListTests`.
- Playwright : `ClaudeSessionsTests` (liste par projet ; une session qui se met à attendre ; interrupteur et
  délai).

## Vérifications sur Windows

À faire avec Benoit :

1. Ouvrir la page avec une session dans un terminal et une dans Claude Desktop : les deux apparaissent, sous
   le bon projet.
2. Deux clones du même dépôt : un seul projet.
3. Laisser une session demander une permission : la ligne passe à « Vous attend », avec la raison.
4. Fermer une session : elle disparaît en quelques secondes.
5. Couper l'interrupteur : la liste se vide.
