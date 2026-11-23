# Essais techniques

Synthèse des dix essais du [document de conception](plans/design.md#essais-techniques-à-faire-avant-de-coder),
faits le 18 novembre 2026 sur le PC de Benoit (RTX 3060 12 Go, .NET 10). Le détail, les mesures et la façon
de relancer chaque essai sont dans `spikes/<nom>/`.

| Essai | Verdict | En une phrase | Détail |
| --- | --- | --- | --- |
| E1 3CX | **Abandonné** | Benoit a retiré la téléphonie 3CX : seulement l'application MAUI. | [e1-3cx-sip](../../../spikes/e1-3cx-sip/README.md) |
| E2 sessions Claude Code | **Réussi** | Le registre `~/.claude/sessions/<pid>.json` donne chaque session et son état (`busy`, `idle`, `waiting`), lu en 73 ms. | [e2-claude-sessions](../../../spikes/e2-claude-sessions/RESULTS.md) |
| E3 Claude Desktop | **Réussi** | Ses sessions de code sont des processus Claude Code inscrits au même registre. | [e2-claude-sessions](../../../spikes/e2-claude-sessions/RESULTS.md) |
| E4 chaîne vocale | **Réussi, deux réserves** | 1,6 à 1,8 s entre la fin de la phrase et le premier son, en C# sans Python ; « hey jarvis » à la française mal reconnu ; Kokoro lent sur processeur. | [e4-voice-chain](../../../spikes/e4-voice-chain/RESULTS.md) |
| E5 mémoire GPU | **Réussi, marge mince** | Whisper turbo et le modèle 14B tiennent ensemble : 11,7 Go sur 12,3. Whisper sur processeur trop lent. | [e4-voice-chain](../../../spikes/e4-voice-chain/RESULTS.md) |
| E6 orbe WPF | **Échec sur la cible** | Orbe animé : 6 à 18 % d'un cœur avec WebView2, 4 à 7 % en WPF pur. Image fixe : 1,3 %. | [e6-orb-window](../../../spikes/e6-orb-window/RESULTS.md) |
| E7 outils du modèle local | **Réussi, changer de modèle** | qwen3 14B (réflexion coupée) : 42 sur 42, 0,9 s. qwen2.5-coder écrit ses appels en texte. | [e7-local-tool-calls](../../../spikes/e7-local-tool-calls/RESULTS.md) |
| E8 piloter Claude Code | **Réussi** | `claude -p` en flux JSON depuis C#, sans SDK : lecture faite, écritures non autorisées refusées et signalées. | [e8-drive-claude-code](../../../spikes/e8-drive-claude-code/RESULTS.md) |
| E9 événements Home Assistant | **Non par MCP, oui par WebSocket** | MCP sans notifications ; `subscribe_trigger` par l'API WebSocket fonctionne (4 déclenchements sur 4). | [e9-home-assistant-events](../../../spikes/e9-home-assistant-events/RESULTS.md) |
| E10 audio mobile | **En attente** | Demande un téléphone Android et une nuit de mesure batterie. | [e10-mobile-audio](../../../spikes/e10-mobile-audio/README.md) |

## Décisions prises le 18 novembre 2026

Benoit a accepté les recommandations issues des essais. Elles sont reprises dans les [ADR](adr/README.md) de
l'étape 1, indiqués à la fin de chaque ligne.

1. **Orbe en veille** (E6) : immobile en veille (image fixe, 1,3 % d'un cœur) ; l'animation complète ne
   tourne qu'en écoute, réflexion et parole. Le composant Razor partagé avec le mobile est conservé.
   ([ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md))
2. **Modèle local par défaut** (E7) : `qwen3:14b` avec la réflexion coupée (`think: false`), et un analyseur
   tolérant pour les modèles qui écrivent leurs appels d'outils en texte.
   ([ADR 0007](adr/0007-llm-providers-and-tool-format.md))
3. **Voix par défaut** (E4) : Piper (sherpa-onnx 1.13.8) ; Kokoro en option tant qu'il ne tourne pas sur le GPU.
   ([ADR 0006](adr/0006-voice-chain.md))
4. **Mot d'éveil** (E4) : modèle « hey_jarvis » d'openWakeWord ; seuil réglable et indicateur de score
   (US1.5), à ajuster avec la voix de Benoit pendant l'étape 1. ([ADR 0006](adr/0006-voice-chain.md))
5. **Pilotage de Claude Code** (E8) : `claude -p` en flux JSON, avec des réglages propres à Jarvis.
   ([ADR 0009](adr/0009-permission-and-confirmation-model.md) pour le principe ; la ligne de commande exacte
   attend l'ADR de l'étape 2.)
6. **Déclencheurs Home Assistant** (E9) : API WebSocket (`subscribe_trigger`) ; MCP pour les commandes.
   ([ADR 0009](adr/0009-permission-and-confirmation-model.md) pour le principe ; le détail attend l'ADR de
   l'étape 3.)
7. **Mobile** (E10) : l'étape 1 commence sans les essais sur le téléphone ; ils seront faits avant l'étape 4.
   ([ADR 0003](adr/0003-hosts-and-native-services.md))

## Essais de l'étape 2 (20 novembre 2026)

Faits sur le PC de Benoit par un agent en contrôle à distance (chantier `stage2/1-spikes`, voir le
[plan de l'étape 2](plans/stage-2.md)). Benoit n'a rien eu à faire.

| Essai | Verdict | En une phrase | Détail |
| --- | --- | --- | --- |
| E11 fenêtre de l'éventail | **Réussi** | WPF, fenêtre gardée cachée : fermé 0,5 %, ouvert immobile 0,3 %, animation 33 % d'un cœur pendant 0,4 s, première image en 6 ms ; clics justes ; simple clic retardé de 500 ms. WebView2 recréé : 2,4 s. | [e11-task-fan-window](../../../spikes/e11-task-fan-window/RESULTS.md) |
| E12 permissions et hooks de Claude Code | **Réussi, trois surprises** | Refus et accords de l'outil MCP de `--permission-prompt-tool` respectés ; les cinq hooks atteignent `/hook/…` et voient `RELAIS_TASK_ID` ; `SessionStart` n'accepte que les hooks `command` ; `CLAUDE.md` vient avec la source `project` et ses hooks. 0,009 $ la session. | [e12-claude-code-permissions](../../../spikes/e12-claude-code-permissions/RESULTS.md) |
| E13 outils en anglais | **Réussi** | `qwen3:14b` : 117 sur 117 en anglais, en français et en mélange, tous par le canal officiel, 0,9 s ; `kind` de `hand_off_to_claude` juste 43 fois sur 45. | [e13-tool-calls-english](../../../spikes/e13-tool-calls-english/RESULTS.md) |
| E14 MCP Home Assistant | **Réussi, mise en garde** | `ModelContextProtocol` 2.4.0, HTTP « streamable » sans état : 47 outils devenus `AIFunction`, lecture en 0,3 s, lumière allumée puis remise éteinte, reprise après coupure ; `HassTurnOff` déverrouille aussi les serrures et les étiquettes ne passent pas par MCP. | [e14-mcp-home-assistant](../../../spikes/e14-mcp-home-assistant/RESULTS.md) |
| E15 recherche par navigateur interne | **Réussi** | WebView2 dans une fenêtre WPF jamais affichée : 0 prise du premier plan sur 180 vérifications ; DuckDuckGo et Startpage 10 sur 10 en 1 à 1,8 s, Google illisible, Brave bloqué ; lecture seule et réseau local bloqués par l'hôte (0 requête sur 18) ; 124 Mo au repos, 1 Go en pointe. | [e15-webview-search](../../../spikes/e15-webview-search/RESULTS.md) |

## Décisions proposées après E11 à E14

**Acceptées par Benoit le 2026-11-20** (« je suis d'accord avec tes recommandations », en réponse aux cinq
questions posées dans la conversation). Ses choix sont à la suite de la liste ; là où ils changent une ligne,
ils l'emportent. Elles s'écrivent dans les ADR du chantier 2 (indiqués à la fin de chaque ligne).

1. **Éventail** (E11) : rendu WPF dans une seconde fenêtre transparente, **créée au démarrage et gardée
   cachée** (jamais détruite) ; animation de 400 ms à l'ouverture et 300 ms à la fermeture à 30 images par
   seconde ; l'orbe est remis au sommet à chaque activation de la fenêtre de l'éventail. L'ADR 0010 tient tel
   quel : pas d'ADR de remplacement. Si les panneaux doivent être des vues Razor, la même fenêtre cachée avec
   `BlazorWebView` tient aussi (21 ms, 0,6 % fermé). (Chantier 14.)
2. **Confirmation des écritures de Claude Code** (E12) : Jarvis expose un serveur MCP local en HTTP
   (`ModelContextProtocol.AspNetCore`) avec un outil `approve` passé par `--permission-prompt-tool` ; c'est
   **le seul point de décision** ; le hook `PermissionRequest` ne sert qu'à passer la tâche en
   `waiting_approval`. (ADR 0021)
3. **Hooks des sessions lancées par Jarvis** (E12) : dans le fichier de Jarvis passé par `--settings`, de type
   `http` avec `allowedEnvVars: ["RELAIS_TASK_ID"]`, sauf `SessionStart` en commande `curl`. Une tâche n'est
   finie qu'au `result` sans tâche de fond ; un `UserPromptSubmit` qui commence par `<task-notification>` n'est
   pas une demande humaine. (ADR 0021)
4. **Réglages chargés** (E12) : `--setting-sources ""` (rien de l'utilisateur ni du dépôt) et les consignes du
   dossier par `--append-system-prompt-file CLAUDE.md` ; la source `project` (consignes complètes, mais hooks
   du dépôt exécutés sans question) seulement pour les dossiers que l'utilisateur marque de confiance. Ligne de
   commande complète : [E12, section (d)](../../../spikes/e12-claude-code-permissions/RESULTS.md#d-ligne-de-commande-recommandée-pour-jarvis).
   (ADR 0021)
5. **Langue des outils** (E13) : la règle de l'ADR 0007 est confirmée (noms en anglais, descriptions dans la
   langue de l'assistant) ; `hand_off_to_claude(kind: question|task, reason)` dans les deux langues ; `kind`
   choisit la cible, jamais une permission. (ADR 0017 et 0020)
6. **Client MCP** (E14) : paquet `ModelContextProtocol` 2.4.0, transport HTTP « streamable » (`/api/mcp`) avec
   repli SSE, jeton dans l'en-tête `Authorization`, les outils donnés tels quels comme `AIFunction` ; sur une
   erreur réseau, réessayer sans recréer de session. (ADR 0018)
7. **Domotique de sécurité** (E14) : la classe se décide **après résolution de l'entité visée**, jamais sur le
   nom de l'outil : domaine `lock`, `alarm_control_panel`, `valve`, `siren`, ou `cover` de classe `garage`,
   `door` ou `gate` = critique ; scripts (`script__…`, `intent_script__…`) sensibles par défaut ; liste réglable
   d'entités et de scripts « sécurité » (MCP ne donne ni étiquettes ni zones). (ADR 0018)

### Réponses de Benoit du 2026-11-20

1. **Panneaux de l'éventail : Razor**, dans une fenêtre `BlazorWebView` créée au démarrage et gardée cachée,
   comme le reste de l'interface (point 1 : le rendu WPF pur n'est pas retenu). (Chantier 14.)
2. **Écritures de Claude Code : un seul point de décision**, l'outil de permission de Jarvis, qui pose la
   question à la voix ou près de l'orbe (point 2). (ADR 0021)
3. **Réglages du dépôt : le `CLAUDE.md` seul**, sans réglages ni hooks ; les réglages complets seulement pour
   les dossiers que l'utilisateur marque « de confiance » (point 4). (ADR 0021)
4. **Domotique de sécurité : d'après l'appareil visé**, plus une liste que l'utilisateur complète (point 7).
   (ADR 0018)
5. **Scripts Home Assistant : pas de confirmation par défaut.** L'utilisateur marque « sécurité » ceux qui
   exigent le clic. Le point 7 disait « sensibles par défaut » : ce n'est pas retenu. (ADR 0018)

Ce qui reste après ces essais : le jugement de Benoit sur la fluidité de l'éventail et le confort du simple
clic, la confirmation à la voix avec un vrai délai (l'expiration de l'appel MCP par Claude Code n'est pas
mesurée), et les 47 outils de Home Assistant donnés au modèle local.

## Après E15 (20 novembre 2026)

L'essai E15 prouve le navigateur interne de l'[ADR 0022](adr/0022-in-app-browser-for-web-search.md), fait sur le
PC de Benoit pendant qu'il travaillait. Le premier essai E15, sur Edge piloté par Playwright, a été arrêté à sa
demande et rien n'en est gardé. Ce que l'essai fixe pour le chantier `stage2/5-basic-tools` :

1. **Moteur par défaut : DuckDuckGo en version HTML** (`html.duckduckgo.com`), page sans script, la plus simple
   à lire. Repli : Startpage, puis Bing. Google (adresses cachées) et Brave Search par sa page (bloqué dès la
   première requête) ne sont pas proposés. Brave par son API reste le second fournisseur (ADR 0017).
2. **Fenêtre :** WPF jamais affichée (`EnsureHandle`, sans `Show`), hors barre des tâches, sans activation
   (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), **contrôleur WebView2 gardé visible**. Avec le contrôleur caché, la
   page est bridée et une capture attend plus de deux minutes. `Show()` sans activation pour « Voir la page ».
3. **Adresses refusées :** le contrôle se fait dans `NavigationStarting`, `FrameNavigationStarting` et
   `WebResourceRequested`, **avec résolution DNS** : un nom public qui pointe vers 127.0.0.1 passe un contrôle
   fait sur le texte seul.
4. **Mémoire :** fermer le navigateur après un temps sans usage et le recréer (0,6 s) : la mémoire ne redescend
   pas d'elle-même après des recherches (594 Mo au repos).
5. **Lire une page :** prendre le plus long des blocs `main` ou `article`, pas le premier ; retirer le texte des
   bandeaux de consentement. Une heuristique générique (« lien qui contient un titre ») sert de filet quand un
   moteur change sa page.

Non vérifié par E15 : le « DNS rebinding », l'enregistrement réel des mots de passe (les réglages sont
seulement relus), la tenue des moteurs sur la durée (une série de 10 requêtes), le coût en processeur.
