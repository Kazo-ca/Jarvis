# Agents : un agent par genre de tâche, le tri et le passage de la main

Chantier `stage2/17-agents`. Décision : [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 1 à 4,
8 et 9. Le tour complet est décrit dans [conversation.md](conversation.md), la boucle d'outils dans
[tools.md](tools.md), les pannes dans [faults.md](faults.md).

Ce chantier construit les agents, leurs fichiers, le tri par l'agent Jarvis et le choix de l'agent dans la
conversation. **Restent à construire** (chantiers suivants) : la mémoire de chaque agent (section 5), les projets,
leur mémoire et l'agent « Projet » (sections 6, 10 et 11), le choix de l'agent d'un serveur MCP sur sa page
(section 3), le mode « Appel » (section 12).

## Ce qui est construit

```
agents\<nom>.md (dossier de Jarvis) ─► IAgentFiles (noyau, mis en œuvre par l'hôte)
                                             │
                                             ▼
page « Agents » ◄── AgentCatalog (tranche Agents/Catalog) ──► registre des outils :
                         │   lit, relit, écrit les fichiers        hand_to_agent, return_to_jarvis
                         │   de départ, lève la panne
                         ▼
Conversation/Dialogue ─► IAgentCatalog (noyau) : Main · Find(nom) · ToolsOf(agent) · ReadHand(arguments)
   │
   ├─ demande ─► agent Jarvis (ses outils + hand_to_agent) ─► répond lui-même : un seul appel au modèle
   │                 └─ hand_to_agent(agent) ─► l'agent nommé (ses consignes, ses outils) ─► répond
   │                                                └─ return_to_jarvis ─► agent Jarvis, sans repasser la main
   └─ demande écrite, agent choisi dans la fenêtre ─► cet agent, sans l'agent Jarvis
```

Les tranches `Agents/Catalog` et `Conversation/Dialogue` ne se connaissent pas : le contrat `IAgentCatalog` du
noyau les relie. Sur un hôte sans `IAgentFiles`, il n'y a pas d'agent, et un tour donne au modèle tous les outils
activés, comme avant.

## Un fichier d'agent

`%USERPROFILE%\.kazo\jarvis\agents\<identité>.md`, à la manière d'un agent de Claude Code :

```
---
name: Domotique
description: Les lumières, les prises, les volets, les serrures, les scènes et les scripts de la maison.
tools: HassTurnOn, HassTurnOff
---

Avant d'agir sur un appareil dont le nom est incertain, lis les entités et prends le nom exact.
```

- **L'identité** est le nom du fichier sans son extension, en minuscules (`home-automation`). Elle ne change pas
  avec la langue. `jarvis` est l'agent Jarvis.
- `name` est le nom affiché, dit, et écrit par le modèle qui passe la main. `description` est la phrase qui
  sert au tri. Les deux sont obligatoires.
- `tools` : des noms d'outils séparés par des virgules, ou un `- nom` par ligne. Peut manquer.
- Le reste du fichier est ajouté au message système quand cet agent répond, après la personnalité et les règles
  fixes, avant la mémoire.
- Un fichier fait 256 Kio au plus.

| Fichier laissé de côté | Cause |
| --- | --- |
| `Unreadable` | le disque refuse la lecture, ou le fichier est trop gros |
| `NoHeader` | pas d'en-tête entre deux lignes `---` |
| `NoName`, `NoDescription` | l'en-tête n'a pas cette ligne |
| `NameTaken` | un autre fichier donne déjà ce nom |

Un fichier laissé de côté lève la panne `AgentUnreadable` (origine `Agents`), qui mène à la page « Agents » ; la
page nomme le fichier et la cause. La panne tombe dès que tous les fichiers se lisent. Les autres agents
continuent de répondre.

**Les fichiers sont relus quand ils changent** (`FileSystemWatcher`, les écritures d'un même enregistrement
regroupées sur 400 ms). Le changement vaut pour le tour suivant.

## Les agents de départ

Écrits au premier lancement, dans la langue de l'assistant :

| Identité | Nom (fr / en) | Outils nommés |
| --- | --- | --- |
| `jarvis` | Jarvis | `get_datetime`, `calculate`, `hand_off_to_claude`, `start_quiet_period` |
| `home-automation` | Domotique / Home | aucun : ceux des serveurs de type Home Assistant lui viennent d'eux-mêmes |
| `web` | Web | `web_search`, `fetch_url` |
| `files` | Fichiers / Files | `list_directory`, `read_file` |
| `claude-code` | Claude Code | `get_claude_sessions` |
| `memory` | Mémoire / Memory | `remember_fact`, `forget_fact`, `search_memory` |

L'agent « Projet » de l'ADR arrive avec les projets.

**Un fichier modifié par l'utilisateur n'est jamais écrasé.** Jarvis garde l'empreinte SHA-256 de chaque fichier
qu'il a écrit (section `Agents.Files` de `config.json`). Au démarrage :

- fichier absent et jamais écrit : il est écrit ;
- fichier présent, dont le texte a encore l'empreinte gardée : il suit ce que Jarvis fournit (nouvelle version,
  ou autre langue de l'assistant) ;
- tout autre cas (modifié, ou supprimé par l'utilisateur) : Jarvis n'y touche pas.

Si le fichier `jarvis.md` manque ou ne se lit pas, l'agent Jarvis fourni avec l'application répond, et la page le
dit.

## Les outils d'un agent (`AgentCatalog.ToolsOf`)

Un agent ne donne aucun droit : il choisit ce que le modèle **voit**. Chaque appel passe toujours par le point de
décision.

- Un agent voit les outils **activés** que son fichier nomme, et ceux que leur source lui donne
  (`ITool.Agent` : les outils d'un serveur Home Assistant vont à `home-automation`).
- **L'agent Jarvis** voit en plus tout outil activé qu'aucun autre agent ne réclame : les outils d'un serveur MCP
  générique, un outil ajouté par une version suivante. Rien n'est perdu.
- L'agent Jarvis reçoit `hand_to_agent` seulement si un autre agent a au moins un outil à lui. Sinon, rien à
  trier : un seul appel, avec tout ce qu'il a.
- Les autres agents reçoivent `return_to_jarvis`.
- Un agent ne peut pas appeler l'outil d'un autre : l'appel est refusé (« NotThisAgent ») sans aller plus loin.

## Le tri et la main (`ConversationEngine`)

- **L'agent Jarvis reçoit la demande.** Il n'y a pas d'appel de tri avant lui. Une demande simple coûte un appel
  au modèle ; une demande passée en coûte deux.
- `hand_to_agent(agent, reason)` : sa description porte une ligne par agent qui peut prendre la main (nom et
  description), jamais leurs outils ; `agent` n'accepte que ces noms. Le nom se compare sans la casse ni les
  accents, et l'identité vaut aussi.
- **La main passe :** la boucle de l'agent Jarvis s'arrête, et le tour continue avec l'agent nommé. Il reçoit
  l'historique et la demande telle que l'utilisateur l'a faite, pas les appels de l'agent précédent.
- **À la voix, Jarvis le dit :** ce que le modèle a écrit avant l'appel est dit ; s'il n'a rien écrit, Jarvis dit
  « Je passe la main à l'agent Web. »
- **Un nom inconnu est refusé**, comme un agent sans outil : le résultat nomme les agents connus, et l'agent
  Jarvis garde la main.
- `return_to_jarvis` : la demande revient à l'agent Jarvis, qui répond alors sans `hand_to_agent` : la main ne
  tourne pas en rond. Un tour fait donc au plus trois réponses du modèle, hors appels d'outils.
- **La fenêtre de conversation** a une liste « Agent » : « Jarvis choisit », ou un agent. Une demande **écrite** va
  alors droit à cet agent. Le choix tient jusqu'à ce qu'on le retire, et s'oublie à l'arrêt de Jarvis. Une
  demande **dite** va toujours à l'agent Jarvis.
- Chaque tour garde le nom de l'agent qui a répondu (`ConversationTurn.Agent`), affiché sous la réponse.
- Ne passent par aucun agent : « Demande à Claude… » et « Demande à Claude Code de… », reconnus par du code, la
  réponse à une demande de confirmation, et le tour d'annonce.

## La page « Agents »

Après la page « Mémoire ». Le dossier des fichiers, les fichiers laissés de côté avec leur cause, puis chaque
agent : son nom, son fichier, sa description, les outils que le modèle voit, ceux que le fichier nomme mais qui
sont coupés ou absents, et un bouton « Ouvrir le fichier » (l'éditeur que Windows a pour un `.md`).

## Choix provisoires de ce chantier

À confirmer par Benoit :

1. Les fichiers de départ sont écrits dans la langue de l'assistant, et suivent un changement de langue tant
   que l'utilisateur n'y a pas touché.
2. Les identités sont en anglais (`home-automation`, `files`, `memory`), les noms dans la langue.
3. Un agent de départ supprimé par l'utilisateur n'est pas recréé.
4. Un agent sans aucun outil activé ne reçoit pas la main.
5. Le choix de l'agent dans la fenêtre s'oublie à l'arrêt de Jarvis.
6. À la voix, la phrase fixe « Je passe la main à l'agent X » n'est dite que si le modèle n'a rien dit avant.
7. Les outils de la mémoire sont à l'agent Mémoire : « souviens-toi que… » coûte deux appels au modèle.
8. L'agent Claude Code a seulement `get_claude_sessions` ; lancer un travail reste « Demande à Claude Code de… ».

## Vérifications sur Windows

Rien de ce chantier n'a tourné sur Windows.

1. Au premier lancement, les six fichiers sont dans `%USERPROFILE%\.kazo\jarvis\agents`.
2. « Ouvrir le fichier » ouvre l'éditeur ; enregistrer une consigne change le tour suivant sans redémarrer.
3. Un fichier sans en-tête : panne nommée, page qui dit lequel, panne levée une fois corrigé.
4. **Délai avant le premier son** d'une demande simple, à comparer à celui d'avant les agents (ADR 0025,
   section 8) ; puis celui d'une demande passée (deux appels).
5. Le modèle local (`qwen3:8b`) passe-t-il la main au bon agent ? « Éteins la lumière Articulé », « cherche sur
   le web… », « souviens-toi que… ».
6. Taille de la demande de l'agent Jarvis, à lire dans les jetons du tour, contre les 1 757 jetons de la recette.
