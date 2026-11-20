# ADR 0021 : sessions Claude Code (lancement, permissions, hooks, annonces)

## Statut

Proposé le 2026-11-20 (chantier `stage2/2-adrs`). Les décisions prises par Benoit sont dans la section
« Réponses de Benoit ». Il n'y a plus de question ouverte dans cet ADR ; il s'accepte avec les ADR 0017 et 0018.

- **Tranche les points laissés par :** [ADR 0009](0009-permission-and-confirmation-model.md) (section 7 : la
  ligne de commande exacte, et si les réglages du projet sont chargés),
  [ADR 0013](0013-relais-readiness.md) (sections 6 et 7 : l'installation des hooks des sessions lancées à la
  main), [ADR 0020](0020-hand-off-targets-and-spend-cap.md) (la classe de `hand_off_to_claude` pour `task`,
  et comment le résumé et la demande initiale arrivent à Claude Code).
- **Récits :** US2.9 à US2.15 du [document de conception](../plans/design.md).
- **Essais :** E2, E3, E8 et E12 ([résultats d'E12](../../../../spikes/e12-claude-code-permissions/RESULTS.md)).
- **S'appuie sur :** [ADR 0017](0017-tools-classes-and-decision-point.md) (point de décision).

## Contexte

- Essai E12, Claude Code 2.1.328 : chaque demande de permission de `claude -p` arrive à l'outil MCP local
  donné par `--permission-prompt-tool`, et la réponse est respectée (2 refus, 2 accords).
- Les hooks déclarés dans le fichier passé par `--settings` appellent des routes locales et voient
  `RELAIS_TASK_ID`. **Un hook `http` ne part jamais sur `SessionStart`** : il faut une commande.
- **Le `CLAUDE.md` d'un dossier n'est chargé qu'avec la source `project`**, qui exécute aussi les hooks du
  dépôt, sans question en mode `-p`. `--append-system-prompt-file` donne le `CLAUDE.md` seul.
- Un sous-agent lancé en arrière-plan fait un second tour : second `UserPromptSubmit`, second `Stop`, second
  `result`.
- Essais E2 et E3 : le registre `~/.claude/sessions/<pid>.json` liste les sessions du PC, Claude Desktop
  compris, et se lit en 73 ms. `sessions/*.key` et `ide/*.lock` contiennent des secrets.
- L'ADR 0013 fixe la forme d'une tâche, ses neuf statuts et le nom des routes `/hook/…`.

## Décision

### 1. Ligne de commande d'une session lancée par Jarvis

```text
claude -p --input-format stream-json --output-format stream-json --verbose
  --setting-sources ""                                   (ou "project" pour un dossier de confiance, section 2)
  --append-system-prompt-file <dossier>\CLAUDE.md        (si le fichier existe et que la source est "")
  --settings <fichier de réglages de Jarvis>             (hooks, section 5)
  --mcp-config <fichier MCP de Jarvis> --strict-mcp-config
  --permission-prompt-tool mcp__jarvis__approve
  --allowedTools Read,Glob,Grep
  --add-dir <dossiers autorisés utiles à la tâche>
  --max-budget-usd <plafond de la session>
  [--model <modèle réglé>] [--resume <session> pour un tour suivant]
```

- Le processus démarre dans le dossier de la tâche, avec `RELAIS_TASK_ID=<identifiant de la tâche>` dans son
  environnement. **La tâche est créée avant le lancement** (ADR 0013, section 7).
- Les fichiers de Jarvis sont sous `%USERPROFILE%\.kazo\jarvis\claude\`. Jarvis les réécrit à son démarrage.
  **Il n'écrit jamais dans `%USERPROFILE%\.claude\`** pour ses propres sessions.
- Jarvis cherche `claude` dans le `PATH` et lit sa version. Sous 2.1.328, la page le dit et propose la mise à
  jour. L'option `--permission-prompt-tool` n'apparaît plus dans l'aide : **Jarvis surveille donc le flux de
  chaque session, et l'arrête si un outil d'écriture ou de commande s'exécute sans que `approve` ait reçu sa
  demande.**
- Dossier de la tâche : un dossier autorisé (ADR 0009, section 5). Si la demande n'en désigne pas un seul,
  Jarvis demande lequel.
- Plafond d'une session : réglage, **2 $ US par défaut**, jamais plus que ce qui reste du plafond mensuel
  quand Claude Code est compté (ADR 0020, section 6).
- Sessions lancées par Jarvis en même temps : 3 au plus, réglable.

### 2. Réglages chargés

- **Par défaut : rien de l'utilisateur ni du dépôt** (`--setting-sources ""`), et les consignes du dossier
  par son `CLAUDE.md` seul (réponse n° 2). Limite connue : seul le `CLAUDE.md` de la racine est lu, pas ceux
  des sous-dossiers, ni les `@imports`, ni `.claude/rules`.
- **Dossier « de confiance » :** une case par dossier autorisé, décochée par défaut. Cochée, la session
  charge la source `project` : consignes complètes, **et hooks du dépôt exécutés sans question**. La case le
  dit en toutes lettres.
- Les réglages et les hooks personnels de l'utilisateur ne sont jamais chargés (ADR 0009, section 7).

### 3. Serveur local de Jarvis

- Un serveur HTTP dans le processus de Jarvis, **à l'écoute de `127.0.0.1` seulement**, port réglable (un
  port fixe : les hooks installés chez l'utilisateur doivent le connaître). Il porte :
  - `/mcp` : le serveur MCP de Jarvis (`ModelContextProtocol.AspNetCore`), avec l'outil `approve` ;
  - `/hook/task.context`, `/hook/task.start`, `/hook/approval.requested`, `/hook/task.idle`,
    `/hook/subtask.idle` : les routes de l'ADR 0013.
- **Aucune route sans jeton.** Jarvis tire un jeton à chaque démarrage, le garde en mémoire et le donne à ses
  sessions par une variable d'environnement, reprise dans un en-tête (`allowedEnvVars`, prouvé par E12 pour
  les hooks ; à vérifier pour le fichier MCP au chantier `stage2/11-tasks-and-sessions`). Les hooks installés
  chez l'utilisateur (section 6) ont un jeton à part, dans le coffre, qui ne donne accès qu'aux routes
  `/hook/…`.
- Ce serveur n'est pas le serveur intégré du mobile ([ADR 0003](0003-hosts-and-native-services.md), étape 4) :
  il ne s'ouvre jamais au réseau.
- Le corps d'un hook est une **donnée** (ADR 0009, principe n° 2).

### 4. Permissions d'une session lancée par Jarvis

- **Un seul point de décision** (réponse n° 1) : l'outil `approve`. Il passe la demande au point de décision
  de l'ADR 0017, avec la session comme demandeur.
- Classe d'une demande :

  | Outil de Claude Code | Classe |
  | --- | --- |
  | `Read`, `Glob`, `Grep` dans un dossier de la session | Lecture (permis par `--allowedTools`, la demande n'arrive pas) |
  | `Write`, `Edit`, `NotebookEdit` dans un dossier de la session | Sensible |
  | `Bash` et tout lancement de commande | Sensible |
  | Tout chemin hors des dossiers de la session, `sessions/*.key`, `ide/*.lock` | Interdite |
  | Tout autre outil (`WebFetch`, `WebSearch`, `Agent`, outil MCP…) | Sensible |

- La demande de confirmation (ADR 0009, section 3) dit le dossier, l'outil et le détail exact : le chemin et
  les différences pour une écriture, la commande entière pour `Bash`.
- Réponse rendue à Claude Code : `allow` avec l'entrée reçue, **inchangée**, ou `deny` avec la cause. Jarvis
  ne corrige jamais une entrée.
- Sans réponse humaine dans le délai : `deny`. Le délai après lequel Claude Code abandonne l'appel n'a pas été
  mesuré ; il se mesure au chantier `stage2/12-claude-code-tasks`, et le délai de confirmation de Jarvis se
  règle en dessous.
- Le hook `PermissionRequest` **ne décide rien** : il passe seulement la tâche à `waiting_approval`.

### 5. Hooks des sessions lancées par Jarvis

Dans le fichier de réglages de Jarvis, avec l'en-tête `X-Relais-Task: $RELAIS_TASK_ID` :

| Hook | Type | Route | Effet sur la tâche |
| --- | --- | --- | --- |
| `SessionStart` | commande (`curl`, livré avec Windows) | `/hook/task.context` | Lie la session à la tâche |
| `UserPromptSubmit` | `http` | `/hook/task.start` | `working` |
| `PermissionRequest` | `http` | `/hook/approval.requested` | `waiting_approval` |
| `SubagentStop` | `http` | `/hook/subtask.idle` | La sous-tâche passe à `done` |
| `Stop` | `http` | `/hook/task.idle` | `idle` |

- Une tâche créée par Jarvis est `pending` jusqu'au premier hook.
- **Une tâche lancée par Jarvis n'est `done` qu'au `result` du flux sans tâche de fond en cours.** Un
  `UserPromptSubmit` dont le texte commence par `<task-notification>` n'est pas une demande humaine.
- Le processus s'arrête sans `result` : la tâche passe à `blocked`, avec la cause. Arrêtée par
  l'utilisateur : `abandoned`.
- La décision rendue par `approve` remet la tâche à `working`.

### 6. Sessions lancées à la main

- Jarvis les voit par le registre `~/.claude/sessions/<pid>.json` (US2.11, US2.14), lu à intervalle régulier.
  **Il ne lit jamais `sessions/*.key` ni `ide/*.lock`** ; un test le prouve.
- Pour savoir qu'une session attend ou a fini (US2.12), il faut ses hooks. **Jarvis les installe dans
  `%USERPROFILE%\.claude\settings.json` seulement quand l'utilisateur le demande**, par un bouton de la page
  « Sessions Claude » :
  - la page montre d'abord les lignes exactes qui seront ajoutées ;
  - Jarvis fait une copie datée du fichier, puis ajoute ses entrées sans toucher aux autres ;
  - ses entrées se reconnaissent à leur adresse (le port et les routes de Jarvis) ; le bouton « Retirer »
    n'enlève que celles-là ;
  - un fichier que Jarvis ne sait pas lire n'est pas modifié : la page donne les lignes à coller à la main.
- **Avant d'offrir ce bouton**, le chantier 11 vérifie qu'un hook dont le serveur ne répond pas (Jarvis fermé)
  ne bloque ni ne ralentit la session de l'utilisateur. Si ce n'est pas le cas, les hooks installés sont des
  commandes qui échouent en silence.
- Pour ces sessions, Jarvis **annonce seulement** : il ne répond jamais à leur place (ADR 0009, section 7,
  US2.15). Sa route `/hook/approval.requested` ne rend aucune décision.
- Une session vue par le registre sans hook s'affiche avec l'état du registre et sa dernière activité.

### 7. Forme et rangement des tâches

- La forme, les neuf statuts, la clé de projet et la clé de sujet sont ceux de l'ADR 0013, section 6. Les
  tâches sont dans `jarvis.db`.
- La liste des sessions est regroupée par projet. Une session lancée par Jarvis porte la source `harness`, une
  session vue par ses hooks la source `hook`.

### 8. Passage de main vers Claude Code

- `hand_off_to_claude` avec `kind: task` est de classe **« Lecture »** : la session démarre en lecture seule
  sur un dossier autorisé, et chaque écriture se confirme ensuite (réponse n° 3). Jarvis dit le dossier
  avant de lancer (« Je lance Claude Code sur Kazo Bar. ») ; une interruption annule.
- Le premier message de la session, envoyé par le flux d'entrée, porte dans cet ordre (ADR 0020, réponse
  n° 1) :
  1. un **résumé des échanges précédents**, fait par le modèle local, présenté comme un contexte ;
  2. la **demande initiale complète**, mot pour mot.
- Rien d'autre de la mémoire ne part (ADR 0020, section 4).
- À la fin, Jarvis dit le résumé de la session (son dernier message, raccourci par le modèle local) et ajoute
  le coût annoncé au compteur, selon le réglage « Claude Code est payé par abonnement » (ADR 0020).

### 9. Annonces

- Jarvis annonce à voix haute quand une session **se termine** ou **attend une permission** (US2.12).
- Réglage par dossier : tout, les attentes seulement, rien. Par défaut : tout pour les sessions lancées par
  Jarvis ; tout pour les autres une fois leurs hooks installés.
- **Heures de silence** : aucune par défaut, réglables (réponse n° 4). Pendant ces heures, rien n'est dit ;
  l'orbe garde son badge ambre (ADR 0011) et l'annonce reste dans la conversation.
- **« Je vais dormir »** (réponse n° 4) : si l'utilisateur dit qu'il va dormir et qu'aucune plage de silence
  n'est réglée, Jarvis lui propose le silence pour 8 heures. Le silence ne commence qu'après son « oui ».
  C'est un outil du modèle local, `start_quiet_period`, de classe « Effet courant » ; il ne change pas les
  plages réglées et se termine seul.
- Une annonce n'interrompt pas l'utilisateur ni une réponse en cours : elle attend que l'assistant soit au
  repos.
- « Où en est Claude sur Kazo Bar ? » (US2.13) : le modèle local résume à partir du dernier message et de
  l'état de la tâche, reçus comme données.

### 10. Tranches

- `Tasks/Board` : la forme d'une tâche, son rangement, les routes `/hook/…`.
- `ClaudeCode/Sessions` : le registre et la page « Sessions Claude ».
- `ClaudeCode/Launch` : le lancement, le flux JSON, l'outil `approve`.
- `ClaudeCode/Hooks` : l'installation et le retrait des hooks chez l'utilisateur.
- `ClaudeCode/Announcements` : les annonces et les heures de silence.
- Le processus `claude` et le serveur local sont des services natifs derrière des contrats du noyau, mis en
  œuvre dans `Kazo.Jarvis.Adapters` ; l'hôte Web en a des versions simulées.

## Réponses de Benoit

Questions posées dans la conversation le 2026-11-20, après l'essai E12. Réponse : « je suis d'accord avec tes
recommandations ».

1. **Où se décide une écriture demandée par Claude Code ?** À un seul endroit : l'outil de permission de
   Jarvis, qui pose la question à la voix ou près de l'orbe.
2. **Une session lancée par Jarvis charge-t-elle les réglages du dépôt ?** Non : le `CLAUDE.md` seul, sans
   réglages ni hooks. Les réglages complets seulement pour les dossiers marqués « de confiance ».

Questions posées le même jour à l'écriture de cet ADR, réponses de 18 h 46 :

3. **Lancer une session Claude Code demande-t-il une confirmation ?** Non. Jarvis dit le dossier et lance ;
   la session est en lecture seule et chaque écriture se confirme.
4. **Heures de silence par défaut.** Aucune. Benoit ajoute : « si l'utilisateur dit qu'il va dormir, il
   serait bien que l'agent lui propose d'activer le mode silencieux pour une durée de 8 heures si aucune
   planification n'est configurée ».

## Options écartées

- **Laisser Claude Code décider avec `--allowedTools` large ou `--permission-mode acceptEdits`.** L'accord ne
  viendrait plus d'un humain (ADR 0009, principe n° 4).
- **Décider aussi dans le hook `PermissionRequest`.** Deux endroits qui décident : le premier qui répond
  l'emporte (E12).
- **Toujours charger la source `project`.** Les hooks d'un dépôt cloné s'exécuteraient sans question.
- **Installer les hooks chez l'utilisateur sans le demander.** Jarvis ne touche pas aux réglages d'un autre
  programme sans accord.

## Conséquences

- Une session lancée par Jarvis ne profite ni des greffons ni des hooks de l'utilisateur : contexte plus
  petit (60 commandes au lieu de 470 à l'essai), comportement prévisible.
- Sans la case « de confiance », les consignes des sous-dossiers et les `@imports` d'un dépôt ne sont pas lus.
- Jarvis dépend d'options de Claude Code qui peuvent changer d'une version à l'autre ; la surveillance de la section 1
  arrête une session dont les permissions n'arrivent pas, plutôt que de la laisser écrire.
- **Non vérifié par E12 :** le délai d'abandon de l'appel `approve`, un mode de permission autre que celui par
  défaut, le coût d'une vraie tâche avec Sonnet ou Opus, le comportement quand le serveur local ne répond
  pas, le jeton dans le fichier MCP. Ces points se vérifient aux chantiers 11 et 12.
- En v2, avec Relais, l'humain pourra décider depuis Jarvis pour une session qu'il n'a pas lancée (ADR 0013,
  section 9). Un nouvel ADR changera alors la section 6.
