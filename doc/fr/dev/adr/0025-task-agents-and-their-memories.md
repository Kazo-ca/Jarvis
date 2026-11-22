# ADR 0025 : un agent par genre de tâche, ses consignes et ses mémoires

## Statut

Proposé le 2026-11-21 (chantier `stage2/adr-0025-task-agents`), d'après les cinq réponses de Benoit du même
jour, pendant la recette de l'étape 2. **Pas encore accepté** : quatre points restent ouverts (section 9).

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
| Général | horloge, calcul, passage de main | la conversation courante |

Les outils d'un serveur MCP générique vont à l'agent que l'utilisateur choisit sur la page du serveur ;
« Général » par défaut.

### 4. Le tri (réponse 2)

Avant chaque demande, **un appel de tri** au modèle local : il reçoit la demande et la liste des agents (nom
et description), et rend un nom d'agent. Puis le tour se fait avec cet agent : ses consignes, ses outils, ses
mémoires.

- La demande de tri est courte et sans outil : c'est elle qui rend la seconde plus courte.
- Une réponse qui ne nomme aucun agent connu charge « Général ».
- **Dans la fenêtre de conversation, l'utilisateur peut choisir l'agent** : le tri est alors sauté. Le choix
  vaut jusqu'à ce qu'il le retire.
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

- Deux appels au modèle local par demande au lieu d'un, sauf agent choisi à la main. À mesurer sur le PC de
  Benoit : le délai avant le premier son ne doit pas dépasser celui d'aujourd'hui pour une demande simple.
- La demande envoyée est plus courte : seuls les outils de l'agent y sont.
- Un mauvais tri donne un agent sans le bon outil : l'agent doit pouvoir dire « ce n'est pas pour moi », et
  Jarvis refait alors le tour avec « Général ».

### 9. Points ouverts

1. Comment un tour sait-il de quel projet il parle : projet choisi dans la conversation, nommé à la voix, ou
   déduit par le tri ?
2. La mémoire du projet : un fichier Markdown lisible dans le dossier (que Claude Code lit aussi), ou la base
   de Jarvis ?
3. Le passage de main vers Claude ou ChatGPT : un agent de plus, ou un outil de l'agent « Général » comme
   aujourd'hui ?
4. À la voix, peut-on nommer l'agent (« Jarvis, domotique : éteins Articulé ») pour sauter le tri ?
