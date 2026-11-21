# Home Assistant : classement des appels et domotique de sécurité

Chantier `stage2/7-home-assistant` (US2.3). Décision :
[ADR 0018](adr/0018-mcp-client-home-assistant-and-security-devices.md), section 5. Le client MCP, la
connexion et la page des serveurs sont décrits dans [mcp.md](mcp.md) ; le point de décision dans
[tools.md](tools.md) et la demande de confirmation dans [confirmation.md](confirmation.md).

## Le problème

Le nom d'un outil de Home Assistant ne dit pas ce qu'il fait : `HassTurnOff` éteint une lampe **et
déverrouille une serrure**, et le serveur annonce tout ce qui n'est pas une lecture comme destructeur
(essai E14). Jarvis classe donc chaque appel par du code, **d'après les appareils visés**.

## Ce qui est construit

```
appel d'un outil ─► point de décision ─► ITool.ClassifyAsync ─► McpTool (tranche Mcp/Servers)
                                                                   │ serveur de type HomeAssistant
                                                                   ▼
                                    IToolServerClassifier (noyau) ◄─ HomeAssistantClassifier (tranche Mcp/HomeAssistant)
                                                                   │ lit GetLiveContext par la connexion du serveur
                                                                   ▼
                              Lecture · Effet courant · Sensible · Critique (clic exigé)
```

Un serveur se déclare de type « Home Assistant » à son ajout, sur la page « Serveurs MCP ». Les deux tranches
ne se connaissent pas : `Mcp/Servers` demande au noyau un `IToolServerClassifier` du type `home-assistant`.

## Classement d'un appel (`HomeAssistantClassifier`)

Les outils se nomment `<plateforme>__<intention>` (`intent__HassTurnOn`) ; l'intention décide. Dans l'ordre :

| Outil | Classe |
| --- | --- |
| Lecture : `GetLiveContext`, `GetDateTime`, `get_items`, ou annoncé en lecture seule | Lecture |
| Script : `script__…`, `intent_script__…` | Effet courant ; **Critique** si l'utilisateur l'a marqué « sécurité » |
| Outil qui vise des entités : son schéma a `name`, `area`, `floor`, `domain` ou `device_class` | **Critique** si une entité désignée est de sécurité, sinon Effet courant |
| Autre outil connu : `HassBroadcast`, `HassCancelAllTimers`, `HassListAddItem`, `HassListCompleteItem`, `HassListRemoveItem` | Effet courant |
| Tout autre outil | Sensible (confirmation à chaque appel) |

**Une entité est de sécurité** si son domaine est `lock`, `alarm_control_panel`, `valve` ou `siren`, si c'est
un `cover` de classe `garage`, `door` ou `gate`, ou si l'utilisateur l'a mise dans la liste « sécurité ».

### Entités désignées

Pour un outil qui vise des entités, Jarvis appelle lui-même `GetLiveContext` sur la connexion du serveur,
que l'outil soit activé pour le modèle ou non, et lit la liste des entités exposées (`LiveContext.Parse` :
noms et alias, domaine, pièces, classe d'appareil).

- Les filtres `domain` et `device_class` de l'appel écartent d'abord les entités qu'ils excluent.
- Un `name` qui est le nom ou un alias exact d'une entité (sans égard à la casse) et une `area` qui est une de
  ses pièces désignent ces entités.
- **Si rien ne correspond** (nom approximatif, alias absent de la liste, nom et pièce qui ne s'accordent pas),
  si l'appel donne un étage (`floor`, que la liste ne montre pas) ou s'il ne donne ni nom ni pièce : toutes
  les entités non exclues par les filtres comptent. Avec une serrure exposée et un appel sans filtre, c'est
  « Critique ».

### Refuser plutôt que deviner

- Si `GetLiveContext` ne répond pas, ou si aucune entité ne se lit dans sa réponse (format changé, rien
  d'exposé), le classement échoue : le point de décision refuse l'appel (`ClassifierFailed`) et l'écrit au
  journal des permissions. Jarvis ne classe jamais « Effet courant » faute d'information.
- La liste lue est gardée 5 secondes par serveur (réglable de 0 à 60 ; 0 la relit à chaque appel), pour ne
  pas la redemander à chaque appel d'un même tour.
- Une classe montée par l'utilisateur sur la page « Serveurs MCP » reste un plancher : le classement ne
  descend jamais dessous.
- « Critique » demande le clic sur le PC : la voix ne suffit pas (ADR 0009, ADR 0023).

## Liste « sécurité » et page « Home Assistant »

Section de réglages `Mcp.HomeAssistant` : `SecurityEntities` (noms d'entités), `SecurityScripts` (noms
d'outils de script), `ContextSeconds`. 200 entrées au plus par liste.

La page « Home Assistant » des paramètres rappelle ce qui est toujours de sécurité, offre un champ d'un nom
d'entité par ligne, la liste des scripts des serveurs connectés avec une case « sécurité » chacun, et la durée
de garde de la liste. Sur la page « Serveurs MCP », chaque script d'un serveur Home Assistant porte le rappel
qu'un script peut actionner n'importe quoi.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `ITool.ClassifyAsync` | La classe d'un appel quand il faut interroger un serveur pour la connaître ; par défaut, `Classify`. Le point de décision n'appelle que celle-ci |
| `IToolServerClassifier` (`Kernel/Tools/Servers`) | Le classement propre à un type de serveur : classe la plus basse d'un outil, classe d'un appel |
| `ToolServerCall` | Le serveur, l'outil, les arguments et de quoi poser une question au serveur |
| `ToolServerKinds` | Le nom du type `home-assistant` |

## Hôte Web

`SimulatedToolServers` : `https://maison.jarvis.example`, un Home Assistant avec
`homeassistant__GetLiveContext`, `intent__HassTurnOn`, `intent__HassTurnOff`, `script__soiree_cinema` et
`script__depart`. Il expose « Lampe du salon », « Serrure de l'entrée », « Porte du garage », « Store du
bureau » et « Prise du sapin ». Le modèle simulé appelle ces outils sur « allume … », « éteins … »,
« déverrouille … » (« tout dans la pièce … » vise une pièce) et « lance le script … ».

## Choix provisoires de ce chantier

L'ADR ne les tranche pas ; à confirmer par Benoit :

1. **La section de réglages se nomme `Mcp.HomeAssistant`**, pas `Mcp.HomeAssistant.Security` comme l'écrit
   l'ADR : une section de réglages a un seul point. La liste a **sa propre page** « Home Assistant », pas la
   page du serveur.
2. **Une entité se marque par son nom**, tapé à la main : MCP ne donne pas l'identifiant de l'entité. Une
   entité renommée dans Home Assistant doit être marquée de nouveau.
3. **La liste « sécurité » vaut pour tous les serveurs Home Assistant** configurés.
4. **Un appel qui donne un étage** est classé comme si sa cible n'était pas trouvée : `GetLiveContext` ne
   montre pas les étages.
5. **La liste des entités est gardée 5 secondes.** Une serrure exposée dans Home Assistant pendant ces
   5 secondes n'est pas encore vue.
6. **Le thermostat n'est pas de sécurité** par nature (l'ADR ne le nomme pas) : il se marque à la main.

## Non vérifié

- **Aucun appel n'est parti de Jarvis vers le vrai Home Assistant.** Le classement est testé contre une
  réponse de `GetLiveContext` écrite à la main d'après l'essai E14 ; le vrai format peut différer (dans ce
  cas, les appels sont refusés, pas permis).
- Rien n'a tourné sur Windows.

## Tests

- Unitaires : `HomeAssistantClassifierTests` (lecture de la réponse, classe la plus basse, lampe et serrure
  avec le même outil, pièce, cible introuvable, entité marquée, script marqué, outils connus, refus sans la
  liste, ancien nom de `GetLiveContext`, garde de la liste, réglages), `McpServerHubTests` (un serveur d'un
  type classé par son classement), `PermissionDecisionPointTests` (classe trouvée après une attente).
- Playwright (`HomeAssistantTests`) : lampe allumée sans question, serrure et pièce de la serrure avec clic
  exigé et rien d'exécuté avant, script sans question puis « Critique » une fois marqué, entité marquée.

## Vérifications sur Windows

À faire avec Benoit. **Ne jamais approuver une action sur une serrure, l'alarme, la porte de garage, le
thermostat ou un script : on vérifie la demande de clic, puis on refuse.**

1. Ajouter le serveur avec le type « Home Assistant » : `HassTurnOn` et `HassTurnOff` sont de classe « Effet
   courant », les scripts portent le rappel.
2. Activer `HassTurnOn` et `HassTurnOff` ; « allume le salon » et « turn on the living room » allument sans
   question (US2.3).
3. « Déverrouille la porte d'entrée » : fenêtre « Critique », la voix ne suffit pas ; **refuser**. Le journal
   des permissions montre le refus.
4. Un nom approximatif (« éteins le truc ») : « Critique » puisqu'une serrure est exposée ; refuser.
5. Page « Home Assistant » : les 21 scripts environ sont listés ; en marquer un, sans l'exécuter.
6. Couper le réseau puis demander d'allumer une lampe : l'appel est refusé, pas exécuté.
7. Comparer une vraie réponse de `GetLiveContext` au format attendu (noms, `domain`, `areas`,
   `device_class`).
