# Serveur local et hooks Claude Code

Chantier `stage2/11-hook-server`, **seconde partie** du chantier 11 (US2.12). Décisions :
[ADR 0021](adr/0021-claude-code-sessions.md), sections 3, 5 et 6, et [ADR 0013](adr/0013-relais-readiness.md),
section 7. La première partie est dans [tasks-and-sessions.md](tasks-and-sessions.md).

## Ce qui est construit

```
session Claude Code ── hook ──► http://127.0.0.1:<port>/hook/…      (jeton obligatoire)
                                        │
            Adapters/LocalServer/KestrelLocalServer ── ILocalServer, ILocalRoute (noyau)
                                        │
            Tasks/Board ─► TaskHooks : cinq routes ─► ITaskBoard (crée et fait bouger les tâches)

ClaudeCode/Hooks ─► ClaudeHooks, HookEntries ─► IClaudeUserSettings (noyau)
   page « Hooks Claude Code »                    └► Adapters/ClaudeCode/ClaudeUserSettings
   aperçu, installer, retirer                        %USERPROFILE%\.claude\settings.json, copie datée
```

## Serveur local (`ILocalServer`, `Adapters/LocalServer`)

- Kestrel dans le processus de Jarvis, lié à **`127.0.0.1` et à rien d'autre**. Un test ouvre le port sur les
  autres adresses de la machine et constate qu'il est fermé. Ce n'est pas le serveur du mobile (ADR 0003).
- **Aucune route sans jeton**, en-tête `Authorization: Bearer <jeton>`. Sans jeton valide la réponse est
  `401`, quel que soit le chemin : un appelant n'apprend même pas quelles routes existent. La comparaison se
  fait en temps constant.
- Deux jetons :

  | Jeton | Où il vit | Ce qu'il ouvre |
  | --- | --- | --- |
  | Jeton de l'exécution (`ILocalServer.SessionToken`) | En mémoire, tiré à chaque démarrage (32 octets) | Toutes les routes. Pour les sessions lancées par Jarvis (chantier 12) |
  | Jeton des hooks de l'utilisateur | Dans le coffre, référence `ClaudeCode.UserHookToken` | Les routes `/hook/…` seulement ; ailleurs `403` |

- Seul `POST` est servi (`405` sinon), chemin inconnu `404`, corps de plus de 1 Mo `413`. Une route qui échoue
  rend `500` sans détail et n'arrête pas le serveur. La route ne voit jamais le jeton.
- Les routes viennent des tranches, comme services `ILocalRoute`. Le serveur ne connaît aucune tranche.
- Section `Local.Server` :

  | Réglage | Défaut | Rôle |
  | --- | --- | --- |
  | `Port` | 47821 | Port fixe, de 1024 à 65535 : les hooks installés le portent |

  Un changement de port prend effet tout de suite. **Un port déjà pris ne fait pas tomber Jarvis** : le
  serveur n'écoute pas, la page le dit et propose de changer de port.
- `Kazo.Jarvis.Adapters` référence maintenant `Microsoft.AspNetCore.App`. La publication est autonome
  (ADR 0001) : l'utilisateur n'installe rien de plus. `/mcp` et l'outil `approve` arrivent au chantier 12 sur
  ce même serveur.

## Routes des hooks (`Tasks/Board`, `TaskHooks`)

Les chemins sont dans le noyau (`HookPaths`) : la tranche qui les sert, celle qui les écrit chez
l'utilisateur et celle qui lancera les sessions doivent s'accorder.

| Hook de Claude Code | Route | Effet |
| --- | --- | --- |
| `SessionStart` | `/hook/task.context` | Lie la session à la tâche |
| `UserPromptSubmit` | `/hook/task.start` | `working` |
| `PermissionRequest` | `/hook/approval.requested` | `waiting_approval` |
| `SubagentStop` | `/hook/subtask.idle` | La sous-tâche de cet agent passe à `done`, avec son dernier message |
| `Stop` | `/hook/task.idle` | `idle`, avec le dernier message comme résumé |

- **Un hook ne décide rien et ne reçoit rien** : la réponse est toujours vide (`204`). Claude Code continue
  comme si personne n'écoutait.
- **Le corps est une donnée** : seuls `session_id`, `cwd`, `prompt`, `last_assistant_message`, `agent_id` et
  `agent_type` sont lus, coupés à longueur. Rien n'est exécuté ni donné à un modèle ici. Un corps qui n'est
  pas un objet JSON, ou sans session, rend `400` et ne change rien. Le texte d'une demande ou d'une réponse
  n'est jamais écrit au journal.
- **Session lancée par Jarvis :** elle nomme sa tâche par l'en-tête `X-Relais-Task`. La tâche existe avant la
  session ; une tâche inconnue rend `404` et n'est pas créée.
- **Session lancée à la main :** elle n'a pas de tâche. Son premier hook en crée une, de source `hook`, rangée
  par le remote et la branche que donne le registre des sessions (à défaut, par le dossier du hook), retrouvée
  ensuite par l'identifiant de session. Titre : la première ligne de la demande quand le premier hook en porte une ; sinon le nom de la
  session, puis celui du projet.
- **Le jeton des hooks de l'utilisateur ne peut pas nommer une tâche** : l'en-tête `X-Relais-Task` n'est
  écouté que pour le jeton de l'exécution.
- Un `UserPromptSubmit` dont le texte commence par `<task-notification>` n'est pas une demande humaine : la
  tâche repasse à `working`, mais ce texte ne devient jamais un titre.
- Les hooks sont traités un par un : les deux premiers hooks d'une session ne créent pas deux tâches.
- Une session dont la tâche est terminée et qui reprend reçoit une nouvelle tâche.

## Hooks chez l'utilisateur (`ClaudeCode/Hooks`)

Pour les sessions que l'utilisateur lance lui-même. **Jarvis n'écrit dans
`%USERPROFILE%\.claude\settings.json` que sur un clic**, depuis la page « Hooks Claude Code ».

- La page montre d'abord **les lignes exactes** qui seront ajoutées, jeton compris.
- « Installer » : copie datée du fichier à côté de lui (`settings.json.jarvis-AAAAMMJJ-HHMMSS.bak`), puis
  ajout des cinq entrées. Le nouveau fichier est écrit à part puis mis en place d'un seul geste : un échec
  laisse le fichier entier. Tout ce que l'utilisateur a écrit est gardé ; **seule la mise en page du JSON est
  réécrite**.
- Les entrées de Jarvis se reconnaissent à leur adresse, `http://127.0.0.1:<port>/hook/<route>`, **quel que
  soit le port** : un retrait trouve aussi celles d'avant un changement de port.
- « Retirer » n'enlève que ces entrées, puis efface le jeton du coffre : les lignes gardées ailleurs n'ouvrent
  plus rien. Un groupe ou un événement que le retrait a vidé part avec ; ce qui était vide avant reste.
- **Un fichier que Jarvis ne sait pas lire n'est pas modifié** : JSON invalide, commentaires, `hooks` qui
  n'est pas un objet, événement qui n'est pas une liste. La page donne alors les lignes à coller à la main.
- États affichés : non installés, installés, **pas à jour** (autre port, autre jeton, ou une ligne retirée à
  la main), fichier illisible.
- Le jeton est tiré la première fois que la page montre les lignes.

### Forme des hooks installés

```json
{ "hooks": { "Stop": [ { "hooks": [ {
  "type": "command",
  "command": "curl.exe -s -m 2 --noproxy 127.0.0.1 -X POST -H \"Authorization: Bearer <jeton>\" -H \"Content-Type: application/json\" --data-binary @- http://127.0.0.1:47821/hook/task.idle",
  "async": true,
  "timeout": 5
} ] } ] } }
```

**Ce sont des commandes en arrière-plan (`async`), pas des hooks `http`.** L'ADR 0021, section 6, demande de
vérifier qu'un hook dont le serveur ne répond pas ne bloque ni ne ralentit la session, et sinon d'installer
des commandes qui échouent en silence. Cette vérification demande un vrai Claude Code sur Windows ; elle n'a
pas été faite. Les commandes en arrière-plan sont donc installées d'emblée :

- Claude Code n'attend pas un hook `async` : Jarvis fermé, la session ne ralentit pas ;
- `curl` abandonne seul après 2 secondes (`-m 2`) et ne dit rien (`-s`) ;
- `curl.exe` par son nom complet : dans Windows PowerShell, `curl` est une autre commande. La ligne est
  valable dans Git Bash et dans PowerShell ;
- le corps du hook, reçu sur l'entrée standard, est posté tel quel.

Un test lance cette ligne même par un interpréteur de commandes contre le vrai serveur : le corps arrive, la
réponse est vide ; serveur arrêté, la commande échoue sans rien écrire, en moins de 4 secondes.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `ILocalServer`, `LocalServerStatus` (`Kernel/LocalServer`) | Le serveur local, service natif de l'hôte ; le jeton de l'exécution |
| `ILocalRoute`, `LocalRequest`, `LocalResponse`, `LocalCaller` | Une route donnée par une tranche, et qui l'appelle |
| `LocalServerOptions` | Le port ; la référence du jeton des hooks dans le coffre |
| `HookPaths` | Les cinq chemins `/hook/…` |
| `IClaudeUserSettings`, `ClaudeUserSettingsFile`, `ClaudeUserSettingsState` (`Kernel/ClaudeCode`) | Le fichier de réglages de l'utilisateur, service natif de l'hôte |

## Hôte Web

`SimulatedLocalServer` n'ouvre aucun port : il dit qu'il écoute sur le port réglé. `SimulatedClaudeUserSettings`
garde en mémoire un fichier avec un réglage et un hook de l'utilisateur. Boutons de la page de test : « Make
the Claude Code settings unreadable », « Put the Claude Code settings back ».

## Écarts par rapport à l'ADR 0021

1. **Une page à part**, « Hooks Claude Code », juste après « Sessions Claude », au lieu d'un bouton dans cette
   page : deux tranches ne partagent pas une page (ADR 0002).
2. **Commandes en arrière-plan plutôt que hooks `http`** pour les sessions lancées à la main : le cas prévu
   par la section 6 quand la vérification n'est pas faite. Les sessions lancées par Jarvis (chantier 12)
   gardent les hooks `http` de la section 5.

## Choix provisoires de ce chantier

À confirmer par Benoit :

1. **Port 47821** au départ.
2. **Une session lancée à la main devient une tâche à son premier hook**, `idle` si ce hook est `SessionStart`.
3. **Le jeton figure en clair dans `settings.json`** de l'utilisateur : un hook doit le connaître. Il n'ouvre
   que les routes `/hook/…`, qui ne décident rien.
4. **Le retrait efface le jeton** ; une nouvelle installation en tire un autre.
5. **Le fichier est réécrit avec une indentation de deux espaces.** Un fichier avec commentaires n'est pas
   touché.
6. **Une sous-tâche apparaît quand son sous-agent s'arrête** : aucun hook installé ne dit qu'il commence.

## Limites connues

- Une tâche de source `hook` reste `waiting_approval` après la réponse de l'utilisateur, jusqu'au `Stop` ou à
  la demande suivante : aucun hook installé ne dit que la permission a été donnée.
- Rien n'annonce encore à voix haute : c'est le chantier `stage2/13`.
- Les hooks ne valent que pour les sessions démarrées après l'installation.

## Non vérifié

- **Rien n'a tourné sur Windows, et aucun vrai Claude Code n'a appelé ces routes.** La forme des hooks
  (`async`, `timeout`, commande) vient de la documentation de Claude Code, lue le 21 novembre 2026, et des
  corps observés à l'essai E12.
- Que `curl.exe` reçoive bien l'entrée standard du hook dans Git Bash et dans PowerShell.
- Qu'un hook en échec ne montre rien dans la session de l'utilisateur.
- L'écriture du vrai `settings.json` : faite dans un dossier de test seulement.

## Tests

- Unitaires : `KestrelLocalServerTests` (jetons, adresse de bouclage seule, port pris, changement de port, la
  vraie ligne de commande), `ClaudeUserSettingsTests` (copie datée), `TaskHooksTests`, `HookEntriesTests`,
  `ClaudeHooksTests`.
- Playwright : `ClaudeHooksTests` (aperçu sans écriture ; installer puis retirer ; changement de port ;
  fichier illisible).

## Vérifications sur Windows

À faire avec Benoit, sur son PC. **Jarvis n'écrit dans son `settings.json` que par son clic.**

1. Ouvrir la page : le serveur écoute sur le port 47821 ; `netstat -ano | findstr 47821` ne montre que
   `127.0.0.1`.
2. Lire l'aperçu, cliquer « Installer » : la copie datée existe, les autres réglages sont intacts.
3. Démarrer une session Claude Code, lui écrire : la page « Sessions Claude » montre la tâche `working`, puis
   `idle` à la fin de la réponse ; `waiting_approval` quand elle demande une permission.
4. **Fermer Jarvis, puis écrire à une session** : elle répond sans retard et sans message d'erreur de hook.
5. Refaire le point 4 sans Git Bash (PowerShell seul), si le cas se présente.
6. Cliquer « Retirer » : seules les lignes de Jarvis sont parties.
