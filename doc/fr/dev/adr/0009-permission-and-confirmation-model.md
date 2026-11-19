# ADR 0009 : modèle de permissions et de confirmation

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 23, 39 et 40 de la [conception](../plans/design.md) et sa section « Sécurité et
  vie privée » ; récits US1.13, US2.10, US2.15, US3.10 et US3.15 ; essais E2, E8 et E9
  ([synthèse](../spikes.md)) ; décisions n° 5 et 6 des
  [essais](../spikes.md#décisions-prises-le-18-novembre-2026).
- **Complète :** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (un tour de conversation),
  [ADR 0007](0007-llm-providers-and-tool-format.md) (Jarvis garde la boucle d'outils).

## Contexte

- Jarvis entend en permanence, contrôle la maison et pourra lancer du code. La conception pose neuf règles
  de sécurité qui s'appliquent à toutes les étapes.
- Une confirmation vocale peut être captée par erreur ou prononcée par quelqu'un d'autre. La conception
  demande un clic pour les actions les plus risquées.
- La téléphonie 3CX est abandonnée : le NIP vocal prévu pour les appels n'a plus d'usage.
- Essai E8 : `claude -p` refuse et signale les écritures non autorisées. Mais **les hooks et les réglages
  personnels de l'utilisateur s'appliquent aux sessions lancées par Jarvis** : un hook de Benoit a intercepté
  une écriture avant le refus de permission, et la session a coûté 0,64 $ avec son contexte chargé.
- Essai E9 : les événements de Home Assistant arrivent par l'API WebSocket, pas par MCP.
- Essai E2 : les fichiers `sessions/*.key` et `ide/*.lock` de Claude Code contiennent des secrets.
- L'étape 1 n'a ni outil, ni Claude, ni tâche autonome. Le modèle est fixé maintenant pour que tout ce qui
  suit soit construit dessus.

## Décision

### 1. Quatre principes

1. **Le modèle propose, le code décide.** Un modèle, local ou cloud, ne peut que demander une action. La
   décision est prise par du code, hors du modèle, au même endroit pour tous les outils.
2. **Une donnée n'est jamais une instruction.** Le contenu d'un courriel, d'une page web, d'un fichier, du
   corps d'un webhook, de la sortie d'un script, d'un événement Home Assistant ou du résultat d'un outil
   est transmis comme donnée. Il ne peut ni accorder une permission, ni changer ce qu'une tâche a le droit
   de faire.
3. **Refus par défaut.** Sans accord explicite dans le délai, l'action n'a pas lieu.
4. **Seul un humain approuve.** Un accord vient d'un canal humain : la voix, un clic sur le PC, ou le
   téléphone jumelé. Un accord produit par un modèle, un outil ou une tâche est rejeté.

### 2. Classes d'actions

| Classe | Exemples | Accord demandé |
| --- | --- | --- |
| Lecture | Lire un fichier d'un dossier autorisé, donner l'heure, calculer, lire l'état de la maison | Aucun |
| Effet courant | Allumer une lumière, régler le volume | Aucun |
| Sensible | Écrire un fichier, lancer une commande, exécuter un script généré | Confirmation, à la voix ou par un clic |
| Critique | Domotique de sécurité : serrure, alarme, porte de garage | Confirmation par un **canal fort** : clic sur le PC ou téléphone jumelé. La voix ne suffit pas. |
| Interdite | Sortir des dossiers autorisés, lire les fichiers de secrets de Claude Code, s'approuver soi-même | Refus, journalisé |

- Chaque outil déclare sa classe. La classe de chaque outil, et la façon de reconnaître la domotique de
  sécurité, sont fixées par les ADR de l'étape 2.
- Une écriture, une commande ou un script n'est jamais classé sous « Sensible ».

### 3. Demande de confirmation

- Une demande porte : qui la fait (outil, session ou tâche), un **résumé** dans la langue de l'utilisateur,
  le **détail exact** (chemin, commande, différences), sa classe, les canaux acceptés et son délai.
- Jarvis lit le résumé à voix haute et l'affiche près de l'orbe avec les boutons Approuver et Refuser.
- **La réponse vocale est reconnue par du code, pas par le modèle** : une liste fermée de mots par langue
  (oui, non, confirme, annule et leurs équivalents anglais), valable seulement dans l'écoute que Jarvis
  ouvre juste après sa question. Toute autre réponse vaut refus.
- Un accord vaut pour **une** action. Il n'y a pas d'accord permanent, à une exception près : un script
  approuvé (section 6).
- Chaque demande et sa réponse sont journalisées, avec le canal utilisé.

### 4. Tâches autonomes

- Une tâche autonome ne peut jamais approuver elle-même une écriture, une commande ou un script (US3.15).
- Si elle a besoin d'un accord, elle se met en attente et prévient l'utilisateur. Elle ne reprend qu'avec
  l'accord d'un humain.

### 5. Dossiers autorisés

- Les outils de fichiers et Claude Code ne voient que les dossiers de la liste des paramètres.
- **Lecture seule par défaut.** Toute écriture ou commande est de classe « Sensible ».
- Un chemin est comparé après normalisation : chemin complet, liens et jonctions résolus, sans tenir compte
  de la casse.

### 6. Scripts générés

- Un script écrit par Jarvis ou par Claude reste « en attente » jusqu'à l'approbation de l'utilisateur.
- L'approbation est liée à l'empreinte du contenu : toute modification remet le script en attente
  (décision n° 40). Les scripts écrits par l'utilisateur sont approuvés d'office.

### 7. Sessions Claude Code lancées par Jarvis

- **Jarvis lance ses sessions avec ses propres réglages Claude Code.** Les réglages et les hooks personnels
  de l'utilisateur ne sont pas chargés : le comportement est prévisible, et le contexte plus petit.
- Jarvis pilote `claude -p` en flux JSON (E8) et choisit lui-même :
  - les sources de réglages (`--setting-sources`) et son fichier de réglages (`--settings`) ;
  - les serveurs MCP de la session (`--mcp-config` avec `--strict-mcp-config`) ;
  - les outils permis sans question (`--allowedTools`) et le mode de permission (`--permission-mode`) ;
  - les dossiers (`--add-dir`), à partir de la liste des dossiers autorisés ;
  - le plafond de la session (`--max-budget-usd`).
- Une demande de permission de Claude Code arrive à Jarvis par un outil MCP local
  (`--permission-prompt-tool`) et suit la section 3. Ce point n'a pas été essayé : il se vérifie par un
  essai au début de l'étape 2, et la ligne de commande exacte est fixée par l'ADR de l'étape 2.
- **Reste à décider à l'étape 2 :** si les réglages du projet (`project`) sont chargés. Ils apportent les
  consignes du dépôt, mais aussi ses hooks.
- Pour les sessions que Jarvis n'a pas lancées, il annonce seulement. Il ne répond jamais à leur place
  (US2.15).
- Jarvis ne lit jamais `sessions/*.key` ni `ide/*.lock`.

### 8. Home Assistant

- Les commandes passent par MCP et suivent les classes de la section 2.
- Les déclencheurs passent par l'API WebSocket (`subscribe_trigger`), en lecture seule. Le contenu d'un
  événement est une donnée (principe n° 2).
- Les deux canaux utilisent la même adresse et le même jeton, rangé dans le coffre
  ([ADR 0004](0004-configuration-storage-and-secret-vault.md)).

### 9. Ce qui sort de la machine

- Le mot d'éveil, la reconnaissance et la synthèse locale tournent sur le PC.
- Un texte ou un son ne quitte la machine que par un choix explicite de l'utilisateur : une voix cloud, un
  fournisseur cloud, un passage de main. L'écran où se fait ce choix le dit.
- Micro coupé (US1.13) : le flux de capture est fermé, aucun son n'est capté.
- Le plafond de dépense et l'ouverture du serveur intégré sont décrits par les ADR des étapes 2 à 4.

### 10. Ce que l'étape 1 construit

- La coupure du micro, le coffre et l'avertissement des voix cloud.
- Le point de décision des permissions est construit avec son premier outil, à l'étape 2. Rien n'est écrit
  d'avance.

## Conséquences

- Une injection d'instructions dans un courriel ou une page web peut tromper le modèle, mais pas obtenir une
  écriture, une commande ou une action critique : l'accord ne dépend pas du modèle.
- Les actions sensibles demandent un geste à chaque fois. C'est voulu pour la première version ; un accord
  durable par dossier ou par outil demanderait un nouvel ADR.
- Le NIP vocal de la décision n° 12 n'est pas repris, puisque la téléphonie est abandonnée.
- Avant l'étape 4, le seul canal fort est le clic sur le PC. Une action critique ne peut donc pas être
  approuvée à distance.
- Les sessions Claude Code lancées par Jarvis ne profitent pas des hooks personnels de Benoit. S'il veut
  certains d'entre eux, ils se recopient dans les réglages de Jarvis.
- Benoit a choisi de n'exiger un clic que pour la domotique de sécurité (question posée dans la conversation,
  réponse du 2026-11-19).
