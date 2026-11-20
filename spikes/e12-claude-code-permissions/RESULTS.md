# Résultats E12 — 20 novembre 2026

**E12 : réussi, trois surprises.** Avec Claude Code **2.1.328**, chaque demande de permission de `claude -p`
arrive à l'outil MCP local donné par `--permission-prompt-tool`, et sa réponse est respectée : 2 écritures et
2 commandes demandées, les 2 refusées n'ont rien créé, les 2 acceptées ont créé leur fichier. Les hooks
`SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `Stop` et `SubagentStop` déclarés dans le fichier passé
par `--settings` appellent les routes locales, et voient `RELAIS_TASK_ID`. Les surprises : un hook `http` ne
part jamais sur `SessionStart` ; le `CLAUDE.md` du dossier n'est chargé qu'avec la source `project`, qui
apporte aussi les hooks du dépôt ; un sous-agent lancé en arrière-plan fait un second tour, avec un second
`result`.

Session de l'essai `perm` : modèle `haiku` (Claude Haiku 5.5), 6 tours, 8,3 s, **0,009 $ annoncés**. Les
sessions de lecture des réglages : 0,0002 à 0,002 $ chacune. Total de l'essai : moins de 0,03 $.

## (a) Demande de permission par un outil MCP local

| Étape demandée | Appel à `approve` | Réponse de Jarvis | Fichier |
| --- | --- | --- | --- |
| `Write allow-write.txt` | oui | `allow` | créé |
| `Write deny-write.txt` | oui | `deny` | **absent** |
| `Bash echo ok > allow-bash.txt` | oui | `allow` | créé |
| `Bash echo no > deny-bash.txt` | oui | `deny` | **absent** |
| `Agent` (sous-agent) | non (`--allowedTools Read,Agent`) | — | — |

Format exact de la demande (requête MCP `tools/call` reçue par Jarvis) :

```json
{"method":"tools/call","params":{"name":"approve",
  "arguments":{"tool_name":"Write",
               "input":{"file_path":"C:\\jarvis\\temp\\e12-sandbox\\deny-write.txt","content":"no\n"},
               "tool_use_id":"toolu_013x…"},
  "_meta":{"claudecode/toolUseId":"toolu_013x…","progressToken":2,
           "io.modelcontextprotocol/clientInfo":{"name":"claude-code","version":"2.1.328"}, "…":"…"}},
 "jsonrpc":"2.0","id":2}
```

Pour Bash, `input` vaut `{"command":"echo no > deny-bash.txt","description":"Create deny-bash.txt containing no"}`.

Réponse de l'outil (son résultat texte, une chaîne JSON) :

```json
{"behavior":"allow","updatedInput":{"file_path":"…\\allow-write.txt","content":"ok\n"}}
{"behavior":"deny","message":"Refusé par Jarvis (essai E12)."}
```

- `updatedInput` est obligatoire pour accepter : Jarvis renvoie l'entrée reçue, ou une version corrigée.
- Les refus figurent dans `result.permission_denials` (outil, `tool_use_id`, entrée), comme dans l'essai E8.
- Le hook `PermissionRequest` part **avant** l'appel à l'outil (quelques millisecondes), avec la même entrée et
  des `permission_suggestions` (`setMode acceptEdits`, `addDirectories`). Un hook qui ne répond rien ne décide
  rien ; la documentation dit que le premier qui décide l'emporte. Jarvis ne doit décider qu'à un seul endroit :
  **l'outil MCP**, le hook ne sert qu'à suivre la tâche (`waiting_approval`).
- Le nom de l'option n'apparaît plus dans `claude --help` (2.1.328), mais elle marche. L'aide montre
  `--permission-prompts host|none` : `host` (par défaut) envoie les demandes à l'outil, `none` les refuse toutes.

## (b) Hooks vers une route locale

Hooks déclarés dans le fichier de Jarvis passé par `--settings`, de type `http`, avec l'en-tête
`X-Relais-Task: $RELAIS_TASK_ID` et `allowedEnvVars: ["RELAIS_TASK_ID"]` ; le processus `claude` est lancé avec
`RELAIS_TASK_ID=task-e12-0001`.

| Hook | Route | Reçu | Ce que le corps contient en plus des champs communs |
| --- | --- | --- | --- |
| `SessionStart` (`http`) | `/hook/task.context` | **jamais** | — |
| `SessionStart` (`command`, `curl`) | `/hook/command.session-start` | oui | `source: "startup"` |
| `UserPromptSubmit` | `/hook/task.start` | oui (2 fois, voir plus bas) | `prompt`, `prompt_id`, `permission_mode` |
| `PermissionRequest` | `/hook/approval.requested` | oui, 4 fois | `tool_name`, `tool_input`, `permission_suggestions`, `effort` |
| `SubagentStop` | `/hook/subtask.idle` | oui | `agent_id`, `agent_type`, `agent_transcript_path`, `last_assistant_message`, `background_tasks` |
| `Stop` | `/hook/task.idle` | oui (2 fois) | `last_assistant_message`, `stop_hook_active`, `background_tasks`, `session_crons` |

- **Champs communs :** `session_id`, `transcript_path`, `cwd`, `scratchpad_dir`, `hook_event_name` ; puis
  `prompt_id` et `permission_mode` dès le premier message.
- **`RELAIS_TASK_ID` est vu** par les deux sortes de hooks : en-tête `X-Relais-Task: task-e12-0001` reçu par
  tous les hooks `http` (grâce à `allowedEnvVars`) et par le hook `command` (variable héritée, développée par
  le shell). Le hook `command` passe par `curl` 8.19 ; les hooks `http` partent d'axios.
- **`SessionStart` n'accepte que les hooks `command`** : le hook `http` déclaré au même endroit n'est jamais
  parti, dans aucun des 13 lancements. Pour `/hook/task.context`, Jarvis doit installer une commande (`curl`
  est livré avec Windows) ou se passer de ce hook (il connaît déjà la session qu'il lance par `system/init`).
- **Sous-agent en arrière-plan :** l'outil `Agent` a lancé le sous-agent en tâche de fond. Le premier tour se
  termine (`Stop`, `result` avec les 2 refus) avant lui ; à sa fin, `SubagentStop` part, puis Claude Code se
  renvoie une `<task-notification>` qui déclenche un **second `UserPromptSubmit`** et un second `Stop` et
  `result` (coût cumulé). Pour Jarvis : une tâche n'est finie qu'au `result` sans `background_tasks` en cours,
  et un `UserPromptSubmit` dont le texte commence par `<task-notification>` n'est pas une demande humaine.
- `--no-session-persistence` n'empêche pas d'envoyer un `transcript_path` aux hooks.

## (c) Ce que charge `--setting-sources`

Même dossier, même demande (« quel est le mot secret de tes consignes ? »), outils coupés (`--tools ""`).
Le dossier contient un `CLAUDE.md` (mot secret), un `.claude/settings.json` et un `.claude/settings.local.json`
qui déclarent chacun un hook `SessionStart` (commande) et `UserPromptSubmit` (`http`) vers leur propre route.
Benoit n'a pas de `~/.claude/CLAUDE.md` ; ses réglages utilisateur ont des greffons et des hooks.

| `--setting-sources` | `CLAUDE.md` du dossier | Hooks du projet | Hooks du local | Greffons, commandes, compétences, agents de l'utilisateur | Hooks de l'utilisateur | Hooks de `--settings` |
| --- | --- | --- | --- | --- | --- | --- |
| `""` (aucune) | non | non | non | non (4 greffons intégrés, 60 commandes, 24 compétences, 7 agents) | non | **oui** |
| `user` | non | non | non | **oui** (6 greffons, 470 commandes, 351 compétences, 78 agents) | **oui** (2 `SessionStart`, 9 `Stop` de plus) | oui |
| `project` | **oui** | **oui** | non | non | non | oui |
| `local` | non | non | **oui** | non | non | oui |
| `project,local` | oui | oui | oui | non | non | oui |
| option absente | oui | — | — | oui | oui | oui |
| `""` + `--append-system-prompt-file CLAUDE.md` | **oui** | non | non | non | non | oui |
| `project` + `--settings '{"disableAllHooks":true}'` | oui | non | non | non | non | **non** |

- **Le `CLAUDE.md` du dossier suit la source `project`** : sans elle, Claude ne le voit pas (il propose de le lire).
- **On ne peut pas avoir `project` sans ses hooks** par les réglages : `disableAllHooks` coupe aussi les hooks
  de Jarvis. En mode `-p`, le dossier est considéré comme de confiance, donc les hooks d'un dépôt cloné
  s'exécutent sans question.
- **Pour avoir les consignes sans les hooks** : `--setting-sources ""` (ou `local`) et
  `--append-system-prompt-file <dossier>\CLAUDE.md`. Limite : seul ce fichier est lu (pas les `CLAUDE.md` des
  sous-dossiers, ni les `@imports`, ni `.claude/rules`).

## (d) Ligne de commande recommandée pour Jarvis

Claude Code **2.1.328**, sur l'abonnement de Benoit (`/login`). Le processus est lancé avec
`RELAIS_TASK_ID=<id de la tâche>` dans son environnement, dans le dossier de la tâche.

```text
claude -p --input-format stream-json --output-format stream-json --verbose
  --setting-sources ""
  --append-system-prompt-file <dossier>\CLAUDE.md          (si le fichier existe)
  --settings %USERPROFILE%\.kazo\jarvis\claude\settings.json   (hooks http vers /hook/…, SessionStart en commande)
  --mcp-config %USERPROFILE%\.kazo\jarvis\claude\mcp.json --strict-mcp-config
  --permission-prompt-tool mcp__jarvis__approve
  --allowedTools Read,Glob,Grep
  --add-dir <dossiers autorisés>
  --max-budget-usd <plafond de la session>
  [--model <modèle choisi>] [--resume <session> pour un tour suivant]
```

- Sans `--setting-sources user`, ni les greffons ni les hooks de l'utilisateur ne se chargent : contexte bien
  plus petit (60 commandes au lieu de 470) et comportement prévisible.
- Charger `project` (consignes complètes du dépôt, mais ses hooks aussi) peut devenir un choix par dossier,
  pour les dépôts de l'utilisateur ; l'ADR 0021 tranche.
- `--include-hook-events` ajoute `system/hook_started` et `system/hook_response` au flux : utile pour le
  journal, pas nécessaire.

## Non vérifié

- `--permission-mode` autre que celui par défaut (`default`) ; l'aide de 2.1.328 liste `manual`, `auto`,
  `dontAsk`, `acceptEdits`, `plan`, `bypassPermissions`.
- Le délai : la réponse de `approve` était immédiate. Une confirmation à la voix prend des secondes ; le délai
  d'expiration de l'appel MCP par Claude Code n'a pas été mesuré.
- Les sessions Opus ou Sonnet (coût réel d'une vraie tâche) : l'essai utilise Haiku pour rester sous quelques
  cents.
- Le comportement quand le serveur local de Jarvis ne répond pas (hook en échec, outil MCP absent).
- L'installation des hooks dans les réglages de l'utilisateur pour les sessions lancées à la main (chantier 11).
