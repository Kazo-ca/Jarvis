# Tâches Claude Code : « Demande à Claude Code de… », dossier, permissions, fin d'une session

Chantier `stage2/12-claude-code-tasks`, **seconde partie** du chantier 12 (US2.9, US2.10, US2.15). Décisions :
[ADR 0021](adr/0021-claude-code-sessions.md), sections 1, 2, 4, 5 et 8, et
[ADR 0020](adr/0020-hand-off-targets-and-spend-cap.md), section 6. La première partie
([claude-code-launch.md](claude-code-launch.md)) a posé le lancement du processus et l'outil `approve` ;
celle-ci les branche à la conversation.

## Ce que voit l'utilisateur

- « Demande à Claude Code de… » (ou "Ask Claude Code to…"), dit ou écrit : Jarvis répond « Je lance Claude
  Code sur *dossier*. » et le tour se termine. La session travaille seule.
- La session lit, liste et cherche dans son dossier sans demander. **Chaque écriture et chaque commande ouvre
  la demande de confirmation du chantier 4**, avec le dossier, l'outil et le chemin exact ou la commande
  entière. Sans réponse, la session attend : aucun délai ne répond à la place de l'utilisateur (ADR 0023).
- La page « Tâches Claude Code » (réglages, ordre 54) : Claude Code trouvé ou non et sa version, les sessions
  en cours avec un bouton « Arrêter », le plafond d'une session, le nombre de sessions en même temps, et une
  case « de confiance » par dossier autorisé.
- La page « Dépense » : la case « Claude Code est payé par abonnement » et le coût annoncé par les sessions.

## Tranche `ClaudeCode/Launch`

```
conversation ── IClaudeTasks (noyau) ──► ClaudeTaskRunner ── IClaudeCode (noyau) ──► processus claude -p
                                              │  ▲
                 ITaskBoard (tâche créée      │  └── IClaudeApprovals (noyau) ◄── outil approve du serveur local
                 avant le lancement, statuts) │
                                              ├── IAllowedFolders (noyau) : dossiers autorisés, vérification d'un chemin
                                              ├── IPermissionDecision : le point de décision, pour chaque demande
                                              ├── ISpendCap.SessionBudget : plafond de la session
                                              └── publie ClaudeTaskEnded (fin, résumé, coût)
```

| Fichier | Rôle |
| --- | --- |
| `ClaudeTaskRunner` | Lance, suit et arrête les sessions ; reçoit leurs demandes de permission |
| `FolderChoice` | Le dossier qu'une demande nomme, par du code |
| `SessionRules` | La classe d'une demande d'une session (table de l'ADR 0021, section 4) |
| `ClaudeLaunchOptions` | Section `ClaudeCode.Launch` de `config.json` |
| `ClaudeLaunchPage` | La page « Tâches Claude Code » |

### Choix du dossier

- Le dossier d'une session est **un dossier autorisé** (page « Dossiers autorisés »), jamais un autre.
- Un seul dossier autorisé : c'est lui. Plusieurs : celui que la demande nomme. Le nom est comparé par du
  code, sans égard à la casse, aux accents ni aux séparateurs : « Kazo Bar » nomme `kazo-bar`, `KazoBar` et
  `Kazo Bar`.
- Aucun ou plusieurs dossiers nommés : Jarvis demande « Sur quel dossier ? A, B ou C. ». **Le tour suivant
  répond** s'il nomme un seul dossier, dans les deux minutes ; toute autre demande oublie la question.
- Aucun dossier autorisé : Jarvis le dit et ne lance rien.

### Lancement

Dans cet ordre, et le premier refus arrête tout avec sa phrase :

1. Claude Code présent sur cet hôte, un dossier, une place parmi les sessions (3 au plus par défaut).
2. Claude Code trouvé dans le `PATH`.
3. Plafond de la session : le réglage (2 $ US par défaut), **jamais plus que ce qui reste du plafond
   mensuel quand Claude Code compte**. À zéro, rien ne démarre.
4. Jarvis dit le dossier. **Une interruption pendant cette phrase annule le lancement.**
5. La tâche est créée (`pending`, source `harness`), puis le résumé des échanges précédents est demandé au
   modèle local, puis la session démarre.

Premier message de la session (ADR 0021, section 8) : le résumé, présenté comme un contexte, puis la demande
mot pour mot. Sans échange précédent, ou si le résumé échoue, la demande part seule. **Rien d'autre de la
mémoire ne part.** Dans le dossier d'un projet, une ligne en tête dit où est le fichier `JARVIS.md` du projet
([projects.md](projects.md)) : le chemin seulement, pas son contenu.

Aucune confirmation n'est demandée pour lancer (réponse n° 3 de l'ADR 0021) : la session est en lecture seule
tant qu'une écriture n'est pas approuvée.

### Permissions d'une session

Chaque demande reçue par `approve` est classée par `SessionRules`, puis passe par le point de décision avec
la session comme demandeur ; elle est journalisée comme les autres.

| Demande | Classe |
| --- | --- |
| `Read`, `Glob`, `Grep` dans le dossier de la session | Lecture |
| `Write`, `Edit`, `MultiEdit`, `NotebookEdit` dans le dossier | Sensible |
| `Bash` | Sensible ; la confirmation montre la commande entière |
| Tout autre outil | Sensible |
| Un chemin hors du dossier de la session, quel que soit l'outil | Interdite |
| `sessions/*.key`, `ide/*.lock` sous un dossier `.claude` | Interdite |
| Une écriture qui ne dit pas où | Interdite |
| Le dossier de la session n'est plus un dossier autorisé | Interdite |

- Un chemin relatif se lit depuis le dossier de la session ; le chemin est résolu (liens suivis) avant d'être
  comparé.
- Une demande qui ne nomme aucune session suivie par Jarvis est refusée sans question.
- Réponse rendue à la session : `allow` avec l'entrée inchangée, ou `deny` avec la cause, en anglais.
- La tâche passe à `waiting_approval` pendant la question et revient à `working` après la réponse.

### Surveillance du flux

Si un outil d'écriture ou `Bash` s'exécute sans erreur alors qu'`approve` n'a jamais reçu sa demande, **Jarvis
arrête la session** et la tâche passe à `blocked`, avec la cause (ADR 0021, section 1). C'est ce qui arriverait
si une version de Claude Code cessait d'appeler l'outil de permission, ou si les réglages d'un dossier de
confiance permettaient une commande d'avance.

### Fin d'une session

| Ce qui arrive | Tâche | Résumé |
| --- | --- | --- |
| Un résultat, puis le processus se termine | `done` | Le dernier message de la session |
| Un résultat en erreur (plafond atteint…) | `blocked` | Le dernier message |
| Le processus se termine sans résultat | `blocked` | « La session s'est arrêtée sans résultat. » |
| Arrêtée par la surveillance | `blocked` | L'outil qui s'est exécuté sans demander |
| Arrêtée par l'utilisateur (page) | `abandoned` | « Arrêtée par l'utilisateur. » |
| Jarvis se ferme | `blocked` | « Jarvis a été fermé pendant que la session tournait. » |

- Un résultat ne termine pas la tâche tant que le processus vit : un travail de fond peut continuer (un
  second résultat arrive alors).
- **À la fermeture, Jarvis arrête toutes ses sessions** : aucune ne continue d'écrire sans personne pour
  répondre. Au démarrage, une tâche de source `harness` restée ouverte (Jarvis a planté) passe à `blocked`.
- À la fin, `ClaudeTaskEnded` est publié : tâche, dossier, fin, résumé, coût annoncé.

## Réglages (`ClaudeCode.Launch`)

| Réglage | Défaut | Bornes |
| --- | --- | --- |
| `SessionBudgetUsd` | 2 | 0,1 à 100 |
| `MostSessions` | 3 | 1 à 10 |
| `TrustedFolders` | aucun | des dossiers autorisés |
| `Model` | aucun (celui de Claude Code) | un mot ; pas encore dans la page |

**Dossier de confiance :** la session charge les réglages du projet, **hooks du dépôt compris, exécutés sans
question**. La page le dit en toutes lettres au-dessus des cases.

## Coût (`Llm/Spend`)

- `SpendOptions.ClaudeCodeBySubscription`, **coché par défaut** (ADR 0020, réponse n° 4).
- Coché : le coût annoncé par chaque session est additionné **à part** (`SpendCount.ClaudeCodeUsd`), affiché
  sous la case, et ne compte pas dans le plafond.
- Décoché : il compte dans le plafond comme une réponse cloud, avec les mêmes avertissements à 80 % et au
  plafond, et `ISpendCap.SessionBudget` borne chaque session à ce qui reste.
- Le coût est celui que Claude Code annonce (`total_cost_usd`) : Jarvis ne le calcule pas. Une session
  bloquée ou abandonnée qui a annoncé un coût est comptée aussi.

## Conversation (`Conversation/Dialogue`)

- Un passage de genre `task` va à `IClaudeTasks` quand la tranche est là ; sinon le refus d'avant
  (« la cible des tâches n'est pas encore disponible »).
- Par la formule (« Demande à Claude Code… ») : le modèle local n'est ni chargé ni interrogé, sauf pour le
  résumé quand il y a des échanges précédents.
- Par l'outil `hand_off_to_claude` avec `kind: task` : la demande du tour part mot pour mot ; l'outil est
  terminal, rien ne revient au modèle local. **Ce genre n'est pas encore proposé au modèle local** (voir les
  écarts).
- Le résumé est fait par un appel sans outil : les échanges sont un seul message à résumer, avec la consigne de
  ne suivre aucune instruction qu'ils contiennent. 1 500 caractères au plus.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `IClaudeTasks`, `ClaudeTaskRequest`, `ClaudeTaskOpening` (`Kernel/ClaudeCode`) | Confier un travail à Claude Code depuis la conversation |
| `ClaudeTaskEnded`, `ClaudeTaskEnd` | La fin d'une session, pour le compteur et les annonces |
| `IAllowedFolders`, `PathVerdict`, `CheckedPath` (`Kernel/Files`) | Les dossiers autorisés, vus par une autre tranche que celle des outils de fichiers |
| `ISpendCap.SessionBudget` | Ce qu'une session peut coûter au plus |

## Hôte Web

`SimulatedClaudeCode` tient lieu de Claude Code. Sa session demande à écrire `NOTES.md` dans son dossier, par
le même contrat que le vrai outil `approve`. Des mots du message changent le scénario : « outside » ou
« hors » (écriture hors du dossier), « without asking » ou « sans demander » (commande qui n'a rien demandé),
« wait » ou « attends » (travaille jusqu'à l'arrêt). La page de test montre le dernier lancement et la tâche
dans `#claude-code`.

## Écarts par rapport à l'ADR 0021 et au plan

1. **Le résumé de fin n'est pas dit à voix haute dans ce chantier.** Il est gardé sur la tâche et publié
   (`ClaudeTaskEnded`). Le dire sans couper l'utilisateur, hors d'un tour et en respectant les heures de
   silence, est le travail du chantier `stage2/13-session-announcements`, qui construit ce mécanisme.
   **Fait au chantier 13** : voir [session-announcements.md](session-announcements.md).
2. **Une session a un seul dossier** : `--add-dir` n'est pas utilisé.
3. **Seule la formule lance une session.** L'outil `hand_off_to_claude` ne propose toujours que
   `kind: question` au modèle local : la conversation sait traiter un appel `kind: task` (testé), mais l'offrir
   laisserait le modèle local lancer une session de lui-même, ce qu'aucun essai avec le vrai modèle n'a mesuré.
   À ouvrir après un essai sur le PC de Benoit.
4. **Pas de délai de confirmation** (ADR 0023, déjà noté en première partie).

## Choix provisoires de ce chantier

À confirmer par Benoit ; chacun se change sans toucher aux autres.

1. Une écriture sans chemin, et tout chemin hors du dossier de la session même dans un autre dossier
   autorisé, sont interdits sans question.
2. La question « sur quel dossier ? » vit deux minutes et n'accepte qu'une réponse.
3. Le titre de la tâche est la demande, coupée à 80 caractères.
4. Les bornes des réglages (0,1 à 100 $ US ; 1 à 10 sessions).
5. À la fermeture de Jarvis, les sessions sont arrêtées et leurs tâches passent à `blocked`.
6. Le coût d'une session bloquée ou abandonnée est compté.
7. Le modèle (`Model`) se règle dans `config.json` seulement.

## Non vérifié

- **Aucune vraie session Claude Code n'a été lancée par ce code, et rien n'a tourné sur Windows.**
- La surveillance suppose que le flux montre chaque usage d'outil avec son identifiant et son résultat, y
  compris ceux d'un sous-agent.
- Les noms des champs de chemin des outils de Claude Code (`file_path`, `notebook_path`, `path`) viennent de
  l'essai E12 et de sa documentation ; un outil qui nommerait son chemin autrement serait classé « Sensible »
  sans vérification du dossier (il reste confirmé par l'utilisateur).
- La qualité du résumé fait par le modèle local.

## Tests

- Unitaires : `SessionRulesTests`, `FolderChoiceTests`, `ClaudeTaskRunnerTests` (dossier, annonce, tâche
  avant le lancement, plafond, permissions, surveillance, fins, fermeture), `ConversationEngineTests`
  (fichier `ConversationEngineClaudeTasksTests.cs`), `SpendTests` (coût à part ou compté).
- De bout en bout : `ClaudeTasksTests` (8 scénarios sur l'hôte Web).

## Vérifications sur Windows

À faire avec Benoit, sur son PC, avec un vrai Claude Code et un dossier de travail jetable :

1. « Demande à Claude Code de lire le README et de le résumer » : Jarvis dit le dossier, la session démarre,
   la tâche passe à `done` avec son dernier message.
2. Une écriture : la confirmation montre le chemin exact ; refusée, le fichier n'existe pas ; approuvée, il
   existe.
3. Une commande (`Bash`) : la confirmation montre la commande entière.
4. Une écriture hors du dossier : refusée sans question.
5. Les en-têtes du fichier MCP (`${JARVIS_LOCAL_TOKEN}`) sont bien développés (sinon tout est refusé).
6. Combien de temps Claude Code attend la réponse d'`approve` avant d'abandonner.
7. « Arrêter » sur la page : le processus et ses enfants disparaissent.
8. Le coût annoncé apparaît sur la page « Dépense », à part.
9. Un dossier de confiance charge bien les consignes du dépôt.
