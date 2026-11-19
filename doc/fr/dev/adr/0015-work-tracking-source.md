# ADR 0015 : source de suivi du travail (GitHub Issues ou Azure Boards) et lien obligatoire des commits et des PR

## Statut

Proposé le 2026-11-19 (chantier `stage1/work-tracking-adr`). Fonctionnalité **pour plus tard** : rien ne se
construit avant la v2, avec Relais.

- **Source :** demande de Benoit du 2026-11-19 : pouvoir configurer une source pour l'organisation et le suivi
  des tâches (GitHub Issues ou Azure DevOps) ; une consigne donnée à chaque agent pour que ses PR et ses commits
  soient liés à un work item ou à une issue ; pour Azure DevOps, Azure Boards configuré sur le dépôt GitHub et
  `AB#{WorkItemId}` dans les commits et la description des PR ; la règle inscrite dans la mémoire de l'agent et
  dans celle du projet, l'utilisateur étant avisé d'approuver ces modifications ; une option qui force un work
  item ou une issue pour chaque sujet (feature) ou tâche, que Jarvis peut créer, modifier et suivre.
- **Documentation vérifiée le 2026-11-19 :**
  [Azure Boards : lier GitHub](https://learn.microsoft.com/en-us/azure/devops/boards/github/link-to-from-github)
  (page mise à jour le 2026-07-17),
  [connecter Azure Boards à GitHub](https://learn.microsoft.com/en-us/azure/devops/boards/github/connect-to-github),
  [GitHub : lier une PR à une issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue).
- **Complète :** [ADR 0013](0013-relais-readiness.md) (hiérarchie Projet → Sujet → Tâche → Sous-tâche,
  orchestrateur, clés de sujet), [ADR 0004](0004-configuration-storage-and-secret-vault.md) (réglages et
  coffre), [ADR 0009](0009-permission-and-confirmation-model.md) (permissions).
- **Ne remplace aucun ADR.**

## Contexte

- Relais (ADR 0013) suit le travail en projets, sujets, tâches et sous-tâches, mais ce suivi vit dans sa
  propre base. Benoit veut qu'il puisse aussi vivre là où l'équipe le suit déjà : les issues GitHub ou les
  work items d'Azure Boards.
- Les agents (Claude Code, Codex, le modèle local) font des commits et des PR. Sans lien vers une issue ou un
  work item, on ne retrouve pas pourquoi un changement a été fait, et l'état du suivi ne bouge pas tout seul.
- Ce que la documentation dit d'Azure Boards, et que les consignes aux agents doivent respecter :
  - **Prérequis :** l'application [Azure Boards](https://github.com/apps/azure-boards) installée sur le dépôt
    GitHub, et le projet Azure Boards connecté à ce dépôt. Il faut le rôle **Contributor** des deux côtés.
    Sans connexion, un lien ajouté à la main échoue.
  - **Syntaxe :** `AB#{ID}` (par exemple `AB#125`) crée un lien dans un **message de commit**, dans la
    **description** d'une PR ou dans la description d'une issue GitHub. **Il ne crée rien dans le titre d'une
    PR ni dans un commentaire.** Plusieurs work items : `AB#123, AB#124 et AB#126`. Retirer `AB#` de la
    description d'une PR retire le lien.
  - **Changer l'état :** un mot placé devant `AB#` : `Fixes AB#123` (aussi `fix`, `fixed`) passe le work item
    au premier état de la catégorie *Resolved*, ou à défaut *Completed* ; un nom d'état (`Closed AB#123`) passe
    à cet état s'il existe, sinon rien ne change. **Ces transitions ne s'appliquent que lorsque la PR est
    fusionnée dans la branche par défaut.** Dans `Fixes AB#123, AB#124`, seul le premier change d'état : il faut
    répéter le mot devant chaque work item.
  - Une PR ouverte depuis une branche créée à partir du work item (menu *New GitHub branch*) est liée
    automatiquement.
  - Azure Boards **ne refuse pas** une PR sans `AB#` : aucun contrôle de ce genre n'existe côté Microsoft.
- Côté GitHub Issues : `#123` (ou `owner/repo#123`) dans un commit ou une PR fait un renvoi ; `Fixes #123`
  (aussi `close`, `closes`, `closed`, `fix`, `fixed`, `resolve`, `resolves`, `resolved`) dans la
  **description** d'une PR ou dans un message de commit ferme l'issue quand la PR ou le commit arrive dans la
  **branche par défaut**.

## Décision

### 1. Une source de suivi par projet Relais

- Un réglage par **projet** (le dépôt, au sens de l'ADR 0013) : `source de suivi` = `aucune` (par défaut),
  `GitHub Issues` ou `Azure Boards`.
- `GitHub Issues` : le dépôt lui-même, ou un autre dépôt désigné.
- `Azure Boards` : l'organisation Azure DevOps, le projet, et au besoin le chemin de zone et l'itération par
  défaut. Le réglage vérifie, et affiche, que l'application Azure Boards est installée et le dépôt connecté ;
  sinon il dit quoi faire, avec le lien.
- Les jetons d'accès (jeton GitHub, jeton d'accès personnel Azure DevOps ou connexion Microsoft Entra) vont
  dans le coffre (ADR 0004), jamais dans le dépôt.

### 2. Correspondance avec la hiérarchie de Relais

| Relais | GitHub Issues | Azure Boards |
| --- | --- | --- |
| Projet | dépôt | projet Azure DevOps (et chemin de zone) |
| Sujet | issue parente | *Feature* (ou *Epic*, réglable) |
| Tâche | sous-issue | *User Story*, *Product Backlog Item* ou *Issue* selon le processus (réglable) |
| Sous-tâche | sous-issue de la tâche, ou rien | *Task*, ou rien |

- La clé de sujet `issue:<n>` de l'ADR 0013 sert pour GitHub. Pour Azure Boards s'ajoute la clé `ab:<id>`.
- Les états d'Azure Boards dépendent du processus (Agile, Scrum, Basic, ou personnalisé). Jarvis ne s'appuie
  donc pas sur des noms d'état mais sur les **catégories d'état** (*Proposed*, *In Progress*, *Resolved*,
  *Completed*, *Removed*), que l'API fournit pour chaque type de work item.

| Statut Relais | GitHub Issues | Catégorie Azure Boards |
| --- | --- | --- |
| `pending` | ouverte | *Proposed* |
| `working`, `paused`, `idle`, `waiting_answer`, `waiting_approval`, `blocked` | ouverte | *In Progress* |
| `done` | fermée, *completed* | *Resolved* puis *Completed* à la fusion |
| `abandoned` | fermée, *not planned* | *Removed* |

- Le détail des six statuts actifs n'est pas poussé dans le suivi (trop bruyant) ; une étiquette ou un champ
  pour `blocked` pourra s'ajouter en v2.

### 3. Consigne aux agents : chaque commit et chaque PR sont liés

Quand une source est réglée, Jarvis donne à **chaque agent** qu'il lance ou qui se branche sur Relais
(réglages Claude Code propres à Jarvis, `AGENTS.md` pour Codex, consigne système du modèle local) la consigne
suivante, adaptée à la source :

- **Azure Boards (projets Azure DevOps seulement) :**
  - chaque message de commit contient `AB#<id>` du work item de la tâche ;
  - la **description** de chaque PR contient `AB#<id>` (le titre ne compte pas, un commentaire non plus) ;
  - pour fermer le travail à la fusion : `Fixes AB#<id>` dans la description, **le mot répété devant chaque
    work item** ; seule la fusion dans la branche par défaut fait changer l'état ;
  - de préférence, la branche est créée depuis le work item ou nommée d'après lui.
- **GitHub Issues :** simplement `#<id>` de l'issue (pas de `AB#`) dans chaque message de commit et dans la
  description de chaque PR. Pour fermer l'issue à la fusion, la PR qui la termine écrit `Fixes #<id>`.
- **Sans work item ni issue :** l'agent ne commite pas sans lien. Il en crée un si l'option de la section 4
  le permet, sinon il demande à l'humain lequel utiliser.
- **Mémoire :** la consigne dit aussi à l'agent d'**inscrire cette règle dans sa propre mémoire et dans la
  mémoire du projet**, et d'**aviser l'utilisateur d'approuver ces modifications** (fichier de mémoire,
  `CLAUDE.md`, `AGENTS.md`), pour que l'agent la suive toujours, y compris dans une session lancée hors de
  Jarvis. Jarvis ne modifie pas lui-même la mémoire d'un outil tiers sans cette approbation.

### 4. Option « lien obligatoire » et synchronisation

- Option par projet, **désactivée par défaut** : tout sujet et toute tâche doivent avoir une issue ou un work
  item lié. Une tâche sans lien reste `pending` tant qu'elle n'en a pas.
- Avec l'option, Jarvis (ou l'orchestrateur du sujet) peut **créer**, **modifier** (titre, description,
  parent, état) et **suivre** les issues et work items de ses sujets et tâches. Il ne supprime jamais rien :
  un travail abandonné passe à *Removed* ou est fermé *not planned*.
- **Synchronisation dans les deux sens :**
  - un changement de statut d'une tâche Relais met à jour l'état lié selon la section 2 ;
  - un changement fait dans GitHub ou Azure Boards (fermeture, réouverture, nouveau titre) remonte à Relais.
    Par défaut, Jarvis interroge la source à intervalle régulier ; les webhooks GitHub et les *service hooks*
    d'Azure DevOps demandent une adresse joignable depuis Internet et passeront par le tunnel de l'ADR 0013,
    section 8.
  - En cas de conflit, l'état de la source de suivi gagne, et le fil du sujet le note.
- Classement selon l'ADR 0009 : créer ou modifier une issue ou un work item dans la source réglée pour le
  projet est une action **réversible et attendue** de l'option, sans confirmation. Toute autre destination
  (autre dépôt, autre projet Azure DevOps) demande une confirmation.

### 5. Accès technique

- Une tranche par source dans `Kazo.Relais` (`Tracking/GitHubIssues/`, `Tracking/AzureBoards/`), derrière un
  contrat commun du noyau de Relais. Jarvis ne voit que des messages du noyau, comme pour le reste de Relais.
- Accès par l'API REST de chaque service, ou par leurs serveurs MCP (le serveur MCP GitHub, le serveur MCP
  Azure DevOps de Microsoft) : le choix se fait en v2.

### 6. Ce que cet ADR ne décide pas

- Un contrôle en intégration continue qui refuse une PR sans lien (Azure Boards n'en fournit pas ; une action
  GitHub tierce existe, non vérifiée).
- Le suivi dans GitHub Projects (champs et vues de projet), Jira ou d'autres outils.
- L'affichage des liens dans l'éventail (ADR 0010) et sur le téléphone.

## Conséquences

- Rien n'est construit avant la v2. D'ici là, les ADR de l'étape 2 sur les sessions Claude Code gardent une
  place pour la consigne de la section 3 dans les réglages propres à Jarvis.
- La forme d'une tâche de l'ADR 0013 gagne, en v2, un champ « lien de suivi » (source, identifiant, URL).
- Les règles de classement des sujets de la spec Relais gagnent la clé `ab:<id>`.
- La qualité du lien repose sur la consigne et sur la mémoire de l'agent : sans contrôle en intégration
  continue, un agent qui l'oublie n'est pas bloqué.
