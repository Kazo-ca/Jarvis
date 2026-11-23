# Client MCP : serveurs d'outils

Chantier `stage2/6-mcp-client` (US2.2). Décision :
[ADR 0018](adr/0018-mcp-client-home-assistant-and-security-devices.md), sections 1 à 4, 6 et 7. Le classement
propre à Home Assistant (section 5) est décrit dans [home-assistant.md](home-assistant.md). Le registre des outils et le
point de décision sont décrits dans [tools.md](tools.md).

## Ce qui est construit

```
page « Serveurs MCP » ─► McpServerHub (tranche Mcp/Servers) ─► IToolServerConnector (noyau)
        │                      │  une session par serveur activé            │
        │                      │  outils ─► IToolRegistry, origine mcp:<serveur>, désactivés
        │                      │  en-têtes ◄─ coffre (mcp.<serveur>.header.<en-tête>)
   Mcp.Servers (config.json)   └─ Tools.Activation (interrupteurs)          ▼
                                              Adapters/Mcp/McpToolServerConnector (paquet ModelContextProtocol.Core)
                                              par adresse : HTTP « streamable », repli SSE ; par commande : stdio
```

Aucun serveur n'est configuré au départ. Un serveur injoignable n'est pas une panne de l'assistant : ses
outils quittent la liste donnée au modèle, et la page le dit.

## Contrat du noyau

`src/Kazo.Jarvis.Kernel/Tools/Servers/`, sans aucun type du paquet :

| Type | Rôle |
| --- | --- |
| `IToolServerConnector` | Ouvre une connexion et fait l'échange d'ouverture du protocole |
| `IToolServerConnection` | Nom annoncé, fin de la connexion (`Closed`), liste des outils, appel d'un outil |
| `ToolServerEndpoint` | Adresse, ou commande et arguments ; en-têtes avec leurs valeurs. Une classe, pas un `record` : aucun `ToString` généré ne peut imprimer un jeton |
| `ToolServerTool` | Nom, description, schéma, déclaration « lecture seule » |
| `ToolServerResult` | Le texte de la réponse, et si l'outil signale un échec |
| `ToolServerFailure`, `ToolServerException` | La cause nommée : injoignable, jeton refusé, délai dépassé, commande introuvable, protocole non compris, connexion terminée |

## Adaptateur

`McpToolServerConnector` utilise le paquet officiel `ModelContextProtocol.Core` 2.4.0 (version de l'essai
E14), référencé par `Kazo.Jarvis.Adapters` seulement. `Core` est le client et le protocole, sans les
extensions d'hébergement dont Jarvis ne se sert pas.

- Par adresse : `HttpClientTransport` en détection automatique (HTTP « streamable », puis SSE), un
  `HttpClient` par connexion. Par commande : `StdioClientTransport`, le processus s'arrête quand la connexion
  est fermée.
- Le client ne déclare aucune capacité : `sampling` et `elicitation` sont refusés par le protocole. Les
  invites et les ressources ne sont pas lues.
- Délai de l'échange d'ouverture : 20 s. Le délai d'un appel (30 s) est celui de la boucle d'outils.
- Une erreur de protocole pendant un appel (outil inconnu, arguments faux) est une réponse de l'outil pour le
  modèle, pas une connexion cassée.

## Tranche `Mcp/Servers`

### Réglages

Section `Mcp.Servers` : pour chaque serveur, `Name` (minuscules, chiffres, traits d'union, 30 au plus),
`Kind` (`Generic` ou `HomeAssistant`), `Address` ou `Command` et `Arguments`, `Headers` (les **noms**
seulement), `Enabled`, `Approved` (empreinte de chaque outil activé) et `Classes` (classes montées à la
main). 20 serveurs au plus.

La valeur d'un en-tête est dans le coffre, sous `mcp.<serveur>.header.<en-tête>`. Le champ « Jeton » de
l'ajout écrit l'en-tête `Authorization` avec `Bearer ` devant. Retirer un serveur efface ses secrets et
coupe les interrupteurs de ses outils.

### Connexion (`McpServerHub`, `McpServerSession`)

- Les serveurs activés se connectent en arrière-plan au démarrage, et à chaque changement des réglages.
- Par adresse, un échec se réessaie après 5 s, puis en doublant jusqu'à 5 minutes.
- Un appel qui échoue sur une erreur réseau est refait une fois, sur la même connexion. Un second échec est
  rendu au modèle ; le serveur passe à « injoignable », ses outils sont retirés et les essais reprennent.
- Par commande, un échec au lancement ne se réessaie pas tout seul (bouton « Reconnecter »). Un programme
  qui s'arrête **garde ses outils** et est relancé au prochain appel, trois fois par heure au plus.
- « Tester la connexion » ouvre sa propre connexion, compte les outils et la ferme. Elle nomme la cause d'un
  échec.
- À l'arrêt de Jarvis, toutes les connexions sont fermées et les programmes lancés sont arrêtés.

### Outils d'un serveur (`McpTool`)

- Nom, description et schéma du serveur, tels quels ; origine `mcp:<serveur>`. Un caractère qu'un modèle
  n'accepte pas dans un nom devient `_`. Un nom déjà pris est préfixé : `<serveur>__<outil>`.
- **Désactivé à la découverte**, comme tout outil qui apparaît plus tard.
- **Activation liée à l'empreinte** (SHA-256 du nom, de la description, du schéma et de la déclaration
  « lecture seule »). Quand l'utilisateur active un outil, sur cette page ou sur la page « Outils »,
  l'empreinte est gardée dans `Approved`. À chaque connexion, un outil activé dont l'empreinte a changé est
  désactivé **avant** d'entrer dans le registre, et la page le signale jusqu'à ce que l'utilisateur le
  réactive. Un interrupteur resté allumé sans empreinte gardée est coupé de la même façon.
- Classe : « Lecture » si le serveur déclare l'outil en lecture seule, sinon « Sensible » (confirmation à
  chaque appel). L'utilisateur peut la monter à « Sensible » ou « Critique », jamais la descendre : les
  réglages refusent toute autre valeur. Un serveur de type `HomeAssistant` a son propre classement, par
  appel ([home-assistant.md](home-assistant.md)).
- Un échec signalé par l'outil revient au modèle avec son texte. Une cause de connexion revient en une
  phrase qui nomme le serveur.
- `Discloses` dit où partent les arguments : l'adresse, ou le programme lancé.

### Page « Serveurs MCP »

Pour chaque serveur : type, état avec la cause, adresse ou commande, ce qui quitte la machine, la mention
« Hors du réseau local » quand l'adresse n'est ni locale ni privée, le nom que le serveur se donne,
« Connecter », « Tester la connexion », « Reconnecter », « Retirer », la valeur de chaque en-tête (champ de
secret), puis chaque outil avec sa description entière, sa classe et son interrupteur, et l'estimation en
jetons des outils activés. En bas, l'ajout par adresse ou par commande locale.

Depuis le chantier 18 (ADR 0025, section 3), chaque serveur générique a une liste « Agent » sur la page
(`McpServerOptions.Agent`) : ses outils vont à cet agent, l'agent Jarvis par défaut. Voir
[agents.md](agents.md#les-outils-dun-agent-agentcatalogtoolsof).

## Hôte Web

`SimulatedToolServers` : `https://mcp.jarvis.example` (`lookup_stock` en lecture seule, `send_message`),
`https://secure.jarvis.example` (exige `Authorization: Bearer bon-jeton`), `https://down.jarvis.example`
(injoignable), commande `demo-server` (`roll_dice`). Le bouton `#revise-tool-servers` fait changer la
description de `send_message`. Le modèle simulé appelle ces outils sur « cours de l'action … », « envoie le
message … » et « lance le dé ». L'élément `#tool-servers` montre les appels reçus.

## Choix provisoires de ce chantier

L'ADR ne les tranche pas ; à confirmer par Benoit :

1. *(Levé par le chantier 7.)* Le type `HomeAssistant` a maintenant son classement :
   [home-assistant.md](home-assistant.md).
2. **Un seul champ « Jeton » à l'ajout** (`Authorization: Bearer …`). Les autres en-têtes s'écrivent dans
   `config.json` (leurs noms) ; leur valeur se saisit ensuite sur la page.
3. **Pas de modification d'un serveur** sur la page : on le retire et on l'ajoute de nouveau.
4. **Pas de variables d'environnement** pour un serveur lancé par commande.
5. **Après un changement de jeton, il faut appuyer sur « Reconnecter »** : le coffre ne prévient pas.
6. **La déclaration « lecture seule » entre dans l'empreinte** : un serveur qui la change fait désactiver
   l'outil. C'est plus strict que l'ADR.
7. **Deux serveurs qui offrent le même nom** : celui qui se connecte le premier garde le nom simple. Si
   l'ordre change à un autre lancement, l'outil activé est coupé et doit être réactivé.
8. **Paquet `ModelContextProtocol.Core`** au lieu de `ModelContextProtocol` : même client, sans les
   extensions d'hébergement.

## Non vérifié

- **Aucun vrai serveur MCP n'a été joint depuis Jarvis.** L'adaptateur est testé contre un serveur du même
  paquet en mémoire, et contre des réponses HTTP écrites à la main. Le serveur de Home Assistant a été joint
  par l'essai E14, avec le même paquet.
- Un serveur lancé par commande sur Windows (`npx`, `uvx`, un `.cmd`) : seul l'échec « commande
  introuvable » est testé avec un vrai processus.
- Le repli sur SSE.

## Tests

- Unitaires : `McpServerHubTests` (rien au départ, outils désactivés, classes, empreinte, nom préfixé, jeton
  au coffre et jamais dans `config.json`, nouveaux essais, second échec, commande relancée trois fois par
  heure, arrêt), `McpToolServerConnectorTests` (liste, appel, échec d'outil, fin du serveur, commande
  introuvable, 401 et 403, hôte injoignable, page web à la place d'un serveur).
- Playwright (`McpServersTests`) : aucun serveur au départ, ajout par adresse et par commande, outils
  désactivés, appel d'un outil activé, confirmation avant un outil qui n'est pas une lecture, classe montée,
  outil changé par son serveur, test de connexion, jeton refusé puis remplacé.

## Vérifications sur Windows

À faire avec Benoit :

1. Ajouter le serveur de Home Assistant par son adresse (`…/api/mcp`) avec un jeton : « Connecté », 47 outils
   environ, tous désactivés. Le jeton n'est pas dans `config.json`.
2. Avec le type « Générique », activer `GetLiveContext` et `HassTurnOn` ; « allume le salon » demande une
   confirmation. Avec le type « Home Assistant », voir [home-assistant.md](home-assistant.md#vérifications-sur-windows). **Ne rien activer qui touche une serrure, l'alarme, la porte de
   garage, le thermostat ou un script.**
3. Couper le réseau : le serveur passe à « Injoignable », ses outils disparaissent de la page « Outils »,
   Jarvis reste en veille sans panne. Rétablir : il revient tout seul.
4. Un mauvais jeton : « le serveur a refusé le jeton ».
5. Un serveur par commande (par exemple `npx` avec un serveur de démonstration) : il démarre, ses outils
   apparaissent ; tuer son processus, puis appeler un outil : il redémarre. « Quitter » l'arrête.
6. Avec `qwen3:14b` : le modèle choisit le bon outil parmi ceux activés.
