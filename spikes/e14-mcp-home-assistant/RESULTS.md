# Résultats E14 — 20 novembre 2026

**E14 : réussi, avec une mise en garde de sécurité.** Le client C# du paquet officiel `ModelContextProtocol`
**2.4.0** parle au serveur MCP de Home Assistant de Benoit en HTTP « streamable » : 47 outils listés avec leur
schéma, tous utilisables tels quels comme `AIFunction` de `Microsoft.Extensions.AI`, un appel en lecture en
0,3 s, une lumière allumée puis remise éteinte, une reprise sans rien faire après une coupure. **Mais** les
outils génériques `HassTurnOn` et `HassTurnOff` verrouillent et **déverrouillent** les serrures, et chaque
script exposé est un outil sans paramètre : la classe d'un appel ne peut pas se déduire du nom de l'outil.

## Connexion

| Transport | Adresse | Résultat |
| --- | --- | --- |
| HTTP « streamable » | `/api/mcp` | **OK**, 0,5 à 0,9 s (connexion et liste des outils) |
| SSE | `/api/mcp` | 405 Method Not Allowed |
| SSE (ancien) | `/mcp_server/sse` | OK, 0,4 s |
| Détection automatique | `/api/mcp` | OK (choisit HTTP « streamable ») |

- Authentification : en-tête `Authorization: Bearer <jeton longue durée>`, le même jeton que l'essai E9.
- Le serveur se présente comme `home-assistant` 1.28.1, protocole MCP `2025-11-25`, **sans identifiant de
  session** (sans état) ; capacités : outils, invites (`prompts`) et ressources, pas de journal ni de
  notifications (cohérent avec E9).

## Outils

47 outils, nommés `<domaine>__<intention>` : `homeassistant__GetLiveContext`, `llm__GetDateTime`,
`intent__HassTurnOn`, `intent__HassTurnOff`, `light__HassLightSet`, neuf commandes `media_player__…`, trois
`todo__…`, trois `vacuum__…`, `assist_satellite__HassBroadcast`, `intent__HassCancelAllTimers`, quatre
`intent_script__…` et **21 `script__…`** (un outil par script de la maison exposé à Assist, sans paramètre).

- Chaque `McpClientTool` **est** un `AIFunction` : `Name`, `Description` et `JsonSchema` viennent du serveur, et
  la liste entière se met telle quelle dans `ChatOptions.Tools` (47 sur 47). Aucune adaptation n'est à écrire
  pour la boucle d'outils de l'ADR 0007.
- Schéma de `intent__HassTurnOn` : `name`, `area`, `floor`, `domain` (liste), `device_class` (liste, avec entre
  autres `garage`, `door`, `gate`, `window`, `shutter`). Description du serveur : *« For locks, this performs a
  'lock' action »* ; pour `HassTurnOff` : *« performs an 'unlock' action »*.
- Annotations MCP : `GetLiveContext` et `GetDateTime` sont marqués `readOnly` ; **tous** les autres outils, y
  compris le simple « pause » d'un lecteur, sont `destructive`. Les annotations ne distinguent donc pas une
  lumière d'une serrure.

## Appels

| Appel | Résultat |
| --- | --- |
| `homeassistant__GetLiveContext` (lecture) | 0,29 s, 213 entités exposées : scènes 86, lecteurs 50, lumières 39, capteurs 15, interrupteurs 9, capteurs binaires 6, listes 3, booléens 3, aspirateur 1, sélection 1 ; 138 avec une pièce |
| `intent__HassTurnOn` `{name, domain: ["light"]}` sur une bande DEL de meuble télé éteinte | 0,34 s, `action_done`, l'identifiant d'entité est rendu (`light.…`) ; état « on » 3 s plus tard |
| `intent__HassTurnOff` sur la même | 0,31 s, `action_done` ; état « off » vérifié ensuite : **remise dans son état d'origine** |
| `llm__GetDateTime` | 0,10 à 0,15 s |

L'état lu par `GetLiveContext` juste après une action peut avoir une à deux secondes de retard : une lecture
faite 2 s après l'extinction disait encore « on », une autre 3 s après disait « off ».

## Reconnexion

Coupure simulée dans le `HttpMessageHandler` (toute requête échoue comme un hôte injoignable) :

1. appel avant la coupure : OK en 154 ms ;
2. appel pendant la coupure : `HttpRequestException` immédiate ;
3. **après la coupure, le même client** : OK en 135 ms, sans rien refaire ;
4. nouveau client : OK ;
5. connexion pendant la coupure : `HttpRequestException` à la création ;
6. connexion après la coupure : OK.

Le serveur étant sans état, il n'y a pas de session à refaire : il suffit de réessayer. Une connexion ratée au
démarrage se réessaie simplement plus tard.

## Reconnaître la domotique de sécurité depuis MCP

Ce que MCP expose, et ce que ça permet :

- **Domaine de l'entité** : oui, dans `GetLiveContext` (`domain: lock`, `alarm_control_panel`, `cover`…), avec
  l'état, les pièces (`areas`) et certains attributs, dont `device_class` quand l'entité en a une. Chez Benoit,
  **aucune serrure, alarme, porte de garage, volet, thermostat ni caméra n'est exposée à Assist** : la liste les
  écarte déjà. Seuls des capteurs de la centrale d'alarme (température, humidité) apparaissent.
- **Étiquettes (labels) et zones** : **non**, MCP ne les expose pas. Les pièces (`areas`) oui, mais une pièce
  ne dit rien de la sécurité.
- **Nom de l'outil** : non. `HassTurnOn` et `HassTurnOff` agissent sur tout domaine ; c'est l'argument
  (`domain`, `device_class`, `name`) et l'entité visée qui décident.
- **Scripts** : chaque script exposé est un outil `script__<id>` sans paramètre, dont la description n'est que
  son alias. Le nom ne dit pas ce qu'il fait (chez Benoit, un script de départ de la maison et un script
  d'animation de l'alarme sont exposés). Un script peut actionner n'importe quoi.

Conséquence pour l'ADR 0018 (proposée) : classer **après** résolution de l'entité, pas sur le nom de l'outil.

1. `HassTurnOn` / `HassTurnOff` / `HassLightSet` : Jarvis lit d'abord `GetLiveContext` (filtré par `name`,
   `area` ou `domain`), trouve l'entité visée et la classe d'après son domaine : `lock`, `alarm_control_panel`,
   `cover` de classe `garage`, `door` ou `gate` (et `valve`, `siren`) = **critique** ; le reste = effet courant.
   Un appel qui ne désigne pas une entité unique (une pièce entière) prend la classe la plus haute des entités
   de la pièce.
2. Tout outil `script__…` et `intent_script__…` = **sensible** par défaut (confirmation), avec une liste réglable
   pour en passer certains en effet courant ou en critique.
3. Une liste réglable d'entités et de scripts « sécurité » par l'utilisateur, puisque les étiquettes de Home
   Assistant ne passent pas par MCP. Si Jarvis veut les étiquettes, il les lit par l'API WebSocket
   (`config/entity_registry/list`), déjà utilisée pour les déclencheurs (E9).
4. Garder la règle de fond : la meilleure protection reste de ne pas exposer les serrures et l'alarme à Assist
   dans Home Assistant, ce qui est le cas chez Benoit.

## Non vérifié

- Une vraie coupure réseau ou un redémarrage de Home Assistant pendant un appel (la coupure est simulée côté
  client) ; le délai d'expiration réglé à 30 s n'a pas été atteint.
- Le classement d'une serrure ou d'une porte de garage réelle : aucune n'est exposée chez Benoit, et l'essai ne
  devait rien actionner de sensible.
- Les invites (`prompts`) et ressources annoncées par le serveur n'ont pas été lues.
- Les 47 outils donnés au modèle local (fiabilité du choix avec une longue liste).
