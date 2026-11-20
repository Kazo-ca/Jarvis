# Plan de la série de l'étape 2 : outils, MCP, passage de main et sessions Claude

2026-11-20 · **Validé par Benoit le 2026-11-20** (chantier `stage1/stage-2-plan`). Ses réponses sont à la fin.

## But

À la fin de cette série, Jarvis agit : il contrôle la maison par Home Assistant, confie les tâches lourdes à
Claude et annonce où en sont les sessions Claude du PC. Ce sont les récits US2.1 à US2.15 du
[document de conception](design.md#étape-2--outils-mcp-passage-de-main-et-sessions-claude), plus l'éventail
des tâches de l'[ADR 0010](../adr/0010-task-fan-and-orb-click.md).

Contrairement au plan de l'étape 1, celui-ci **commence par des décisions** : les ADR acceptés laissent
exprès plusieurs points à l'étape 2 (classe de chaque outil, ligne de commande de Claude Code, hooks, plafond
de dépense). Les chantiers 1 et 2 les règlent avant toute ligne de code.

## Ce qui est déjà décidé

| Sujet | Décidé par | En une phrase |
| --- | --- | --- |
| Format des outils, boucle d'outils gardée par Jarvis, analyseur tolérant | [ADR 0007](../adr/0007-llm-providers-and-tool-format.md) | Noms en anglais, descriptions dans la langue de l'assistant, chaque appel passe par les permissions et s'affiche |
| Classes d'actions, confirmation, dossiers autorisés, sessions lancées par Jarvis | [ADR 0009](../adr/0009-permission-and-confirmation-model.md) | Le modèle propose, le code décide ; refus par défaut ; seul un humain approuve |
| Éventail des tâches, double-clic sur l'orbe | [ADR 0010](../adr/0010-task-fan-and-orb-click.md) | Se construit après l'essai E11 |
| Forme des tâches, neuf statuts, clés de projet et de sujet, hooks et routes `/hook/…` | [ADR 0013](../adr/0013-relais-readiness.md), sections 6 et 7 | Tout ce qui s'affiche comme « tâche » prend la forme de Relais |
| Modèle local `qwen3:14b` sans réflexion, 42 appels d'outils sur 42 en français | Essai E7 | L'anglais reste à vérifier |
| Registre des sessions Claude Code, Claude Desktop compris | Essais E2 et E3 | `~/.claude/sessions/<pid>.json`, lu en 73 ms |
| `claude -p` en flux JSON depuis C# | Essai E8 | La demande de permission par outil MCP n'a pas été essayée |

## Branches et fusion

- Série `stage2`, branche de base `stage2/base`, partie de `main` une fois `stage1/base` fusionné dans `main`
  (réponse n° 1 plus bas).
- Un chantier par branche `stage2/<n>-<sujet>`, une PR par chantier vers `stage2/base`.
- Un agent fusionne lui-même sa PR dans `stage2/base` quand la construction et les tests sont verts. Seul
  Benoit fusionne dans `main`.
- Un ADR n'est fusionné qu'une fois accepté par Benoit.

## Ce que chaque chantier doit livrer

Les huit points du [plan de l'étape 1](stage-1.md#ce-que-chaque-chantier-doit-livrer) valent tels quels.
S'y ajoutent, pour cette série :

9. Tout outil déclare sa classe (ADR 0009) et a un test qui prouve le refus par défaut.
10. Rien ne sort de la machine sans réglage explicite : un chantier qui ajoute une connexion sortante
    (recherche web, fournisseur cloud, serveur MCP distant) l'écrit dans l'écran où elle se règle et dans la
    liste de la recette.
11. Tout ce qui s'affiche comme tâche utilise la forme et les statuts de l'ADR 0013, section 6.

## Essais et ADR à faire d'abord

### Essais (chantier 1, sur le PC de Benoit)

| Essai | Bloque | Ce qu'il doit prouver |
| --- | --- | --- |
| E11 | Chantier 14 | Coût et fluidité de la fenêtre transparente de l'éventail, passage des clics, confort du simple clic retardé (ADR 0010, section 7). |
| E12 | Chantiers 11 et 12 | Une demande de permission de `claude -p` arrive à Jarvis par un outil MCP local (`--permission-prompt-tool`) et sa réponse est respectée ; les hooks `SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `Stop` et `SubagentStop` appellent une route locale ; ce que chargent `--setting-sources` (réglages, consignes du dépôt, hooks). |
| E13 | Chantier 3 | Le protocole de l'essai E7 rejoué avec les descriptions d'outils en anglais, et avec l'outil « passer la main » dans les deux langues. |
| E14 | Chantiers 6 et 7 | Le client MCP en C# (paquet officiel `ModelContextProtocol`) parle au serveur MCP de Home Assistant de Benoit : liste des outils, un appel, la reconnexion. Dit aussi comment reconnaître la domotique de sécurité (domaine de l'entité, étiquette, liste réglable). |

### ADR (chantier 2)

| ADR | Sujet | Tranche les points laissés par |
| --- | --- | --- |
| 0017 | Outils de base, classe de chaque outil, point de décision des permissions et affichage des appels | ADR 0007 (section 3), ADR 0009 (sections 2 et 10) |
| 0018 | Client MCP : transports, configuration, activation outil par outil, Home Assistant et domotique de sécurité | ADR 0009 (sections 2 et 8) |
| 0019 | Mémoire de conversation : ce qui est gardé, résumé automatique, page pour voir et effacer | ADR 0004, ADR 0007 (section 6) |
| 0020 | Passage de main, cibles et plafond de dépense | ADR 0007 (sections 1 et 7), ADR 0009 (section 9) |
| 0021 | Sessions Claude Code : ligne de commande exacte, réglages chargés, hooks et leur installation, serveur local des routes `/hook/…`, annonces | ADR 0009 (section 7), ADR 0013 (sections 6 et 7) |

Les numéros sont ceux qui suivent l'ADR 0016 ; ils se confirment à l'écriture. Si l'essai E11 échoue, un
ADR de plus dit quoi changer dans l'éventail (ADR 0010, section 7).

## Vue d'ensemble

| N° | Chantier (branche `stage2/…`) | Récits | ADR | Dépend de | Taille |
| --- | --- | --- | --- | --- | --- |
| 1 | `1-spikes` : essais E11 à E14 | — | 0010, 0009 | — | M |
| 2 | `2-adrs` : ADR 0017 à 0021 | — | — | 1 | M |
| 3 | `3-tool-loop` : boucle d'outils, point de décision, appels affichés, heure et calcul | US2.1 (début) | 0017 | 2 | L |
| 4 | `4-confirmation` : demande de confirmation à la voix et près de l'orbe, journal | US2.10 (socle) | 0009, 0017 | 3 | L |
| 5 | `5-basic-tools` : fichiers et dossiers autorisés, recherche web, requête HTTP | US2.1 | 0017 | 4 | L |
| 6 | `6-mcp-client` : serveurs MCP, liste et activation des outils | US2.2 | 0018 | 4 | L |
| 7 | `7-home-assistant` : Home Assistant par MCP, domotique de sécurité | US2.3 | 0018 | 6 | M |
| 8 | `8-memory` : mémoire persistante, résumé, page de la mémoire | US2.4 | 0019 | 3 | M |
| 9 | `9-hand-off` : outil « passer la main », « demande à Claude », cibles, qui répond | US2.5, US2.6, US2.7 | 0020 | 3 | L |
| 10 | `10-spend-cap` : plafond mensuel et compteur | US2.8 | 0020 | 9 | M |
| 11 | `11-tasks-and-sessions` : forme des tâches, serveur local des hooks, liste des sessions | US2.11, US2.14 | 0021, 0013 | 2 | L |
| 12 | `12-claude-code-tasks` : lancer une session à la voix, permissions de ses écritures | US2.9, US2.10, US2.15 | 0021, 0009 | 4, 9, 11 | L |
| 13 | `13-session-announcements` : annonces par dossier, heures de silence, résumé d'une session | US2.12, US2.13 | 0021 | 11 | M |
| 14 | `14-task-fan` : éventail des tâches et double-clic sur l'orbe | — | 0010, 0013 | 1, 11 | L |
| 15 | `15-acceptance` : recette de l'étape sur le PC de Benoit | toutes | — | tous | S |

Tailles : S, quelques heures ; M, une journée ; L, plus d'une journée. Ce sont des ordres de grandeur.

Indépendants une fois le chantier 4 fait : 5 (outils de base), 6 et 7 (MCP), 8 (mémoire), 9 et 10 (passage
de main). Le chantier 11 ne dépend que des ADR et peut avancer dès le chantier 2.

```
1 ─ 2 ─┬─ 3 ─┬─ 4 ─┬─ 5 ───────────┐
       │     │     ├─ 6 ─ 7 ───────┤
       │     │     └─────┐         │
       │     ├─ 8 ───────┼─────────┤
       │     └─ 9 ─┬─ 10 ┼─────────┼─ 15
       │           └─────┼─ 12 ────┤
       └─ 11 ─┬──────────┘         │
              ├─ 13 ───────────────┤
              └─ 14 ───────────────┘
```

## Détail des chantiers

### 1. Essais (`stage2/1-spikes`)

- E11 à E14, chacun dans `spikes/<nom>/` avec son `README.md` et son `RESULTS.md`.
- Synthèse ajoutée à [spikes.md](../spikes.md), avec les décisions qui en sortent.
- Tout se fait sur le PC de Benoit (fenêtre WPF, Claude Code, Ollama, Home Assistant), par un agent en
  contrôle à distance. Benoit n'a rien à dire ni à écouter.

### 2. ADR (`stage2/2-adrs`)

- ADR 0017 à 0021, en français puis en anglais, une PR par ADR ou par paire d'ADR liés.
- Chaque question ouverte est posée à Benoit dans la conversation, avec ses choix et une recommandation.
- La mémoire du projet et l'index des ADR suivent.

### 3. Boucle d'outils (`stage2/3-tool-loop`)

- Contrats du noyau : un outil (nom, description par langue, schéma, classe), le registre des outils, le
  point de décision des permissions.
- La tranche `Conversation/Dialogue` donne les outils au modèle, exécute les appels un par un par le point
  de décision et renvoie les résultats comme données (ADR 0007, sections 2 et 3).
- Chaque appel et son résultat apparaissent dans la fenêtre de conversation et sont enregistrés avec le tour.
- Deux premiers outils de classe « Lecture » : date et heure, calcul.
- L'orbe reste en réflexion pendant un appel ; une interruption annule l'appel en cours.

### 4. Confirmation (`stage2/4-confirmation`)

- La demande de confirmation de l'ADR 0009, section 3 : résumé dit à voix haute, détail exact affiché près
  de l'orbe, boutons Approuver et Refuser, délai, refus par défaut.
- Réponse vocale reconnue par du code : liste fermée de mots par langue, dans la seule écoute ouverte après
  la question.
- Canal fort pour la classe « Critique » : le clic seulement à cette étape (le téléphone arrive à l'étape 4).
- Journal des demandes et des réponses, avec le canal.
- Un outil factice de classe « Sensible » dans l'hôte Web prouve le parcours par Playwright.

### 5. Outils de base (`stage2/5-basic-tools`)

- Lecture de fichiers dans les dossiers autorisés : page « Dossiers autorisés », chemins normalisés (liens et
  jonctions résolus), refus journalisé hors de la liste.
- Recherche web et requête HTTP, derrière des contrats ; le premier fournisseur de recherche est une API avec
  clé (réponse n° 2). Les deux sont désactivés tant que l'utilisateur ne les a pas activés.
- Le contenu d'une page ou d'un fichier revient au modèle comme donnée, tronqué à une taille réglable.

### 6. Client MCP (`stage2/6-mcp-client`)

- Ajout d'un serveur par adresse ou par commande locale, test de connexion avec cause nommée, liste des
  outils exposés, activation outil par outil (US2.2).
- Un outil MCP entre dans le registre du chantier 3 avec une classe : celle que l'ADR 0018 fixe par défaut,
  réglable par l'utilisateur vers plus strict seulement.
- Jetons et en-têtes dans le coffre.

### 7. Home Assistant (`stage2/7-home-assistant`)

- Le serveur MCP de Home Assistant se configure comme les autres ; « Allume le salon » et « turn on the
  living room » fonctionnent (US2.3).
- Domotique de sécurité (serrure, alarme, porte de garage) reconnue selon l'ADR 0018 et classée « Critique » :
  clic exigé, la voix ne suffit pas.

### 8. Mémoire (`stage2/8-memory`)

- Mémoire persistante et résumé automatique des anciens échanges selon l'ADR 0019 ; le résumé entre dans le
  message système à la place des tours qu'il remplace.
- Page « Mémoire » : voir ce que Jarvis retient, effacer une entrée, tout effacer.

### 9. Passage de main (`stage2/9-hand-off`)

- Outil `hand_off_to_claude` avec un motif (US2.5) ; « demande à Claude » et « ask Claude » reconnus par du
  code et envoyés directement à la cible (US2.6).
- Page des cibles : une pour les tâches avec fichiers ou étapes (Claude Code, branchée au chantier 12), une
  pour les questions difficiles (fournisseur `anthropic` ou `openai`) (US2.7).
- L'orbe et la conversation disent qui répond. L'écran qui active une cible cloud dit ce qui sort de la machine.

### 10. Plafond de dépense (`stage2/10-spend-cap`)

- Prix par modèle réglables, coût calculé à partir des jetons déjà enregistrés à chaque réponse (ADR 0007,
  section 7), compteur mensuel visible.
- Plafond atteint : Jarvis le dit et reste en local (US2.8). Le plafond de chaque session Claude Code
  (`--max-budget-usd`) en découle.

### 11. Tâches et sessions (`stage2/11-tasks-and-sessions`)

- Forme d'une tâche et ses neuf statuts dans le noyau (ADR 0013, section 6), stockée dans `jarvis.db`.
- Serveur local, à l'écoute de `localhost` seulement, pour les routes `/hook/…` ; installation et retrait des
  hooks dans les réglages Claude Code de l'utilisateur, avec son accord, selon l'ADR 0021.
- Lecture du registre des sessions (essais E2 et E3) : liste regroupée par projet, avec dossier, état et
  dernière activité, sessions lancées à la main et sessions de Claude Desktop comprises (US2.11, US2.14).
- Jarvis ne lit jamais `sessions/*.key` ni `ide/*.lock` ; un test le prouve.

### 12. Tâches Claude Code (`stage2/12-claude-code-tasks`)

- « Demande à Claude de… » sur un dossier autorisé : Jarvis choisit ou demande le dossier, crée la tâche,
  lance `claude -p` avec ses propres réglages et passe à « tâche déléguée » (US2.9).
- Lecture seule par défaut ; toute écriture ou commande arrive par l'outil de permission et suit le chantier 4
  (US2.10, US2.15). Pour les sessions que Jarvis n'a pas lancées, il annonce seulement.
- Fin de session : résumé dit à voix haute, coût ajouté au compteur du chantier 10.

### 13. Annonces (`stage2/13-session-announcements`)

- Annonce quand une session se termine ou attend une permission, réglable par dossier, muette pendant les
  heures de silence (US2.12).
- « Où en est Claude sur Kazo Bar ? » : résumé fait par le modèle local à partir de la session, dont le
  contenu est une donnée (US2.13).

### 14. Éventail (`stage2/14-task-fan`)

- L'éventail de l'ADR 0010, nourri par les tâches du chantier 11 : un panneau par tâche, sous-tâches dans le
  panneau, ambre pour `waiting_answer` et `waiting_approval`, Approuver et Refuser dans le panneau.
- Double-clic sur l'orbe pour ouvrir et fermer ; le simple clic lance toujours l'écoute, retardé du délai de
  double-clic.
- Mesures de l'ADR 0010 refaites sur l'hôte réel.

### 15. Recette de l'étape (`stage2/15-acceptance`)

- Sur le PC de Benoit, en français puis en anglais : allumer une lumière, poser une question que le modèle
  local passe à Claude, lancer une tâche Claude Code sur un dossier autorisé, approuver une écriture, entendre
  l'annonce de fin.
- Liste des connexions sortantes par réglage, vérifiée une par une ; aucune avec la configuration par défaut,
  sauf la vérification des mises à jour.
- `features.md` passe l'étape 2 dans « Disponibles », la mémoire du projet dit que la série est finie.
- Benoit fusionne `stage2/base` dans `main`.

## Vérifications sur Windows, en un coup d'œil

| Vérification | Chantier |
| --- | --- |
| Essais E11 à E14 | 1 |
| Appels d'outils fiables avec `qwen3:14b` réel, dans les deux langues | 3 |
| Confirmation à la voix avec le vrai micro : « oui », « non », une phrase quelconque vaut refus | 4 |
| Jonctions et liens Windows refusés hors des dossiers autorisés | 5 |
| Serveur MCP lancé par commande locale (processus fermé à « Quitter ») | 6 |
| Une vraie lumière ; une serrure ou l'alarme demande le clic | 7 |
| Passage de main réel vers l'API, délai avant le premier son | 9 |
| Hooks installés puis retirés des réglages de Benoit sans rien y perdre | 11 |
| Session Claude Code réelle : écriture refusée, puis approuvée | 12 |
| Annonce d'une session lancée à la main ; heures de silence | 13 |
| Coût de l'éventail, passage des clics, confort du simple clic | 14 |

## Réponses de Benoit

Questions posées dans la conversation le 2026-11-20. Benoit a répondu le même jour : « je suis d'accord avec
tes recommandations. Tu peux aussi tout fusionner les branches dans main (en passant par une PR) puis
fusionner les PR dans main. »

1. **Point de départ de `stage2/base`.** `stage1/base` est fusionné dans `main` dès maintenant, par une PR
   que l'agent fusionne avec l'accord de Benoit ; `stage2/base` part de `main`. La recette de l'étape 1
   (chantier 17) et les vérifications Windows en attente restent à faire avec lui ; leurs corrections
   passent par une petite série à part.
2. **Fournisseur de la recherche web.** Une API avec clé (Brave Search), la clé dans le coffre, derrière un
   contrat pour en ajouter d'autres. Les conditions de l'offre gratuite se vérifient à l'écriture de l'ADR
   0017.
3. **Ordre des premiers chantiers.** Essais, puis ADR, puis outils ; l'éventail vers la fin, comme dans le
   tableau.

## Hors de cette série

Tâches planifiées, déclencheurs (dont les événements Home Assistant par WebSocket), Gmail et Agenda : étape 3.
Application mobile et confirmation par le téléphone jumelé : étape 4. Module Relais et suivi du travail (ADR
0013 et 0015) : v2. Plein écran « poste de commande » : pas encore planifié.
