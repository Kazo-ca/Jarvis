# ADR 0013 : préparer le module Relais (v2) : projets, sujets, tâches et orchestrateur

## Statut

Proposé le 2026-11-19 (chantier `stage1/relais-architecture-adr`). En attente de l'accord de Benoit.

- **Sources :** demande de Benoit du 2026-11-19 : brancher un MCP et des hooks dans Claude ou ChatGPT, faire
  parler les modèles entre eux sous les yeux de l'humain, sans coder tout de suite (probablement en v2), mais
  en s'assurant que l'architecture respecte la structure Projet (dépôt) → Sujet → Tâche → Sous-tâche avec un
  orchestrateur ; sa spécification « Module Relais dans le harness .NET » du même jour (gardée hors du dépôt,
  appelée ici **la spec**) ; décisions n° 21, 22 et 36 de la [conception](../plans/design.md) ; récits
  US2.9, US2.11, US2.12, US2.15 et US3.2 ; essais E2 et E8 ([synthèse](../spikes.md)).
- **Complète :** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (tranches et noyau),
  [ADR 0003](0003-hosts-and-native-services.md) (hôte Windows et serveur intégré),
  [ADR 0004](0004-configuration-storage-and-secret-vault.md) (stockage),
  [ADR 0009](0009-permission-and-confirmation-model.md) (permissions),
  [ADR 0010](0010-task-fan-and-orb-click.md) (éventail des tâches).
- **Ne remplace aucun ADR.** Les points où la v2 devra en remplacer un sont listés à la section 9.

## Contexte

- La spec fait de Relais une brique du « harness » : un bus où humains et IA se parlent (channels, messages,
  présence, questions, approbations), qui suit aussi le travail sous la forme **Projet → Sujet → Tâche →
  Sous-tâche**. Chaque sujet a un **orchestrateur** : une session d'agent qui suit tout le sujet. Les agents
  (Claude Code, Codex, ChatGPT, Claude.ai, un modèle local) s'y branchent par `/mcp` et `/hook`.
- Pour ce dépôt, **le harness est Kazo.Jarvis** : son hôte Windows est déjà une application WPF qui tourne
  en permanence (zone de notification, démarrage avec Windows).
- La spec a été écrite pour un harness .NET générique. Quatre de ses choix heurtent les ADR acceptés :
  - elle découpe Relais par couche (`Core`, `Data`, `Hosting`), alors que le code doit être en tranches
    verticales (ADR 0002) ;
  - elle range les vues du harness en WPF natif (`TreeView`), alors que dans Jarvis toute interface est un
    composant Razor d'une tranche et que l'hôte ne contient aucune fonctionnalité (ADR 0002, ADR 0003) ;
  - elle stocke tout dans PostgreSQL, alors que Jarvis range ses données dans SQLite (ADR 0004) ;
  - ChatGPT et Claude.ai web n'atteignent `/mcp` que par un tunnel depuis Internet, alors que la règle de
    sécurité n° 7 de la conception n'expose rien sur Internet. Benoit a tranché le 2026-11-19 (section 8).
- Les étapes 2 et 3 vont construire, avant Relais, des choses que Relais reprendra : la liste des sessions
  Claude Code par des hooks (US2.11, essai E2), les sessions lancées par Jarvis (US2.9, ADR 0009 section 7),
  les tâches en arrière-plan affichées dans l'éventail (ADR 0010). Si elles prennent une autre forme, la v2
  devra les réécrire.
- Rien n'est construit par cet ADR. Il fixe ce qui doit rester vrai d'ici là, un changement bon marché
  pour l'étape 1 et des contraintes pour les ADR de l'étape 2.

## Décision

### 1. Ce que Relais est pour Jarvis

- Relais est un **module à part** : le bus, ses données, ses points d'entrée `/mcp`, `/hook`, `/api` et son
  hub SignalR. Il ne connaît pas Jarvis.
- Jarvis est un **client** du bus, par l'interface `IRelaisClient` de la spec, et rien d'autre. Il peut
  aussi y être un agent : son modèle local peut être opérateur d'une tâche ou orchestrateur d'un sujet,
  sous un compte d'agent à lui.
- La hiérarchie, les règles des tâches et des sous-tâches, l'orchestrateur, les questions, l'attribution
  et les outils MCP sont **ceux de la spec**. Cet ADR ne les redit pas ; il dit comment Jarvis s'y prépare.

### 2. Projets de la solution

Le découpage de la spec est gardé là où il sert l'hébergement (ce qui tourne où), et remplacé par des
tranches verticales à l'intérieur :

| Projet | Contenu | Référence | Remplace dans la spec |
| --- | --- | --- | --- |
| `src/Kazo.Relais` | Toutes les tranches du bus, plus `AddRelais` / `MapRelais` et `InProcessRelaisClient` | `Kazo.Relais.Client`, ASP.NET Core, Npgsql | `Relais.Core`, `Relais.Data`, `Relais.Pki`, `Relais.GitHub`, `Relais.Hosting` |
| `src/Kazo.Relais.Client` | `IRelaisClient`, les enregistrements d'événements et de requêtes, `RemoteRelaisClient` | SignalR client seulement | `Relais.Client` |
| `src/Kazo.Relais.Host` | Exécutable du bus en processus à part (`UseWindowsService()` en mode service) | `Kazo.Relais` | `Relais.Host` |
| `tests/Kazo.Relais.Tests` | Tests des tranches et **tests de contrat** joués contre les deux clients | les trois | (PoC 3) |

- **Une tranche de Relais** est un dossier `Kazo.Relais/<Domaine>/<Tranche>/` qui contient sa demande et
  son traitement, son SQL, son outil MCP, sa route `/hook` ou `/api` et ses tests (même chemin). Exemples :
  `Tasks/StartTask/`, `Tasks/CreateSubtask/`, `Questions/AskQuestion/`, `Questions/RelayAnswer/`,
  `Orchestration/ClaimOrchestrator/`, `Subjects/ClosePrSubject/` (GitHub App). La CA interne est une tranche
  `Pki/` comme les autres.
- Les règles de l'ADR 0002 s'appliquent à l'intérieur de `Kazo.Relais` : une tranche n'en référence pas une
  autre, elles se parlent par messages, ce qui sert à trois tranches monte dans un petit noyau
  `Kazo.Relais/Kernel/` (identifiants, statuts, accès à la connexion).
- **Sens des dépendances :** `Kazo.Relais*` ne référence aucun projet `Kazo.Jarvis.*`. Côté Jarvis, seul
  `Kazo.Jarvis.Adapters` référence `Kazo.Relais.Client` ; l'hôte Windows référence `Kazo.Relais` seulement
  si le mode intégré est retenu (section 4). `Kernel` et `Features` ne voient jamais Relais.
- Les projets vivent **dans ce dépôt**, sous `src/` et `tests/`. Si Relais doit servir un autre harness
  (Kazo Bar, par exemple), il partira dans son propre dépôt ; un nouvel ADR le dira.
- Les tests d'architecture de l'ADR 0002 gagnent ces règles le jour où les projets sont créés.

### 3. Côté Jarvis : un contrat du noyau, une tranche par vue

- Le noyau de Jarvis reçoit son propre contrat du bus, réduit à ce que les tranches utilisent, et des
  messages (`Kernel/Messages/Relais/`) qui reprennent les événements de la spec (`TaskChanged`,
  `QuestionAsked`, `ApprovalRequested`, `OrchestratorChanged`, `ConnectionStateChanged`…). Il ne nomme
  aucun type de `Kazo.Relais.Client` (ADR 0002, section 4). La recopie est voulue : la frontière reste celle
  de l'ADR 0002.
- `Kazo.Jarvis.Adapters` le met en œuvre par `IRelaisClient`. Il lit le flux `SubscribeAsync` dans une tâche
  de fond et le traduit en messages du noyau, **regroupés par lots de 100 ms** (règle de la spec). L'hôte Web
  fournit un bus simulé et déterministe pour Playwright.
- Les vues sont des **tranches Razor** du domaine `Features/Relais/` : arbre Projet → Sujet → Tâche →
  Sous-tâche, fil d'un sujet ou d'une tâche, file des questions, file des approbations, orchestrateur du
  sujet. Pas de WPF natif. Cela répond, pour Jarvis, à la question ouverte de la spec sur le fil de
  discussion : `BlazorWebView`, déjà utilisé partout.
- Les mises à jour d'écran passent par le fil de rendu de Blazor (`InvokeAsync`), jamais par le fil de
  lecture du flux.

### 4. Modes d'hébergement

- Les trois modes de la spec restent possibles : **A** intégré à l'hôte Windows, **B** processus compagnon,
  **C** service Windows. Le choix se fait en v2, par les preuves de concept 1 et 2 de la spec.
- Ce qui pèse déjà, pour mémoire : Jarvis tourne en permanence, ce qui rend le mode A moins coûteux qu'ailleurs ;
  mais il charge aussi le mot d'éveil, Whisper et la synthèse dans son processus (ADR 0006), ce qui répond
  « oui » à la première question ouverte de la spec et pousse vers B ou C.
- La règle « un seul processus, une seule instance » de l'ADR 0003 vaut pour Jarvis. En mode B ou C,
  `Kazo.Relais.Host` est un autre programme, pas une seconde instance de Jarvis.
- **Le bus a son propre `WebApplication`**, distinct du serveur intégré de l'ADR 0003 (mobile, webhooks) :
  pas de conteneur d'injection partagé, son propre port, à l'écoute de `localhost` seulement par défaut. Le
  serveur intégré n'est donc pas bloqué par Relais, et Relais ne dépend pas de lui.
- **Préalable dès l'étape 1** (chantier `stage1/4-hosts`) : l'hôte Windows est bâti sur l'hôte générique
  .NET (`Host.CreateApplicationBuilder`). Ses services de fond démarrent après l'affichage de l'orbe, par
  `StartAsync` sans bloquer le fil d'interface ; à « Quitter », ils s'arrêtent par `StopAsync` avec un délai
  maximal de 10 secondes, puis l'application se ferme. Le mode A n'est alors qu'un service de fond de plus,
  et le serveur intégré de l'étape 2 ou 4 en profite aussi.

### 5. Stockage

- Relais garde **PostgreSQL** (prérequis de la spec), dans sa propre base. Ses données ne passent pas par le
  magasin de documents de l'ADR 0004 et ne vont pas dans `jarvis.db`.
- **Relais est optionnel.** Jarvis sans Relais ne demande ni PostgreSQL, ni port de plus : tout ce qui est
  décrit par les ADR 0001 à 0012 fonctionne seul.
- Sur le PC de Benoit, PostgreSQL peut tourner dans Docker, comme Ollama. Le choix se fait en v2.
- Les jetons d'agent et le jeton humain que Jarvis utilise côté client vont dans son coffre (ADR 0004). La
  clé de la CA interne reste à Relais.

### 6. Les tâches de Jarvis prennent dès l'étape 2 la forme de Relais

C'est le point qui évite une réécriture. Tout ce que les étapes 2 et 3 affichent comme « tâche en cours »
(sessions Claude Code, tâches déléguées, panneaux de l'éventail) utilise **la forme et le vocabulaire de
Relais**, même tant que Relais n'existe pas :

- **Vocabulaire.** Dans la doc et le code, *projet*, *sujet*, *tâche*, *sous-tâche*, *opérateur*,
  *orchestrateur* et *superviseur* ont le sens de la spec. Une **tâche autonome** de l'étape 3 (planifiée ou
  sur déclencheur) garde ce nom composé et n'est pas une tâche Relais ; ce qu'elle lance (une session
  Claude Code, par exemple) en est une.
- **Forme d'une tâche :** identifiant, tâche parente (vide pour une tâche, remplie pour une sous-tâche, un
  seul niveau), sujet, projet, titre, résumé, opérateur (compte et session), source (`harness`, `hook`,
  `mcp`, `orchestrator`), dates.
- **Statuts :** les neuf de la spec, et pas d'autres : `pending`, `working`, `paused`, `idle`,
  `waiting_answer`, `waiting_approval`, `blocked`, `done`, `abandoned`.
- **Clé de projet :** URL du remote Git du dossier, normalisée ; sinon nom du dossier. La liste des sessions
  (US2.11) les regroupe par projet, pas par chemin brut.
- **Clé de sujet :** les règles de classement de la spec (`pr:<n>`, `issue:<n>`, `branch:<nom>`, `_inbox`
  pour `main` ou sans Git). À l'étape 2, sans GitHub App, la clé `pr:<n>` n'est pas encore trouvée : la
  branche suffit.
- **Éventail (ADR 0010) :** un panneau montre une tâche ; ses sous-tâches s'affichent dans le panneau, pas
  comme des panneaux. Une tâche `waiting_answer` ou `waiting_approval` est **ambre**.
- Les ADR de l'étape 2 sur les sessions Claude Code et sur l'éventail reprennent cette section.

### 7. Hooks et MCP dès l'étape 2

- Le suivi des sessions Claude Code de l'étape 2 (US2.11, US2.12, essai E2) se fait avec **les mêmes
  hooks** que Relais : `SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `Stop`, et `SubagentStop`
  pour les sous-tâches. Ils appellent une route locale dont le nom suit la spec (`/hook/task.context`,
  `/hook/task.start`, `/hook/approval.requested`, `/hook/task.idle`). En v2, seul le destinataire change.
- **Sessions lancées par Jarvis (US2.9) :** la tâche est créée avant le lancement, et son identifiant et
  celui de la session sont passés par variables d'environnement (`RELAIS_TASK_ID`, `RELAIS_SESSION_ID` en
  v2). Les hooks voient l'identifiant et ne créent rien. Ces hooks vont dans les réglages Claude Code propres
  à Jarvis (ADR 0009, section 7).
- **Sessions lancées à la main :** leurs hooks vont dans les réglages de l'utilisateur. Leur installation, et
  son retrait, sont décrits par l'ADR de l'étape 2 sur les sessions Claude Code.
- Codex : `notify` vers `/hook/task.idle` et `start_task` par une consigne d'`AGENTS.md`, comme dans la spec.

### 8. Sécurité : ce que la spec et l'ADR 0009 ont en commun, et ce qui reste à trancher

- **Accord :** l'auteur d'un message (`user` ou `agent`) est fixé par le serveur, seul un humain décide une
  approbation, un orchestrateur ne le peut pas. C'est le principe n° 4 de l'ADR 0009.
- **Pour le modèle de Jarvis, un message du bus est une donnée** (principe n° 2 de l'ADR 0009). Un ordre
  d'un orchestrateur ou d'un autre agent peut orienter le travail d'une tâche ; il ne peut jamais accorder une
  permission ni changer la classe d'une action.
- **Une demande d'approbation de Relais s'affiche comme une demande de confirmation de l'ADR 0009** : panneau
  ambre de l'éventail, boutons près de l'orbe, clic sur le PC comme canal fort.
- **Réseau :** le bus écoute `localhost` par défaut ; le réseau local et le VPN sont une option.
- **Accès depuis Internet : un tunnel pour tout le bus** (choix de Benoit sur la carte du 2026-11-19, contre
  « `/mcp` seul » et « agents locaux seulement »). ChatGPT et Claude.ai web n'atteignent un MCP que depuis
  Internet ; le tunnel sert aussi des postes et des humains distants. Règles posées pour la v2 :
  - **fermé par défaut**, ouvert par un réglage qui dit clairement que le bus devient joignable depuis
    Internet ;
  - `/mcp`, `/hook`, `/api` et le hub SignalR passent tous par le tunnel ; **aucune route n'y est servie sans
    authentification** : jeton par compte d'agent, jeton ou authentification forte pour un humain, révocables
    un par un ;
  - le serveur intégré de Jarvis (mobile, webhooks, ADR 0003) **ne passe pas par ce tunnel** : il reste sur
    le réseau local et le VPN ;
  - le fournisseur du tunnel, le certificat présenté aux clients d'Internet et la limitation des requêtes
    sont fixés par l'ADR de la v2.
- **Risque accepté :** la surface d'attaque est bien plus grande qu'avec `/mcp` seul. Une fuite de jeton
  donne accès aux fils de tous les sujets que ce compte peut lire. La journalisation des accès et la
  révocation rapide font partie de la v2.

### 9. Ce que la v2 devra changer dans les ADR acceptés

Pour mémoire ; un nouvel ADR le fera au début de la série v2 :

- **ADR 0009, section 7, et US2.15 :** aujourd'hui, Jarvis ne répond jamais à une demande de permission d'une
  session qu'il n'a pas lancée. Avec le hook `PermissionRequest` de Relais, l'humain pourra la décider depuis
  Jarvis. C'est toujours un humain qui décide, mais la règle change.
- **Règle de sécurité n° 7 de la conception** (« n'écoute que sur le réseau local et le VPN ») : le bus
  Relais y fait exception quand son tunnel est ouvert (section 8). Le serveur intégré de Jarvis reste soumis
  à la règle.
- **ADR 0004 :** ajout de PostgreSQL comme base du module Relais.

### 10. Réponses de Benoit du 2026-11-19

- **Une réponse dite à voix haute** à une question `user` est publiée comme `user` : c'est l'humain qui parle.
  Elle ne vaut jamais accord pour une approbation, qui suit toujours l'ADR 0009 (canaux et classes).
- **L'orchestrateur d'un sujet est réglable**, et le réglage par défaut est **le modèle local de Jarvis**. Il
  s'exprime sous son compte d'agent, jamais comme `user`.

### 11. Ce que cet ADR ne décide pas

- Le mode d'hébergement retenu (preuves de concept 1 et 2 de la spec).
- La mise en veille d'un orchestrateur sans tâche active (question ouverte de la spec).
- L'affichage des sujets et des tâches sur le téléphone (ADR du mobile, étape 4).
- Les autres questions ouvertes de la spec.

## Conséquences

- Rien n'est construit avant la v2, sauf l'hôte générique de la section 4, au chantier `stage1/4-hosts`.
- Les ADR de l'étape 2 (sessions Claude Code, éventail, serveur intégré) ont trois contraintes de plus : la
  forme des tâches, les hooks et le port séparé du bus. En échange, la v2 ajoute Relais sans réécrire ce qui
  existe.
- Jarvis tient un second contrat du bus dans son noyau, recopié de `IRelaisClient`. C'est le prix de la
  frontière de l'ADR 0002.
- Le découpage de la spec en `Core` / `Data` / `Hosting` n'est pas repris : à sa place, des tranches dans
  `Kazo.Relais`. Les modes d'hébergement et le contrat `IRelaisClient` ne changent pas.
- PostgreSQL devient une dépendance de Relais, pas de Jarvis.
