# ADR 0025 : un agent par genre de tâche, ses consignes et ses mémoires

## Statut

Accepté par Benoit le 2026-11-22 (proposé le 2026-11-21, chantier `stage2/adr-0025-task-agents`), d'après ses
cinq réponses du 21 novembre pendant la recette de l'étape 2, ses réponses du même soir aux quatre points
ouverts, à l'agent Jarvis et au mode « Appel » (sections 9 à 12), et son « ok » du 22 novembre aux trois
derniers points (section 13).

- **Complète :** [ADR 0017](0017-tools-classes-and-decision-point.md) (les outils et le point de décision ne
  changent pas : un agent choisit *quels* outils le modèle voit, jamais ce qu'un outil a le droit de faire) et
  [ADR 0019](0019-conversation-memory.md) (la mémoire de conversation reste ; deux mémoires s'ajoutent).
- **S'appuie sur :** [ADR 0020](0020-hand-off-targets-and-spend-cap.md) (passage de main),
  [ADR 0021](0021-claude-code-sessions.md) (dossiers autorisés, sessions Claude Code),
  [ADR 0004](0004-configuration-storage-and-secret-vault.md) (dossier de Jarvis).
- **Construction :** après la recette de l'étape 2, comme premier chantier de la suite (réponse 1).

## Contexte

À la recette, « Éteins la lumière articulée » a donné un appel d'outil avec le nom « lumière articulée » pour
la lumière nommée « Articulé ». La correction immédiate est dans le code (`EntityNames`). Benoit a demandé plus
large : « on devrait avoir un agent pour chaque type de tâche. Quand on fait une demande, il évalue quel est le
genre de tâche, puis charge l'agent approprié. Ces instructions devraient être dans l'agent Domotique / Home
Assistant. » Puis : « il devrait y avoir une mémoire locale pour chaque agent et une autre, complémentaire, par
projet. »

Aujourd'hui, chaque tour envoie au modèle local une seule consigne et **tous** les outils activés. Avec un
modèle local, la lecture de cette demande est ce qui coûte : à la recette, 1 757 jetons ont pris plus de deux
minutes quand la carte graphique était pleine. Un agent qui n'apporte que ses outils raccourcit la demande.

## Décision

### 1. Un agent

Un agent est : un nom, une description d'une phrase (ce qui sert au tri), des consignes, et la liste des
outils que le modèle voit quand cet agent est chargé. Il ne donne aucun droit : la classe de chaque appel et
la confirmation restent celles de l'ADR 0017. Un outil désactivé dans la page « Outils » reste invisible,
même si un agent le nomme.

### 2. Des fichiers Markdown (réponse 3)

Chaque agent est un fichier `agents\<nom>.md` dans le dossier de Jarvis, à la manière des agents de Claude
Code : un en-tête (`name`, `description`, `tools`) puis les consignes en texte.

- Jarvis écrit les fichiers de départ au premier lancement. Un fichier modifié par l'utilisateur n'est jamais
  écrasé par une mise à jour.
- Les fichiers sont relus quand ils changent. Un fichier illisible est ignoré, et une panne le dit.
- Une page des paramètres liste les agents, leurs outils et ouvre le fichier.

### 3. Les agents de départ (réponse 4)

| Agent | Outils | Consignes propres |
| --- | --- | --- |
| Domotique | ceux des serveurs de type Home Assistant | lire les entités avant d'agir sur un nom incertain, ne jamais deviner une serrure |
| Web | `web_search`, `fetch_url` | citer la source |
| Fichiers | `list_directory`, `read_file` | rester dans les dossiers autorisés |
| Claude Code | lancement et suivi des sessions | résumer la demande avant de lancer |
| Mémoire | `remember_fact`, `forget_fact`, `search_memory` | |
| Projet | ceux qu'il faut pour travailler dans le dossier d'un projet : fichiers, sessions Claude Code | chargé avec le dossier et la mémoire du projet demandé (section 13, point 2) |
| Jarvis (général) | horloge, calcul, passage de main, les outils de Jarvis lui-même | la conversation courante ; fait le tri et décide de passer la main (sections 4 et 9) |

Les outils d'un serveur MCP générique vont à l'agent que l'utilisateur choisit sur la page du serveur ;
l'agent Jarvis par défaut.

### 4. Le tri (réponse 2, et section 13, point 1)

**L'agent Jarvis fait lui-même le tri.** Il reçoit la demande, avec ses propres outils et la liste des autres
agents (nom et description). Il répond lui-même à une demande simple : un seul appel au modèle. Pour le reste,
il passe la main : il nomme un agent, et le tour continue avec cet agent, ses consignes, ses outils et ses
mémoires. Aucun appel de tri silencieux ne le précède.

- Passer la main est un outil de l'agent Jarvis : le nom d'un agent connu, et le projet quand il y en a un
  (section 10). Un nom inconnu est refusé, et l'agent Jarvis garde la main.
- La demande de l'agent Jarvis reste courte : ses outils et une ligne par agent, jamais les outils des autres.
- **Dans la fenêtre de conversation, l'utilisateur peut choisir l'agent** : le tour va alors droit à cet agent,
  sans passer par l'agent Jarvis. Le choix vaut jusqu'à ce qu'il le retire.
- Une demande de confirmation en attente ne passe pas par le tri : la réponse appartient au tour qui a demandé
  (ADR 0023).
- La conversation affiche l'agent de chaque tour.

### 5. La mémoire de chaque agent

Chaque agent a sa mémoire locale : ce qu'il apprend en travaillant et qui ne sert qu'à lui (« Articulé est la
lampe du bureau, dans le salon »). Elle est envoyée seulement quand cet agent est chargé. Mêmes règles que
l'ADR 0019 : faits courts, tirés des seuls mots de l'utilisateur ou de ce qu'il confirme, visibles et
effaçables dans la page « Mémoire », jamais envoyés au cloud.

### 6. Les projets et leur mémoire (réponse 5)

Un projet se crée dans l'interface de Jarvis. C'est **un dossier local** et un nom. Il y aura aussi un projet
pour la domotique.

- Le dossier d'un projet peut être celui où Claude Code (ou une autre cible) travaille lors d'un passage de
  main : le projet devient alors l'endroit où la session est lancée.
- La mémoire du projet est **complémentaire** : elle s'ajoute à celle de l'agent quand le tour porte sur ce
  projet. Elle vit dans le dossier du projet, pour que la session externe puisse la lire.
- **Un dossier sans projet** : quand une session Claude (suivie par Jarvis, ADR 0021) fait une demande dans un
  dossier qui n'appartient à aucun projet, Jarvis affiche une demande : l'assigner à un projet existant, ou
  créer un nouveau projet. Jarvis ne choisit jamais à la place de l'utilisateur, et la demande n'a pas de délai
  (comme l'ADR 0023).
- Créer un projet sur un dossier ne l'ajoute pas aux dossiers autorisés sans le dire : la page le propose, et
  l'utilisateur confirme (ADR 0021).

### 7. Ce qui ne change pas

Le point de décision, les classes, la confirmation, le plafond de dépense, le journal des permissions. Le
tour d'annonce (ADR 0024) ne passe par aucun agent.

### 8. Conséquences

- Une demande simple garde un seul appel au modèle local. Une demande passée à un autre agent en fait deux.
  À mesurer sur le PC de Benoit : le délai avant le premier son ne doit pas dépasser celui d'aujourd'hui pour
  une demande simple.
- La demande envoyée est plus courte : seuls les outils de l'agent chargé y sont.
- Un mauvais tri donne un agent sans le bon outil : l'agent doit pouvoir dire « ce n'est pas pour moi », et
  la main revient alors à l'agent Jarvis.

### 9. L'agent Jarvis

Il y a un agent pour Jarvis lui-même, avec les outils de Jarvis. À la voix, **c'est toujours lui qui reçoit la
demande et qui décide de passer la main** à un autre agent. Il le dit : à « Dans Kazo Os, il nous reste quoi à
faire ? », il répond quelque chose comme « Ok, je vais vérifier cela. Je passe la main à l'agent qui s'occupe de
Kazo OS. »

- À la voix, on ne nomme pas un agent pour sauter l'agent Jarvis (point 4, réponse B). Dans la fenêtre de
  conversation, le choix de l'agent à la main reste possible (section 4).
- Hors du mode « Appel », la main revient à l'agent Jarvis à la fin du tour.

### 10. De quel projet parle un tour (point 1, réponse A)

En passant la main, l'agent Jarvis nomme l'agent **et le projet** en même temps ; le projet peut aussi être choisi dans la conversation, ou
nommé dans la phrase (« Dans Kazo Os, … »). Sans projet reconnu, seule la mémoire de l'agent est envoyée.

### 11. Où vivent la mémoire du projet et le passage de main (points 2 et 3, réponses A)

- La mémoire d'un projet est **un fichier Markdown lisible dans le dossier du projet**, que Claude Code lit
  aussi.
- Le passage de main vers Claude ou ChatGPT reste **un outil**, comme aujourd'hui (ADR 0020), pas un agent de
  plus.

### 12. Le mode « Appel »

Une conversation suivie, sur le téléphone ou dans l'application.

- **Il commence** quand l'utilisateur clique sur le bouton « Appeler », ou dit quelque chose comme « On doit
  discuter de tel projet » ou « de tel problème ».
- **Pendant l'appel, l'agent change et reste** : celui du projet ou du sujet garde la main d'un tour à l'autre,
  jusqu'à ce que l'utilisateur demande de revenir à Jarvis.
- **Pendant l'appel, Jarvis écoute la suite sans « Hey Jarvis »**, comme au téléphone (section 13, point 3).
  La coupure du micro et l'orbe gardent leurs règles (ADR 0005) : l'orbe montre l'écoute tant qu'elle dure.
- **Il se termine** quand l'utilisateur dit au revoir.

Les appels dans l'application Android sont l'étape 5 du document de conception ; cette section fixe seulement
ce que le mode « Appel » veut dire pour les agents.

### 13. Les trois derniers points (« ok » de Benoit du 2026-11-22, les trois recommandations)

1. **L'agent Jarvis fait lui-même le tri** : il répond aux demandes simples et passe la main pour le reste.
   Aucun appel de tri silencieux ne le précède (section 4).
2. **« L'agent qui s'occupe de Kazo OS » est un seul agent « Projet »**, chargé avec le dossier et la mémoire
   du projet demandé. Il n'y a pas de fichier d'agent par projet : créer un projet ne demande d'écrire aucun
   agent (section 3).
3. **Pendant un appel, Jarvis écoute la suite sans « Hey Jarvis »**, jusqu'à « au revoir » (section 12).
