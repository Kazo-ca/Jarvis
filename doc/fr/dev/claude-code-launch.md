# Lancer Claude Code : processus, outil de permission, demandes d'une session

Chantier `stage2/12-claude-code-launch`, **première partie** du chantier 12 (US2.9, US2.10). Décisions :
[ADR 0021](adr/0021-claude-code-sessions.md), sections 1 à 5, et [ADR 0017](adr/0017-tools-classes-and-decision-point.md).
Cette partie pose les services ; « Demande à Claude Code de… », le choix du dossier, la tâche déléguée et le
coût sont la seconde partie, [claude-code-tasks.md](claude-code-tasks.md).

## Ce qui est construit

```
tranche qui lance (seconde partie) ── IClaudeCode (noyau) ──► Adapters/ClaudeCode/ClaudeCodeProcess
                                                                 claude -p … (processus, flux JSON)
                                                                        │ demande de permission
session Claude Code ── MCP ──► http://127.0.0.1:<port>/mcp, outil approve   (jeton de l'exécution)
                                        │
            Adapters/LocalServer/KestrelLocalServer ── IClaudeApprovals (noyau) ──► tranche qui lance
                                                                                        │ classe la demande
                                                             IPermissionDecision (point de décision, ADR 0017)
```

## Demandes d'une session au point de décision

Un outil de Claude Code (`Write`, `Bash`…) n'est pas un outil de Jarvis : il n'est dans aucun registre.
`ActionRequest` porte donc, **pour un demandeur de sorte `Session` seulement**, un `SessionAction` : la classe
donnée par le code qui a reçu la demande (table de l'ADR 0021, section 4) et la phrase à dire.

- Lecture ou effet courant : permis sans question. Sensible ou critique : la demande de confirmation du
  chantier 4, avec l'outil et l'entrée exacte. Interdite : refusée sans question.
- Sans classe, avec une classe inconnue ou une entrée illisible : refusé.
- **Une classe donnée par un appel d'outil du modèle n'est pas écoutée** : le champ ne vaut que pour une
  session.
- Chaque décision est journalisée comme les autres.
- **Aucun délai** : Jarvis ne décide jamais à la place de l'utilisateur (ADR 0023, qui l'emporte ici sur la
  phrase « sans réponse dans le délai : `deny` » de l'ADR 0021). Si Claude Code abandonne l'appel, la demande
  est annulée.

## Outil `approve` (`/mcp`)

- Le serveur local porte le serveur MCP de Jarvis (`ModelContextProtocol.AspNetCore`), sans état : une
  requête, une réponse. Il n'offre qu'un outil, `approve`, que Claude Code appelle par
  `--permission-prompt-tool mcp__jarvis__approve`.
- **Fermé sans le jeton de l'exécution.** Le jeton des hooks de l'utilisateur, écrit dans son `settings.json`,
  reçoit `403` : il n'atteint jamais l'outil de permission.
- La demande (`tool_name`, `input`, `tool_use_id`) part à `IClaudeApprovals` avec la tâche de l'en-tête
  `X-Relais-Task`. Réponse rendue à Claude Code, sous la forme prouvée par l'essai E12 :

  ```json
  {"behavior":"allow","updatedInput":{ …l'entrée reçue, inchangée… }}
  {"behavior":"deny","message":"…la cause…"}
  ```

- **Jarvis ne corrige jamais une entrée.** Personne pour décider, ou une décision qui échoue : `deny`, sans
  détail.

## Lancement (`IClaudeCode`, `Adapters/ClaudeCode`)

- `FindAsync` : cherche `claude` dans le `PATH` et lit sa version. `ClaudeCodeInstall.UpToDate` dit si elle
  vaut au moins 2.1.328, la version de l'essai E12.
- `StartAsync(ClaudeLaunch)` : écrit les deux fichiers de Jarvis, lance le processus dans le dossier de la
  tâche, lui donne son message et **ferme l'entrée** : la session va au bout de son travail, tâches de fond
  comprises, et se termine. Un tour suivant est un nouveau lancement avec `ResumeSession`.
- Ligne de commande (ADR 0021, section 1) :

  ```text
  claude -p --input-format stream-json --output-format stream-json --verbose
    --setting-sources ""                              ("project" pour un dossier de confiance)
    --append-system-prompt-file <dossier>\CLAUDE.md   (si le fichier existe, et pas pour un dossier de confiance)
    --settings <racine de Jarvis>\claude\settings.json
    --mcp-config <racine de Jarvis>\claude\mcp.json --strict-mcp-config
    --permission-prompt-tool mcp__jarvis__approve
    --allowedTools Read,Glob,Grep
    [--add-dir …] [--max-budget-usd 2.00] [--model …] [--resume …]
  ```

- **Les réglages et les hooks de l'utilisateur ne sont jamais chargés.**
- Fichiers de Jarvis, sous `%USERPROFILE%\.kazo\jarvis\claude\`, réécrits à chaque lancement. **Rien n'est
  écrit sous `%USERPROFILE%\.claude\`.** Ils ne contiennent aucun secret : le jeton et la tâche y sont nommés
  par des variables d'environnement (`JARVIS_LOCAL_TOKEN`, `RELAIS_TASK_ID`) que seul le processus lancé
  reçoit. Le jeton n'est jamais sur la ligne de commande.
- Sans serveur local à l'écoute, aucune session ne démarre : elle ne pourrait pas demander ses permissions.
- Événements lus du flux (`ClaudeRunEvent`) : `ClaudeStarted` (session, modèle), `ClaudeToolUse`,
  `ClaudeToolResult`, `ClaudeTurnEnded` (dernier message, coût cumulé annoncé, erreur, refus), et
  `ClaudeExited`, toujours le dernier. Une ligne non comprise est ignorée : le format appartient à Claude Code.
- `StopAsync` arrête le processus et ce qu'il a lancé.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `SessionAction`, `ActionRequest.Session` (`Kernel/Permissions`) | La classe d'une action d'une session, donnée par le code |
| `IClaudeApprovals`, `ClaudeApprovalRequest`, `ClaudeApproval` (`Kernel/ClaudeCode`) | Le seul endroit où se décide une écriture d'une session lancée par Jarvis |
| `IClaudeCode`, `ClaudeLaunch`, `IClaudeRun`, `ClaudeRunEvent` et ses sortes, `ClaudeCodeInstall`, `ClaudeCodeException` | Claude Code comme programme lancé par Jarvis |

## Écarts par rapport à l'ADR 0021

1. **Pas de hook `SessionStart`** dans le fichier de Jarvis : il n'accepte que des commandes (essai E12), dont
   la syntaxe des variables dépend de l'interpréteur. La session est connue par le début du flux
   (`ClaudeStarted`) ; la tranche qui lance la liera à la tâche.
2. **Le jeton est aussi exigé sur les hooks des sessions de Jarvis** (`Authorization`), en plus de
   `X-Relais-Task`.
3. **Aucun délai de confirmation** (ADR 0023).

## Suite du chantier

La seconde partie est construite : voir [claude-code-tasks.md](claude-code-tasks.md) (tranche
`ClaudeCode/Launch`, « Demande à Claude Code de… », page de réglages, version simulée de `IClaudeCode`).

## Non vérifié

- **Ce code n'a jamais lancé un vrai Claude Code**, et rien n'a tourné sur Windows. La ligne de commande, les
  hooks `http` et la forme d'`approve` viennent de l'essai E12, fait avec un autre code.
- **Les variables dans les en-têtes du fichier MCP** (`${JARVIS_LOCAL_TOKEN}`, `${RELAIS_TASK_ID}`) : l'essai
  ne l'a prouvé que pour les hooks. Si elles ne sont pas développées, `/mcp` répondra `401` et la session ne
  pourra rien écrire (elle échoue fermée).
- Le délai après lequel Claude Code abandonne l'appel à `approve`.
- Seul `claude.exe` est cherché : une installation par npm (`claude.cmd`) n'est pas trouvée.

## Tests

- Unitaires : `PermissionDecisionPointTests` (demandes d'une session), `KestrelApproveToolTests` (un vrai
  client MCP appelle `approve` ; fermé au jeton des hooks de l'utilisateur), `ClaudeStreamTests`,
  `ClaudeCommandLineTests`, `ClaudeCodeProcessTests` (un script tient lieu de `claude` : environnement,
  fichiers, message, événements, arrêt).

## Vérifications sur Windows

Elles demandent de lancer une vraie session : la liste est dans
[claude-code-tasks.md](claude-code-tasks.md#vérifications-sur-windows).
