# ADR 0018 : client MCP, Home Assistant et domotique de sécurité

## Statut

Proposé le 2026-11-20 (chantier `stage2/2-adrs`). Les décisions prises par Benoit après l'essai E14 sont dans
la section « Réponses de Benoit ».

- **Tranche les points laissés par :** [ADR 0009](0009-permission-and-confirmation-model.md) (section 2 :
  « la façon de reconnaître la domotique de sécurité » ; section 8 : les commandes Home Assistant passent par
  MCP).
- **Précise :** ADR 0009, section 2, « une écriture, une commande ou un script n'est jamais classé sous
  "Sensible" ». Ce « script » est un script exécuté sur le PC (ADR 0009, section 6). Un script Home
  Assistant est une action de domotique, classée par la section 5 de cet ADR.
- **Récits :** US2.2 et US2.3 du [document de conception](../plans/design.md).
- **Essai :** E14 ([résultats](../../../../spikes/e14-mcp-home-assistant/RESULTS.md)).
- **S'appuie sur :** [ADR 0017](0017-tools-classes-and-decision-point.md) (registre des outils, classe par
  appel, point de décision).

## Contexte

- Essai E14 : le paquet officiel `ModelContextProtocol` 2.4.0 parle au serveur MCP de Home Assistant en HTTP
  « streamable » (`/api/mcp`). 47 outils, tous utilisables tels quels comme `AIFunction`. Le serveur est sans
  état : après une coupure, le même client reprend sans rien refaire.
- Les outils `HassTurnOn` et `HassTurnOff` verrouillent et **déverrouillent** les serrures. Les annotations
  MCP marquent « destructif » tout ce qui n'est pas une lecture, la pause d'un lecteur comme une serrure.
  Chaque script exposé est un outil sans paramètre, dont le nom ne dit pas ce qu'il fait.
- MCP ne donne ni les étiquettes ni les zones de Home Assistant. `GetLiveContext` donne le domaine, la pièce
  et la classe d'appareil de chaque entité exposée.
- Chez Benoit, aucune serrure, alarme ni porte de garage n'est exposée à Assist aujourd'hui.
- La description d'un outil MCP est écrite par son serveur : c'est un texte qui entre dans le contexte du
  modèle.

## Décision

### 1. Client et transports

- Paquet `ModelContextProtocol` (2.4.0 à l'essai), référencé par `Kazo.Jarvis.Adapters` seulement. Le noyau
  porte un contrat « serveur d'outils » sans type du paquet.
- Deux façons d'ajouter un serveur (US2.2) :
  - **par adresse** : HTTP « streamable », avec repli sur SSE (détection automatique du paquet) ;
  - **par commande locale** : transport `stdio` ; le processus est lancé par Jarvis et fermé à « Quitter ».
- Section de réglages `Mcp.Servers` : nom, type (`generic` ou `home-assistant`), adresse ou commande et
  arguments, en-têtes, activé ou non. **Les jetons et les valeurs d'en-têtes vont dans le coffre**
  ([ADR 0004](0004-configuration-storage-and-secret-vault.md)) ; le réglage n'en garde que la référence.
- **Le type `home-assistant` est choisi par l'utilisateur** à l'ajout. Il n'est jamais déduit du nom que le
  serveur annonce : un serveur ne peut pas se donner lui-même des classes plus basses.

### 2. Connexion

- « Tester la connexion » nomme la cause d'un échec : serveur injoignable, jeton refusé, délai dépassé,
  commande introuvable, protocole non compris.
- Les serveurs activés se connectent en arrière-plan au démarrage. Un serveur injoignable **n'est pas une
  panne de l'assistant** : ses outils sont retirés de la liste donnée au modèle, la page le dit, et la
  connexion se réessaie (5 s, puis en doublant jusqu'à 5 minutes).
- Délai d'un appel : 30 s. Sur une erreur réseau, un nouvel essai, sans recréer de session (E14). Un second
  échec est rendu au modèle comme résultat de l'outil.
- Un serveur lancé par commande qui s'arrête est relancé au prochain appel, trois fois au plus par heure.

### 3. Outils d'un serveur

- Les outils entrent dans le registre de l'ADR 0017 avec l'origine `mcp:<serveur>`, **avec le nom, la
  description et le schéma du serveur, tels quels**. Deux serveurs qui donnent le même nom : le second est
  préfixé par le nom de son serveur.
- **Tout outil MCP est désactivé à sa découverte** (US2.2 : activation outil par outil). Un outil qui
  apparaît plus tard sur un serveur déjà configuré est désactivé lui aussi.
- **L'activation est liée à l'empreinte** du nom, de la description et du schéma. Si l'un change, l'outil
  repasse à « désactivé » jusqu'à ce que l'utilisateur le revoie : un serveur ne peut pas changer en silence
  ce qu'un outil approuvé dit faire.
- La page d'un serveur montre chaque outil avec sa description entière, sa classe et son interrupteur, et
  l'estimation de la taille de la liste activée (ADR 0017, section 4).
- Les invites (`prompts`) et les ressources d'un serveur ne sont pas utilisées à cette étape. Les demandes
  d'un serveur au client (`sampling`, `elicitation`) sont refusées.

### 4. Classe d'un outil MCP de type `generic`

| Ce que dit le serveur | Classe |
| --- | --- |
| Annotation `readOnly` | Lecture |
| Tout le reste | Sensible : confirmation à chaque appel |

- L'utilisateur peut monter la classe d'un outil (« Sensible », « Critique »), jamais la descendre : l'annotation
  d'un serveur est une déclaration, pas une preuve.
- L'annotation `readOnly` est crue pour la classe, mais le résultat reste une donnée.

### 5. Classe d'un outil du type `home-assistant`

Du code de Jarvis classe chaque appel, **après avoir trouvé les entités visées** (réponse n° 1).

1. **Lectures** (`GetLiveContext`, `GetDateTime`, `todo__get_items`, et tout outil annoté `readOnly`) :
   « Lecture ».
2. **Outil qui vise des entités** (il a les paramètres `name`, `area`, `floor`, `domain` ou `device_class` :
   `HassTurnOn`, `HassTurnOff`, `HassLightSet`, les commandes de lecteurs, d'aspirateur, et tout outil futur
   de la même forme) :
   - Jarvis lit `GetLiveContext` et cherche les entités que l'appel désigne ;
   - si l'une d'elles est **de sécurité**, l'appel est « Critique » ; sinon « Effet courant » ;
   - **si la cible ne se trouve pas** (un alias que `GetLiveContext` ne montre pas, un nom approximatif),
     l'appel prend la classe la plus haute parmi **toutes** les entités exposées que ses filtres `domain` et
     `device_class` n'excluent pas. Sans serrure ni alarme exposée, c'est « Effet courant » ; avec une
     serrure exposée et un appel sans filtre, c'est « Critique ».
3. **Scripts** (`script__…`, `intent_script__…`) : « Effet courant » par défaut (réponse n° 2). Un script
   marqué « sécurité » par l'utilisateur est « Critique ».
4. **Tout autre outil** sans cible reconnaissable : « Effet courant » s'il est dans la liste connue
   (`HassBroadcast`, `HassCancelAllTimers`, les trois outils de listes `todo`), sinon « Sensible ».

**Une entité est de sécurité** si :

- son domaine est `lock`, `alarm_control_panel`, `valve` ou `siren` ;
- ou son domaine est `cover` et sa classe d'appareil est `garage`, `door` ou `gate` ;
- ou l'utilisateur l'a mise dans la liste « sécurité ».

La liste « sécurité » (entités et scripts) est la section de réglages `Mcp.HomeAssistant.Security`, sur la
page du serveur. À l'activation d'un script, la page rappelle qu'un script peut actionner n'importe quoi, et
propose de le marquer.

Une action « Critique » demande le clic sur le PC : la voix ne suffit pas (ADR 0009, section 2).

### 6. Ce qui sort de la machine

- Un serveur MCP par adresse est une connexion sortante que l'utilisateur a réglée lui-même. L'écran d'ajout
  dit que les arguments de chaque appel partent vers cette adresse. Un serveur hors du réseau local l'affiche
  en clair.
- Aucun serveur n'est configuré par défaut.

### 7. Tranches

- `Mcp/Servers` : les serveurs, leur page, la connexion, l'activation des outils.
- `Mcp/HomeAssistant` : le classement de la section 5 et la liste « sécurité ».
- `Adapters` : le client du paquet, derrière le contrat « serveur d'outils » du noyau.
- Les deux tranches ne se référencent pas : `Mcp/HomeAssistant` inscrit son classement dans le registre pour
  les serveurs du type `home-assistant`, par un contrat du noyau.

## Réponses de Benoit

Questions posées dans la conversation le 2026-11-20, après l'essai E14. Réponse : « je suis d'accord avec tes
recommandations ».

1. **Comment Jarvis reconnaît-il la domotique de sécurité ?** D'après l'appareil visé (serrure, alarme, vanne,
   sirène, porte de garage ou portail exigent le clic), plus une liste que l'utilisateur complète.
2. **Les scripts Home Assistant demandent-ils une confirmation ?** Non par défaut. L'utilisateur marque
   « sécurité » ceux qui doivent exiger le clic.

## Options écartées

- **Classer d'après le nom de l'outil ou ses annotations.** `HassTurnOff` déverrouille une serrure, et tout
  est annoté « destructif » (E14).
- **Lire les étiquettes de Home Assistant par l'API WebSocket.** Possible (essai E9), mais une seconde source
  à tenir en accord avec la première ; la liste « sécurité » de Jarvis suffit à cette étape.
- **Scripts « Sensibles » par défaut.** Écarté par Benoit : confirmer « soirée Netflix » à chaque fois serait
  pénible.
- **Activer d'office les outils d'un serveur ajouté.** Contraire au refus par défaut, et 47 outils d'un coup
  dans un contexte de 4096 jetons.

## Conséquences

- **Risque accepté :** un script non marqué « sécurité » passe sans question, quoi qu'il fasse. La page le dit
  à l'activation de chaque script. La meilleure protection reste de ne pas exposer les serrures et l'alarme à
  Assist dans Home Assistant.
- Un outil MCP générique qui n'est pas une lecture demande une confirmation à chaque appel. C'est voulu pour
  la première version ; un accord durable par outil demanderait un nouvel ADR (ADR 0009, conséquences).
- Chaque appel qui vise des entités coûte une lecture de `GetLiveContext` (0,3 s à l'essai). Jarvis peut la
  garder quelques secondes en mémoire ; la durée se règle au chantier `stage2/7-home-assistant`.
- **Non vérifié :** le classement d'une vraie serrure (aucune n'est exposée chez Benoit) ; le choix du bon
  outil par le modèle local parmi beaucoup d'outils Home Assistant ; la place que prend la liste activée dans
  4096 jetons. Ces points se mesurent au chantier 7, avec un serveur de test pour les serrures.
