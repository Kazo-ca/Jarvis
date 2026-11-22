# Mémoire du projet

Point de reprise pour tout agent. À mettre à jour dans le même changement que
ce qu'il décrit.

## État

- **Série en cours :** `stage1` (étape 1), branche `stage1/base`, partie de `main` le 19 novembre 2026.
  Chantier 1 (`stage1/1-solution`, squelette de la solution, tests d'architecture, intégration continue)
  fusionné et vérifié sous Windows. Chantier 2 (`stage1/2-kernel`, bus de messages, machine à états, niveau
  sonore, voir [dev/kernel.md](../dev/kernel.md)) fusionné (PR 17). Chantier 3
  (`stage1/3-settings-storage-vault`, réglages `config.json`, base SQLite, coffre de secrets, voir
  [dev/storage.md](../dev/storage.md)) fusionné (PR 18). Chantier 4 (`stage1/4-hosts`, hôte Windows sur
  l'hôte générique .NET, orbe provisoire, zone de notification, raccourci global, démarrage avec Windows,
  dialogues, attente du déverrouillage, voir [dev/hosts.md](../dev/hosts.md)) fusionné (PR 20). Vérification
  sur Windows faite le 20 novembre 2026 (construction sans avertissement, tests 286/286 et E2E 11/11, clics
  à travers l'orbe, second lancement, raccourci sans perte de focus, coût immobile 0,8 % d'un cœur). Trois
  défauts trouvés, corrigés par `stage1/4b-hosts-fixes` : verrouillage non détecté sous Windows 11 (l'écran de
  verrouillage est sur le bureau `Default`), dérive × 1,5 de la position avec des écrans d'échelles
  différentes, aucun changement d'état dans le journal. Benoit a arrêté les tests le 20 novembre 2026 : **tests
  Windows en attente** (à regrouper pour une prochaine séance avec lui) : démarrage sur session verrouillée
  (cacher la caméra : Windows Hello déverrouille tout seul), position après relance avec des écrans
  d'échelles différentes, ligne « ActivationRequested: Idle -> Listening » au journal après `Ctrl+Alt+J`,
  « Quitter » en moins de 10 s, taille de l'orbe après reconnexion des écrans (240 × 240 vu une fois sur
  l'écran à 100 %, correct après relance), mise à jour de bout en bout entre deux versions de test (chantier
  16, [liste](../dev/updates.md#vérification-sur-windows)), et pour le chantier 5 : rechargement des textes de
  la fenêtre des paramètres au changement de langue dans `BlazorWebView`, menu de la zone de notification
  traduit, polices affichées hors ligne ([liste](../dev/hosts.md#vérifications-sur-windows), point 8), et pour le
  chantier 6 : coût de l'orbe légère en veille, micro coupé et erreur sous 2 % d'un cœur (obligatoire),
  coût des phases animées et de la tâche longue, clic et glissement, animations coupées dans Windows, et pour
  le chantier 7 : les mêmes mesures pour la version haute qualité (ADR 0011, section 8), changement de
  qualité sans redémarrer, rendu à 160 pixels ([liste](../dev/orb.md#vérifications-sur-windows)), et pour le chantier 8 : micro réel et niveau,
  micro coupé (Windows n'indique plus d'application qui écoute, coupure gardée après relance), micro débranché
  pendant que Jarvis tourne, son de test sur haut-parleurs et casque, coût du micro ouvert en veille
  ([liste](../dev/audio.md#vérifications-sur-windows)), et pour le chantier 10 : modèles Whisper et Silero déposés à la main,
  fin de phrase avec le vrai micro et du bruit (repli sur le seuil d'énergie si Silero n'est pas fiable), texte en
  français et en anglais, durée de transcription ([liste](../dev/transcription.md#vérifications-sur-windows)), et pour le chantier 12 : écoute des voix par Benoit
  (qualité perçue, vitesse, délai de la carte son, arrêt net, [liste](../dev/speech.md#vérifications-sur-windows)), et pour le chantier 9 : **seuil du mot d'éveil réglé avec la voix
  de Benoit** (valeur à écrire ici), « Hey Jarvis » pendant que Jarvis parle, coût du mot d'éveil
  ([liste](../dev/wake-word.md#vérifications-sur-windows)), et pour le chantier 13 : **délai entre la fin de la phrase et
  le premier son** (cible sous 2 s, valeur à écrire ici), « Hey Jarvis » pendant que Jarvis parle (sinon un ADR
  limite l'interruption au raccourci et au clic), fenêtre de conversation et personnalité
  ([liste](../dev/conversation.md#vérifications-sur-windows)). Chantier 16 (`stage1/16-updates`, mises à jour :
  tranche `Maintenance/Updates`, lecture des versions GitHub, programme `Kazo.Jarvis.Updater`, script
  `build/Publish-Release.ps1`, voir [dev/updates.md](../dev/updates.md)) livré dans sa PR ; sa vérification
  sur Windows attend la prochaine séance. Chantier 5 (`stage1/5-shared-ui-language`, style commun, polices
  embarquées, traductions `.resx`, réglage de langue, fenêtre des paramètres, champ de secret, voir
  [dev/shared-ui.md](../dev/shared-ui.md)) fusionné (PR 22), sans vérification Windows (Benoit a arrêté les
  tests le 20 novembre 2026). Benoit fusionne `main` ; un agent fusionne
  lui-même ses PR dans une branche `<série>/base` une fois les vérifications vertes (règle redite par Benoit
  le 19 novembre 2026).
- **Série `spikes` (terminée, fusionnée dans `main`) :** essais E1 à E10. Faits le 18 novembre 2026 : E2 à E9. E1 abandonné (pas de 3CX). L'étape 5 est réécrite le 19 novembre 2026 : appels entre Jarvis et l'application Android, dans les deux sens (US5.1 à US5.9 du document de conception), sans ADR pour l'instant ; elle dépend d'E10. En attente : E10
  (téléphone Android de Benoit). Synthèse : [dev/spikes.md](../dev/spikes.md).
- **Prochain numéro d'ADR libre :** 0028 (0027 = gestes sur l'orbe, PR 66). **ADR 0026 (vue des projets, des sujets et de leurs fils, complète 0013 et 0025) : proposé** le 22 novembre 2026 (chantier `stage2/adr-0026-projects-view`), d'après six réponses de Benoit sur les maquettes : quatre niveaux de travail (Relais), deux fils (sujet et tâche) ; bande des tâches faites / en cours / prévues ; réponse à une tâche dans le fil du sujet avec « T3> » à la manière de mIRC ; un fil par projet ; projet Général intégré, et Jarvis propose de faire un projet d'un sujet de Général ; rail des projets à gauche ; ouverte par le double-clic sur l'orbe (ADR 0027). Trois points ouverts (section 11), construit après l'ADR 0025. **ADR 0025 (un agent par genre de tâche, ses consignes et ses mémoires, complète 0017 et 0019) : proposé** le 21 novembre 2026 (chantier `stage2/adr-0025-task-agents`), d'après cinq réponses de Benoit à la recette de l'étape 2 : construit après la recette ; un appel de tri avant chaque demande, sauté quand l'utilisateur choisit l'agent dans la conversation ; agents en fichiers Markdown dans un dossier ; six agents de départ (Domotique, Web, Fichiers, Claude Code, Mémoire, Général) ; une mémoire par agent et une par projet, un projet étant un dossier local créé dans l'interface de Jarvis. Complété le même soir : réponses aux quatre points (le tri rend aussi le projet ; mémoire du projet en fichier Markdown dans son dossier ; passage de main en outil ; à la voix on ne nomme pas l'agent), **agent Jarvis** qui reçoit toujours la demande vocale et décide de passer la main en le disant, **mode « Appel »** (bouton « Appeler » ou « on doit discuter de tel projet », l'agent reste jusqu'à ce que l'utilisateur demande de revenir à Jarvis, « au revoir » termine), demande d'assigner à un projet un dossier qui n'en a pas. Trois points ouverts (section 13), pas encore accepté. **ADR 0024 (tour d'annonce, complète 0005) : accepté** par Benoit le 21 novembre 2026 (chantier `stage2/13-announcement-turn-adr`), sa réponse « Ok » prenant la recommandation : une annonce que personne n'a demandée (fin d'une session Claude Code, attente d'une permission) se dit dans un tour à elle, ouvert seulement au repos, coupé par une activation, écrit dans la conversation et jamais envoyé au modèle. **ADR 0023 (demande de confirmation dans un tour, complète 0005) : accepté** par Benoit le 21 novembre 2026 (chantier `stage2/confirmation-adr`), avec trois réponses qui diffèrent des recommandations : l'orbe a un aspect générique **« interaction requise »** (question, approbation, confirmation ou action manuelle) ; **aucun délai**, « il ne décide jamais à la place de l'utilisateur » ; pour une demande écrite, Jarvis dit seulement « la tâche X du projet Y a besoin de votre interaction », sans écouter de réponse. **ADR 0022 (recherche web dans un navigateur interne à Jarvis, remplace en partie 0017) : accepté** par Benoit le 20 novembre 2026 (chantier `stage2/in-app-browser-adr`) : fenêtre cachée pendant une recherche avec un bouton « Voir la page », témoins gardés, connexion par l'utilisateur derrière un avertissement. Benoit a corrigé la lecture de l'ADR 0017 : il voulait un navigateur **interne à l'application** (WebView2 dans une fenêtre de Jarvis, dossier de données à part, sans Playwright), pas Edge ou Chrome installé, « qui peut rentrer en conflit avec ce que l'utilisateur fait ». **Ne jamais piloter le navigateur de l'utilisateur.** Ce que l'ADR 0017 dit du navigateur piloté, du profil habituel et de l'essai E15 sur Edge ne vaut plus ; l'essai E15 est redéfini par l'ADR 0022 (WebView2). **Essai E15 : fait et réussi** le 20 novembre 2026 (chantier `stage2/e15-webview-search`) : fenêtre WPF jamais affichée avec le contrôleur WebView2 gardé visible, aucune prise du premier plan, DuckDuckGo HTML par défaut (repli Startpage puis Bing), adresses locales bloquées avec résolution DNS, navigateur fermé après un temps sans usage ([synthèse](../dev/spikes.md#après-e15-20-novembre-2026)). **ADR 0017 (outils, classes, point de décision), 0018 (client MCP, Home Assistant) et 0021 (sessions Claude Code) : acceptés** par Benoit le 20 novembre 2026 (chantier `stage2/2-adrs`). Ses choix à l'écriture : recherche web par un **navigateur piloté** (Edge ou Chrome installé, fenêtre visible, profil à part, lecture seule ; profil habituel possible derrière un avertissement), Brave Search dès qu'une clé est fournie ; pas de confirmation au lancement d'une session Claude Code ; aucune heure de silence par défaut, et Jarvis propose 8 heures de silence quand l'utilisateur dit qu'il va dormir. (La recherche par navigateur de l'ADR 0017 est remplacée par l'ADR 0022, voir plus haut.) Ils reprennent ses cinq réponses après les essais : panneaux de l'éventail en Razor dans une fenêtre gardée cachée, un seul point de décision pour les écritures de Claude Code, `CLAUDE.md` seul sauf dossier « de confiance », domotique de sécurité d'après l'appareil visé plus une liste, scripts Home Assistant sans confirmation sauf ceux marqués « sécurité ». **ADR 0019 (mémoire de conversation) et 0020 (passage de main, cibles, plafond de dépense) : acceptés** par Benoit le 20 novembre 2026 (chantier `stage2/2-adrs-memory-and-hand-off`) ; écrits avant les essais E11 à E14 parce qu'ils n'en dépendent pas. Ses choix : mémoire sur demande et automatique ; échange ancien après 2 heures de silence, réglable ; mémoire non envoyée au cloud, mais une session lancée par Jarvis reçoit un résumé des échanges précédents et la demande initiale complète ; rien sur l'orbe quand Claude répond ; plafond de 10 $ US ; réglage « Claude Code est payé par abonnement » coché par défaut. **ADR 0016 (un seul ONNX Runtime natif, celui de sherpa-onnx ; `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 pour le mot d'éveil, remplace une ligne de l'ADR 0001) : accepté** par Benoit le 20 novembre 2026 (chantier `stage1/onnx-runtime-adr`). **ADR 0015 (source de suivi du travail : GitHub Issues ou Azure Boards ; `#<id>` pour GitHub, `AB#<id>` pour Azure DevOps dans les commits et les PR) : accepté** par Benoit le 19 novembre 2026 (chantier `stage1/work-tracking-adr`), avec confirmation avant toute création ou modification, fonctionnalité pour la v2 avec Relais. **ADR 0014 (licence Apache 2.0, règle sur Kazo Bar, retrait de `jarvis.py`) : accepté** par Benoit le 19
  novembre 2026 (chantier `stage1/license-kazo-bar-android-calls`). Kazo Bar ne se référence jamais (ni projet, ni paquet) ; son code peut
  être copié ici et évolue ensuite séparément. `jarvis.py` n'est plus utilisé. Le passage du dépôt en public revient à Benoit. **ADR 0013 (préparer le module Relais, v2) : accepté** par Benoit le 19
  novembre 2026 (chantier `stage1/relais-architecture-adr`) ; rien ne se construit avant la v2, sauf l'hôte
  générique au chantier `stage1/4-hosts`. Benoit a choisi le même
  jour un tunnel pour tout le bus Relais (fermé par défaut, aucune route sans jeton). ADR 0012 (le script est permis dans le balisage, le style
  reste interdit) : accepté par Benoit le 19 novembre 2026, remplace en partie l'ADR 0002, section 7. Les ADR 0001 à 0009 (étape 1) sont **acceptés** par Benoit le
  19 novembre 2026 (chantier `spikes/11-stage-1-adrs`). Ils ne se modifient plus : un nouvel ADR les remplace.
- **ADR 0010 (éventail des tâches et clic sur l'orbe) : accepté** par Benoit le 19 novembre 2026 (chantier
  `spikes/12-task-fan-adr`). Il ne se construit qu'à l'étape 2. Benoit a choisi la maquette « éventail », sur
  l'ordinateur seulement, l'orbe dans n'importe quel coin de l'écran : décision ambre, bureau assombri,
  animation à l'ouverture et à la fermeture seulement. Réponse de Benoit du 19 novembre 2026 sur le clic :
  **le double-clic ouvre l'éventail, le simple clic lance l'écoute** (US1.6 et l'ADR 0005 ne changent pas).
- **Essais E11 à E14 : faits** le 20 novembre 2026 sur le PC de Benoit (chantier `stage2/1-spikes`), tous
  réussis : éventail en WPF, fenêtre gardée cachée (E11) ; permissions de `claude -p` par un outil MCP local et
  hooks vers `/hook/…` (E12, Claude Code 2.1.328) ; outils en anglais 117 sur 117 (E13) ; client MCP
  `ModelContextProtocol` 2.4.0 contre Home Assistant (E14). Synthèse et **décisions acceptées par Benoit
  le 20 novembre 2026** : [dev/spikes.md](../dev/spikes.md#décisions-proposées-après-e11-à-e14). Reste à juger
  par Benoit : fluidité de l'animation et confort du simple clic retardé.
- **ADR 0011 (couleurs et dessin de l'orbe) : accepté** par Benoit le 19 novembre 2026 (chantier
  `spikes/14-orb-look-adr`). Il reprend le catalogue validé : deux
  versions (haute qualité par défaut, légère en réglage), sept aspects, anneau de progression à la place des
  satellites, badge ambre des décisions. Pendant une tâche longue, l'orbe tourne lentement comme
  dans le prototype : choix de Benoit sur la carte du 19 novembre 2026, coût de 6 à 18 % d'un cœur accepté.
- **À mesurer à l'étape 1 (ADR 0011) :** le coût des deux versions de l'orbe sur l'hôte réel, en veille et en
  animation. Si la haute qualité dépasse nettement 14 à 18 % d'un cœur, la légère devient celle par défaut.
- **Décisions prises mais pas construites :** tout le document de conception.
- **Décisions issues des essais (prises le 18 novembre 2026, reprises dans les ADR 0003 à 0009) :** voir
  [dev/spikes.md](../dev/spikes.md#décisions-prises-le-18-novembre-2026). Orbe immobile en veille, `qwen3:14b`
  sans réflexion, Piper par défaut, E10 reporté avant l'étape 4.
- **Chantier 6** (`stage1/6-orb`, orbe version légère : tranche `Assistant/Orb`, composant `Orb` et
  `OrbView` nourri par la machine à états, page de démonstration `/orb` de l'hôte Web, voir
  [dev/orb.md](../dev/orb.md)) livré dans sa PR le 20 novembre 2026, sans vérification Windows.
- **Chantier 7** (`stage1/7-orb-high-quality`, orbe version haute qualité : sphère plasma et nuages binaires
  dans une toile limitée à 30 images par seconde, cinq anneaux gyroscopiques en SVG et CSS 3D tournés par le
  navigateur, réglage « qualité de l'orbe » dans la section `Assistant.Orb` et page « Orbe » des paramètres,
  appliqué sans redémarrer, voir [dev/orb.md](../dev/orb.md)) livré dans sa PR le 20 novembre 2026, sans
  vérification Windows. **La mesure de l'ADR 0011 (section 8) reste à faire sur Windows** : elle décide si
  la haute qualité reste la version par défaut. Mesure indicative dans un conteneur sans carte graphique :
  environ 21 % du fil principal en animation contre moins de 1 % pour la légère, 0 % en veille.
- **Chantier 11** (`stage1/11-llm-providers`, modèles de langage : contrat `ILlmConnector` et `IAssistantModel`
  du noyau sur `Microsoft.Extensions.AI.Abstractions` 10.11.1, adaptateurs `ollama`, `openai-compatible`/`openai`
  et `anthropic` écrits sur `HttpClient`, tranche `Llm/Providers` avec la page « Modèles de langage », modèle
  par défaut préchargé au démarrage, voir [dev/llm.md](../dev/llm.md)) livré dans sa PR le 20 novembre 2026,
  sans vérification Windows. **Tests Windows en attente** ([liste](../dev/llm.md#vérifications-sur-windows)) :
  Ollama sur 11435 et `llama-server` sur 8080, préchargement au démarrage, causes nommées, mesure d'un modèle
  de repli de 7 à 8 milliards de paramètres. **Choix provisoires** ([détail](../dev/llm.md#choix-provisoires-de-ce-chantier)) :
  port par défaut 11434 (Benoit règle 11435 une fois), `claude-opus-5-5` pour un nouveau fournisseur
  Anthropic, adaptateur Anthropic sur `HttpClient` plutôt que le SDK, réponse de 16 000 jetons au plus, test
  de connexion de 20 s.
- **Chantier 8** (`stage1/8-audio-devices`, périphériques audio : contrats `IAudioCapture` et
  `IAudioPlayback`, `MicrophoneFeed` du noyau (flux du micro et tampon de 30 s en mémoire, pour le mot d'éveil
  et la transcription), tranche `Audio/Devices` (micro, coupure US1.13, panne « micro absent », sortie, page
  « Audio »), WASAPI par NAudio 3.3 dans l'hôte Windows, voir [dev/audio.md](../dev/audio.md)) livré dans sa
  PR le 20 novembre 2026, sans vérification Windows. **Choix provisoires** (détail dans
  [dev/audio.md](../dev/audio.md#choix-provisoires-de-ce-chantier), à confirmer par Benoit) : la coupure du
  micro survit au redémarrage ; micro choisi absent = celui de Windows par défaut, sans panne ; couper le micro
  lève la panne « micro absent » ; tampon de 30 s ; niveau en décibels de −60 à 0 ; conversion vers 16 kHz
  faite par Jarvis.
- **Chantier 9** (`stage1/9-wake-word`, mot d'éveil : contrats `IWakeWordEngine` et `IWakeWordDetector` du
  noyau, adaptateur ONNX Runtime des trois modèles d'openWakeWord 0.5.1 dans `models\wake-word`, tranche
  `Voice/WakeWord` (écoute du flux du micro, seuil 0,5 réglable, une détection par activation, silencieux
  pendant l'écoute, page « Mot d'éveil » avec le score en direct), voir [dev/wake-word.md](../dev/wake-word.md))
  livré dans sa PR le 20 novembre 2026, sans vérification Windows. « Hey Jarvis. » de Piper : 0,999 ;
  « Hey Travis… » : 0,488. **Choix provisoires** ([détail](../dev/wake-word.md#choix-provisoires-de-ce-chantier),
  à confirmer par Benoit) : modèles absents = pas de panne (la page le dit, nouvel essai toutes les 30 s) ;
  actif pendant la réflexion et la parole, en pause pendant l'écoute ; curseur de 0,05 à 0,95 ; meilleur score
  des 3 dernières secondes affiché ; `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 sur le moteur natif de sherpa-onnx
  au lieu de `Microsoft.ML.OnnxRuntime` 1.32.0 de l'ADR 0001 (deux `onnxruntime` natifs cassaient Piper, voir les
  [pièges](../dev/pitfalls.md)) : **l'[ADR 0016](../dev/adr/0016-onnx-runtime-managed-on-sherpa-native.md), accepté, remplace cette ligne de l'ADR 0001**.
- **Chantier 10** (`stage1/10-transcription`, fin de phrase et Whisper : contrats `ISpeechRecognition` et
  `IVoiceActivityDetection` du noyau, message `UtteranceTranscribed` pour la conversation, tranche
  `Voice/Transcription` (écoute d'un tour parlé, fin de phrase par Silero avec repli sur un seuil d'énergie,
  Whisper chargé une fois, page « Transcription » avec la dernière demande entendue), adaptateurs Whisper.net
  1.9.1 et Silero par sherpa-onnx 1.13.8, voir [dev/transcription.md](../dev/transcription.md)) livré dans sa PR
  le 20 novembre 2026, sans vérification Windows. Silero essayé dans le conteneur sur un vrai enregistrement :
  avec 500 ms, la phrase se termine à la première pause naturelle (à surveiller avec la voix de Benoit).
  **Choix provisoires** ([détail](../dev/transcription.md#choix-provisoires-de-ce-chantier)) : attente de la
  parole 8 s, énoncé coupé à 25 s, 150 ms de voix pour commencer, 300 ms gardées avant la parole, repli silencieux
  sur le seuil d'énergie, pas de panne au démarrage si Whisper manque (seulement `TurnFailed` en fin de tour),
  texte vide publié tel quel (le chantier 13 termine le tour), modèles dans `models\whisper\` et `models\vad\`.
- **Chantier 12** (`stage1/12-speech`, synthèse vocale : contrat `ISpeechEngine` et `IVoiceSelection` du noyau,
  messages `SentenceToSpeak` et `AnswerSentencesEnded` pour la conversation, tranche `Voice/Speech` (phrases
  jouées dans l'ordre, arrêt net sur interruption, niveau de la voix vers l'orbe), tranche `Voice/TestVoice`
  (page « Voix »), Piper et Kokoro par sherpa-onnx 1.13.8, ElevenLabs et OpenAI, voir
  [dev/speech.md](../dev/speech.md)) livré dans sa PR le 20 novembre 2026. Piper essayé pour de vrai sous Linux ;
  **l'écoute des voix attend Benoit** ([liste](../dev/speech.md#vérifications-sur-windows) : qualité perçue,
  vitesse, délai de la carte son, arrêt net). **Choix provisoires** ([détail](../dev/speech.md#choix-provisoires-de-ce-chantier)) :
  la tranche de la voix publie `FirstSentenceReady` au premier son ; une voix par tour ; une voix dans le nuage
  qui échoue fait échouer le tour ; voix choisie absente = voix locale de la langue ; vitesse par langue ; clé
  OpenAI des voix séparée.
- **Chantier 13** (`stage1/13-conversation`, le tour complet : tranche `Conversation/Dialogue` (moteur du tour par
  messages, découpage en phrases, message système avec la personnalité et des règles fixes, historique dans la
  moitié du contexte, collection `Conversation.Turns`, fenêtre de conversation avec « Tout effacer », page
  « Personnalité »), la voix se tait aussi sur `TurnFailed`, rang des pages des paramètres corrigé (mot d'éveil 25,
  transcription 30, voix 35), voir [dev/conversation.md](../dev/conversation.md)) livré dans sa PR le 20 novembre
  2026, sans vérification Windows. Le noyau n'a pas changé. **Choix provisoires**
  ([détail](../dev/conversation.md#choix-provisoires-de-ce-chantier), à confirmer par Benoit) : fin de phrase et
  abréviations, Markdown non dit, historique de 20 tours au plus sans limite d'âge, tour enregistré à sa fin,
  personnalités des deux langues sur une page, envoi écrit seulement en veille.
- **Chantier 14** (`stage1/14-first-run`, premier lancement : contrats `IModelInstaller` et `IGraphicsCardProbe` du
  noyau, messages `WhisperModelChosen` et `FirstRunWizardRequested`, adaptateurs `HttpModelInstaller` (taille et
  empreinte vérifiées, `.tar.bz2` dépliés par SharpZipLib) et `NvidiaSmiGraphicsCardProbe`, tranche `Setup/Models`
  (page « Modèles vocaux », conseil selon la carte graphique), tranche `Setup/FirstRun` (assistant : langue, audio,
  modèles vocaux, modèle de langage ; section `Setup.FirstRun`), recherche d'Ollama sur 11434 puis 11435 dans la page
  « Modèles de langage », entrée « Assistant de configuration… » de la zone de notification, voir
  [dev/first-run.md](../dev/first-run.md)) livré dans sa PR le 20 novembre 2026, sans vérification Windows. Vrai
  téléchargement essayé dans le conteneur (GitHub) ; **Whisper (Hugging Face) jamais téléchargé pour de vrai**.
  **Choix provisoires** ([détail](../dev/first-run.md#choix-provisoires-de-ce-chantier), à confirmer par Benoit) :
  10 Go pour la chaîne complète, `small` en dessous, `base` sur le processeur sans NVIDIA, `qwen3:8b` conseillé en
  repli ; SHA-1 pour Whisper ; dépendance SharpZipLib 1.4.2 ; pas d'étape « Voix » ; les deux voix Piper cochées
  d'office ; recherche d'Ollama automatique tant que les fournisseurs sont ceux par défaut. L'hôte Web simule
  désormais un profil existant.
- **Chantier 15** (`stage1/15-faults`, pannes : `FaultOrigin` et `AssistantFault.Origin` du noyau, `FaultResolved` avec
  origine, `SettingsWindowRequested(PageKey)`, `FaultWindowRequested` ; tranche `Assistant/Faults` (titre, explication,
  action corrective et page de chaque cause, fenêtre « Problème de Jarvis », réglages illisibles posés au démarrage) ;
  panne du modèle de langage posée et levée hors d'un tour, Whisper retrouvé et voix entendue lèvent la leur ; entrée
  « Résoudre le problème… » de la zone de notification pendant une panne, voir [dev/faults.md](../dev/faults.md)) livré
  dans sa PR le 20 novembre 2026, sans vérification Windows. La table des transitions n'a pas changé. **Choix
  provisoires** ([détail](../dev/faults.md#choix-provisoires-de-ce-chantier), à confirmer par Benoit) : fenêtre ouverte
  par l'entrée du menu et toute seule seulement sur un refus faute de micro ; serveur injoignable posé après deux
  chargements ratés de suite ; la page « Voix » lève la panne de la voix ; aucun bouton pour les réglages illisibles ;
  origine dans le noyau.
- **Plan de l'étape 2 : validé** par Benoit le 20 novembre 2026 (chantier `stage1/stage-2-plan`, [plan](../dev/plans/stage-2.md)) : 15 chantiers `stage2/<n>-<sujet>`, d'abord les essais E11 à E14 sur son PC puis les ADR 0017 à 0021 (numéros réservés par le plan). Ses réponses : `stage1/base` fusionné dans `main` sans attendre la recette (il a autorisé l'agent à fusionner cette PR), `stage2/base` part de `main` ; recherche web par une API avec clé (Brave Search) derrière un contrat ; essais, puis ADR, puis outils, l'éventail vers la fin.
- **Chantier `stage2/3-tool-loop`** (boucle d'outils, voir [dev/tools.md](../dev/tools.md)) : contrats du
  noyau `ITool`, `IToolRegistry`, `ToolActivation` (section `Tools.Activation`), `IPermissionDecision` et
  `ActionClass` ; tranche `Permissions/Decision` (un seul point de décision, refus par défaut, journal dans
  la collection `Permissions.Decisions`) ; tranches `Tools/Clock` (`get_datetime`) et `Tools/Calculator`
  (`calculate`) ; les trois adaptateurs envoient les outils et lisent les appels, avec l'analyseur tolérant
  pour les adaptateurs locaux ; la conversation fait la boucle (8 appels par tour, 30 s par appel, résultat
  coupé à 2 000 caractères) et montre chaque appel dans le tour. « Sensible » et « Critique » passent par la demande de confirmation du chantier 4. La page « Outils » est faite au chantier 5. **Non vérifié avec un vrai modèle** ni contre les vraies API d'OpenAI et d'Anthropic. **Tests
  Windows en attente** ([liste](../dev/tools.md#vérifications-sur-windows)).
- **Chantier `stage2/4-confirmation`** (demande de confirmation, voir
  [dev/confirmation.md](../dev/confirmation.md)), selon l'ADR 0023 accepté : condition
  `AssistantState.Interaction` (`PendingInteraction`, genre question, approbation, confirmation ou action
  manuelle) et aspect « interaction requise » (ambre) dans le noyau, contrat `IConfirmation`,
  `ITool.Summarize` ; tranche `Permissions/Confirmation` (`ConfirmationDesk`, une demande à la fois,
  fenêtre, **aucun délai**, liste fermée de mots par langue) ; le point de décision demande la
  confirmation, revérifie le canal (« Critique » : clic seul) et journalise le canal ; page « Journal des
  permissions » ; `Voice/Speech` dit la question (tour parlé) ou l'annonce (tour écrit) sans phase
  « Parole », `Voice/Transcription` ouvre une seule écoute. Hôte Web : outils factices `write_note`
  (« Sensible ») et `erase_notes` (« Critique »). **Aucun vrai outil « Sensible » ou « Critique » n'existe
  encore.** Choix provisoires à confirmer par Benoit : fermer la fenêtre vaut refus ; l'annonce d'une
  conversation dit « Votre demande a besoin de votre interaction », la forme « la tâche X du projet Y »
  attend les sessions et les tâches ; la liste de mots suit la langue ; la fenêtre n'est pas encore placée
  près de l'orbe. **Tests Windows en attente** ([liste](../dev/confirmation.md#vérifications-sur-windows)).
- **Chantier `stage2/5-basic-tools`** (outils de base, voir [dev/basic-tools.md](../dev/basic-tools.md)),
  selon les ADR 0017 et 0022 : contrats du noyau `ILocalFiles`, `IReadingBrowser`, `IWebSearchApi`,
  `IWebAddressPolicy` et `ITool.Discloses` ; tranche `Tools/Files` (`list_directory`, `read_file`, page
  « Dossiers autorisés », aucun dossier autorisé au départ, liens et jonctions résolus avant la comparaison) ;
  tranche `Tools/Web` (`web_search` et `fetch_url`, **désactivés au départ**, adresses locales refusées, Brave
  Search quand une clé est au coffre, page « Recherche web ») ; tranche `Tools/Catalog` (page « Outils », un
  interrupteur par outil) ; navigateur de Jarvis en WebView2 dans l'hôte Windows (`Browser/`), bouton « Voir
  la page » dans la fenêtre de conversation. **Rien n'a tourné sur Windows** : le navigateur compile, ses
  scripts sont essayés dans Chromium. Choix provisoires à confirmer par Benoit
  ([détail](../dev/basic-tools.md#choix-provisoires-de-ce-chantier)) : chemin tapé sans sélecteur de dossier,
  pas de réglage « plus strict » par outil, fermeture du navigateur après 5 minutes, un seul onglet. **Tests
  Windows en attente** ([liste](../dev/basic-tools.md#vérifications-sur-windows)).
- **Chantier `stage2/6-mcp-client`** (client MCP, voir [dev/mcp.md](../dev/mcp.md)), selon l'ADR 0018,
  sections 1 à 4 : contrat du noyau `IToolServerConnector` (`Kernel/Tools/Servers`), adaptateur
  `McpToolServerConnector` sur le paquet `ModelContextProtocol.Core` 2.4.0, tranche `Mcp/Servers` (section
  `Mcp.Servers`, `McpServerHub`, page « Serveurs MCP ») : ajout par adresse ou par commande, test de connexion
  avec cause nommée, nouveaux essais de 5 s à 5 minutes, outils **désactivés à la découverte**, activation
  liée à l'empreinte, classe « Lecture » ou « Sensible » montée par l'utilisateur seulement, jetons au
  coffre. **Aucun vrai serveur MCP n'a été joint depuis Jarvis.** Choix provisoires à confirmer par Benoit
  ([détail](../dev/mcp.md#choix-provisoires-de-ce-chantier)) : un seul champ « Jeton », pas de modification d'un serveur sur la page, pas de
  variables d'environnement, paquet `Core`. **Tests Windows en attente**
  ([liste](../dev/mcp.md#vérifications-sur-windows)).
- **Chantier `stage2/7-home-assistant`** (voir [dev/home-assistant.md](../dev/home-assistant.md)), selon
  l'ADR 0018, section 5 : `ITool.ClassifyAsync` (le point de décision n'appelle que celle-ci), contrat du noyau
  `IToolServerClassifier`, tranche `Mcp/HomeAssistant` (`HomeAssistantClassifier`, section `Mcp.HomeAssistant`,
  page « Home Assistant »). Un appel est classé d'après les entités visées, lues par `GetLiveContext` : serrure,
  alarme, vanne, sirène, porte de garage, porte, portail et entités marquées donnent « Critique » ; cible
  introuvable = classe la plus haute possible ; script « Effet courant » sauf s'il est marqué ; sans la liste
  des entités, l'appel est refusé. **Aucun appel n'est parti de Jarvis vers le vrai Home Assistant.** Choix
  provisoires à confirmer par Benoit ([détail](../dev/home-assistant.md#choix-provisoires-de-ce-chantier)) :
  section `Mcp.HomeAssistant` et page à part, entité marquée par son nom, liste commune à tous les serveurs,
  étage traité comme cible introuvable, liste gardée 5 s, thermostat non classé de sécurité. **Tests Windows en
  attente** ([liste](../dev/home-assistant.md#vérifications-sur-windows)).
- **Chantier `stage2/8-memory`** (voir [dev/conversation-memory.md](../dev/conversation-memory.md)), selon
  l'ADR 0019 : contrat du noyau `IConversationMemory`, messages `TurnRecorded` et `ExchangesLeftHistory`,
  propriétés `ITool.ReadsOutsideContent` et `RefusedAfterOutsideContent` ; tranche `Conversation/Memory`
  (`MemoryBook`, `MemoryRecaller`, `MemoryWorker`, outils `remember_fact`, `forget_fact`, `search_memory`,
  section `Conversation.Memory`, page « Mémoire »). Faits de 200 caractères et un résumé de 1 200, bloc d'un
  huitième du contexte après les règles fixes, extraction par le modèle local à partir des seuls mots de
  l'utilisateur, au repos seulement, silence de 2 heures réglable. **Aucun vrai modèle n'a extrait un fait ni
  écrit un résumé.** Choix provisoires à confirmer par Benoit
  ([détail](../dev/conversation-memory.md#choix-provisoires-de-ce-chantier)) : collections
  `Conversation.MemoryFacts` et `Conversation.MemorySummary`, résumé limité à la moitié du budget, pas de
  limite d'âge quand la mémoire est coupée, `forget_fact` n'efface rien en cas d'ambiguïté, 500 faits au plus.
  **Tests Windows en attente** ([liste](../dev/conversation-memory.md#vérifications-sur-windows)).
- **Chantier `stage2/9-hand-off`** (voir [dev/hand-off.md](../dev/hand-off.md)), selon l'ADR 0020, sections 1
  à 5 : contrats du noyau `IHandOff` (`Kernel/Conversation`) et `ILlmProviderList` (`Kernel/Llm`) ; tranche
  `Llm/HandOff` (`HandOffTargets`, `HandOffTool` `hand_off_to_claude`, `HandOffPhrases`, section `Llm.HandOff`,
  page « Passage de main », ordre 11) ; dans `Conversation/Dialogue`, la boucle `RunAsync` sert au modèle
  local et à la cible, `ConversationTurn.HandOff` garde qui a répondu et pourquoi. Aucune cible au départ ;
  l'outil n'est au registre que quand une cible est réglée ; la mémoire ne part que si la case est cochée ;
  une cible qui échoue est une cause dite, jamais une panne. La cible des tâches (Claude Code) attend le
  chantier 12. **Aucun appel n'est parti vers une vraie API cloud** (le plafond de dépense
  est venu avec le chantier 10). Choix provisoires à confirmer par Benoit
  ([détail](../dev/hand-off.md#choix-provisoires-de-ce-chantier)) : motif montré mais pas dit, nom dit d'après
  le genre du fournisseur, formules sur « Claude » seulement, huit appels d'outils propres à la cible.
  **Tests Windows en attente** ([liste](../dev/hand-off.md#vérifications-sur-windows)).
- **Chantier `stage2/10-spend-cap`** (voir [dev/spend-cap.md](../dev/spend-cap.md)), selon l'ADR 0020,
  sections 6 et 7 : contrat du noyau `ISpendCap` (`Kernel/Llm`) et message `ProviderAnswered` ; causes
  `HandOffRefusal.NoPrice` et `CapReached`, `IHandOff.Notice` ; tranche `Llm/Spend` (`SpendLedger`,
  `KnownPrices`, sections `Llm.Spend` et `Llm.SpendCount`, collection `Llm.SpendEntries`, page « Dépense »,
  ordre 12). Plafond de 10 $ US par mois au départ ; un fournisseur cloud sans prix n'est jamais appelé ; le
  plafond est demandé avant chaque passage et avant chaque requête suivante de la cible ; au plafond l'outil
  quitte le registre ; Jarvis prévient une fois à 80 % et une fois au plafond. **Écarts avec l'ADR** : la
  collection a un seul point dans son nom ; le réglage « Claude Code est payé par abonnement » et
  `--max-budget-usd` attendent le chantier 12. **Aucun appel n'est parti vers une vraie API cloud : le compte
  n'a jamais été comparé à une facture.** Choix provisoires à confirmer par Benoit
  ([détail](../dev/spend-cap.md#choix-provisoires-de-ce-chantier)) : « Tout effacer » ne remet pas le compte à
  zéro, bouton de remise à zéro sur la page, avertissement dit après la réponse de la cible.
  **Tests Windows en attente** ([liste](../dev/spend-cap.md#vérifications-sur-windows)).
- **Chantier `stage2/11-tasks-and-sessions`, première partie** (voir
  [dev/tasks-and-sessions.md](../dev/tasks-and-sessions.md)), selon les ADR 0013 (section 6) et 0021 (sections
  6, 7 et 10) : forme d'une tâche dans le noyau (`WorkTask`, neuf statuts, `TaskKeys`, `ITaskBoard`) ; tranche
  `Tasks/Board` (collection `Tasks.Board`) ; contrat `IClaudeSessionRegistry` et adaptateur
  `Adapters/ClaudeCode/ClaudeSessionRegistry`, qui n'ouvre que `sessions/*.json` (un test prouve qu'aucun
  `*.key` ni `ide/*.lock` n'est ouvert) ; tranche `ClaudeCode/Sessions` (section `ClaudeCode.Sessions`, page
  « Sessions Claude », ordre 52). Choix provisoires à confirmer par
  Benoit ([détail](../dev/tasks-and-sessions.md#choix-provisoires-de-ce-chantier)) : lecture du registre
  activée au départ, toutes les 5 secondes. **Tests Windows en attente**
  ([liste](../dev/tasks-and-sessions.md#vérifications-sur-windows)).
- **Chantier `stage2/11-hook-server`, seconde partie du chantier 11** (voir
  [dev/hook-server.md](../dev/hook-server.md)), selon l'ADR 0021 (sections 3, 5 et 6) : contrats du noyau
  `ILocalServer`, `ILocalRoute`, `LocalServerOptions` (section `Local.Server`, port 47821), `HookPaths`,
  `IClaudeUserSettings` ; adaptateur `Adapters/LocalServer/KestrelLocalServer` (Kestrel sur `127.0.0.1`
  seulement, **aucune route sans jeton** : jeton de l'exécution en mémoire, jeton des hooks de l'utilisateur
  dans le coffre sous `ClaudeCode.UserHookToken`, valable pour `/hook/…` seulement) ; routes des hooks dans
  `Tasks/Board` (`TaskHooks`), qui créent les tâches de source `hook` et font bouger le tableau ; tranche
  `ClaudeCode/Hooks` (page « Hooks Claude Code », ordre 53) : aperçu, installation et retrait dans
  `%USERPROFILE%\.claude\settings.json` **sur le clic de l'utilisateur seulement**, avec copie datée.
  `Kazo.Jarvis.Adapters` référence `Microsoft.AspNetCore.App`. **Écarts par rapport à l'ADR 0021** : page à
  part au lieu d'un bouton dans « Sessions Claude » ; hooks installés en **commandes `curl.exe` en
  arrière-plan** (`async`) et non en hooks `http`, parce que la vérification demandée par la section 6 n'a
  pas pu se faire sans Windows. `/mcp` et `approve` : chantier 12. Choix provisoires à confirmer par Benoit
  ([détail](../dev/hook-server.md#choix-provisoires-de-ce-chantier)). **Tests Windows en attente, dont
  « Jarvis fermé, une session ne ralentit pas »** ([liste](../dev/hook-server.md#vérifications-sur-windows)).
- **Chantier `stage2/12-claude-code-launch`, première partie du chantier 12** (voir
  [dev/claude-code-launch.md](../dev/claude-code-launch.md)) : `ActionRequest.Session` (`SessionAction`) pour
  qu'une demande d'une session Claude Code passe par le point de décision avec une classe donnée par le code ;
  outil `approve` sur `/mcp` du serveur local (`ModelContextProtocol.AspNetCore`, jeton de l'exécution
  seulement) et contrat `IClaudeApprovals` ; contrat `IClaudeCode` et adaptateur `ClaudeCodeProcess` (ligne de
  commande de l'ADR 0021, fichiers sous `.kazo\jarvis\claude\` sans secret, jeton et tâche par
  l'environnement). Écarts : pas de hook `SessionStart` pour les sessions
  de Jarvis ; aucun délai de confirmation (ADR 0023 l'emporte sur l'ADR 0021). **Non vérifié : aucun vrai
  Claude Code lancé par ce code ; variables dans les en-têtes du fichier MCP.**
- **Chantier `stage2/12-claude-code-tasks`, seconde partie du chantier 12** (voir
  [dev/claude-code-tasks.md](../dev/claude-code-tasks.md)) : tranche `ClaudeCode/Launch` (`ClaudeTaskRunner`,
  page « Tâches Claude Code », ordre 54). « Demande à Claude Code de… » lance une session sur un dossier
  autorisé (choisi par son nom dans la demande, sinon Jarvis demande lequel) ; tâche créée avant le lancement ;
  demandes classées par `SessionRules` puis passées au point de décision ; session arrêtée si une écriture
  s'exécute sans avoir demandé ; fins `done`, `blocked`, `abandoned` ; `ClaudeTaskEnded` publié. Contrats du
  noyau : `IClaudeTasks`, `IAllowedFolders`, `ISpendCap.SessionBudget`. Réglage « Claude Code est payé par
  abonnement » (coché par défaut) dans `Llm/Spend`. **Écarts : le résumé de fin n'est pas encore dit à voix
  haute (chantier 13) ; `kind: task` n'est pas proposé au modèle local, seule la formule lance une session.**
  Choix provisoires à confirmer par Benoit ([détail](../dev/claude-code-tasks.md#choix-provisoires-de-ce-chantier)).
  **Aucune vraie session lancée ; tests Windows en attente**
  ([liste](../dev/claude-code-tasks.md#vérifications-sur-windows)).
- **Chantier `stage2/13-session-announcements`** (voir
  [dev/session-announcements.md](../dev/session-announcements.md)) : le tour d'annonce de l'ADR 0024 dans le
  noyau (`AnnouncementSubmitted`, `TurnInput.Announcement`, `AnnouncementToSay`) et dans la conversation
  (`AnnouncementDesk` : écrit tout de suite, dit au repos, une à la fois, jamais envoyé au modèle) ; tranche
  `ClaudeCode/Announcements` (`SessionAnnouncer` surveille le tableau des tâches, donc mêmes annonces pour une
  session lancée par Jarvis ou vue par ses hooks ; choix par dossier ; heures de silence, aucune par défaut ;
  période de silence gardée en mémoire seulement ; outils `start_quiet_period` et `get_claude_sessions` ; page
  « Annonces », ordre 55). Le résumé de fin laissé de côté au chantier 12 est dit ici. **Écarts : le badge
  ambre de l'orbe pendant le silence est arrivé au chantier 14 ; « je vais dormir » repose sur la description de
  l'outil, donc sur le modèle local.** Choix provisoires à confirmer par Benoit
  ([détail](../dev/session-announcements.md#choix-provisoires-de-ce-chantier)). **Aucune annonce dite par une
  vraie voix ; tests Windows en attente** ([liste](../dev/session-announcements.md#vérifications-sur-windows)).
- **Chantier `stage2/14-task-fan`** (voir [dev/task-fan.md](../dev/task-fan.md)) : l'éventail de l'ADR 0010.
  Noyau : `TaskFanToggleRequested`, `IPointerTimings` (délai de double-clic donné par l'hôte),
  `ITaskFanSurface` et `FanAnchor`, `IOpenConfirmations` (la demande de confirmation ouverte, que le bureau des
  confirmations met en œuvre), `TaskId` sur `ConfirmationRequest` et `SessionAction`. Orbe : `OrbActivation`
  retarde le simple clic du délai de double-clic et publie le double-clic ; `OrbView` nourrit le badge ambre.
  Tranche `Tasks/Fan` (`TaskFan`, `FanContent`, `FanLayout`, `TaskFanView` : un SVG dont l'origine est l'orbe,
  cinq panneaux au plus, sous-tâches dans le panneau, demande d'une session dans le panneau de sa tâche avec
  Autoriser, Refuser et Détails, animation en pas à 30 images par seconde au plus). `Tasks/Board` publie
  maintenant `DelegatedTaskCountChanged` (`TaskCount`). Hôte Windows : `FanWindow` créée une fois après l'orbe
  et gardée cachée, `SystemPointerTimings`. Hôte Web : l'orbe est dans un coin de la page. Choix provisoires à
  confirmer par Benoit ([détail](../dev/task-fan.md#choix-provisoires-de-ce-chantier)). **Rien n'a tourné sur
  Windows ; les mesures de l'ADR 0010 ne sont pas refaites sur l'hôte réel ; tests Windows en attente**
  ([liste](../dev/task-fan.md#vérifications-sur-windows)).
- **Prochaine étape :** série `stage2`, branche `stage2/base` partie de `main`, chantier 15 (`stage2/15-acceptance`, recette sur le PC de Benoit, avec lui) du [plan](../dev/plans/stage-2.md) ; le chantier `stage2/14-task-fan` est fait (les chantiers `stage2/3-tool-loop`, `stage2/4-confirmation`, `stage2/5-basic-tools`, `stage2/6-mcp-client`, `stage2/7-home-assistant`, `stage2/8-memory`, `stage2/9-hand-off`, `stage2/10-spend-cap` `stage2/11` (ses deux parties, `stage2/11-tasks-and-sessions` et `stage2/11-hook-server`) et `stage2/12` (ses deux parties, `stage2/12-claude-code-launch` et `stage2/12-claude-code-tasks`) et `stage2/13-session-announcements` sont faits ; les ADR 0017 à 0022 sont tous acceptés). Le chantier `stage2/1-spikes` (essais E11 à E14) est fait. `stage1/base` est fusionné dans `main` le 20 novembre 2026 **sans recette** (décision de Benoit) : il reste le chantier `stage1/17-acceptance` (recette sur le PC de Benoit, avec lui), dont les corrections passeront par une petite série à part. Les tests Windows en attente (chantiers 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 et 15, [liste](../dev/hosts.md#vérifications-sur-windows)) se font à la prochaine séance avec Benoit. Plan : le [plan de la série de l'étape 1](../dev/plans/stage-1.md),
  **validé** par Benoit le 19 novembre 2026 (17 chantiers `stage1/<n>-<sujet>`). Construire et tester :
  [dev/build.md](../dev/build.md).
- **Réponses de Benoit du 19 novembre 2026 sur le plan :** intégration continue GitHub Actions sous Linux
  (tests Windows sur son PC) ; une demande écrite reçoit une réponse écrite seulement ; les vérifications sur
  Windows sont faites par un agent sur son PC par le contrôle à distance, lui seulement pour sa voix et ses
  oreilles (chantiers 9, 10, 12, 13 et 17).
- **Réponses de Benoit du 19 novembre 2026 (inscrites dans les ADR) :** base SQLite pour la conversation, la
  mémoire et le journal (ADR 0004) ; abstractions `Microsoft.Extensions.AI` (ADR 0007) ; un clic exigé pour la
  domotique de sécurité seulement (ADR 0009) ; hypothèses n° 1, 2, 3, 7 et 9 de la conception acceptées ; les
  données vont dans `%USERPROFILE%\.kazo\jarvis`, pas dans `%APPDATA%` (le nom du dossier a été choisi par
  l'agent ; le changer demande un nouvel ADR).
- **À vérifier au début de l'étape 1** (les essais ne l'ont pas fait) : mot d'éveil avec la voix de Benoit et
  pendant que Jarvis parle, micro réel, fin de phrase par Silero, passage des clics autour de l'orbe, coût de
  l'orbe en veille sur l'hôte réel, rechargement des fenêtres au changement de langue.
- **Designs de l'interface validés le 19 novembre 2026 :** orbe haute qualité (plasma, nuages binaires,
  anneaux gyroscopiques), orbe légère (sphère fixe, trois anneaux ouverts), plein écran « poste de commande ».
  Décrits dans [dev/ui/catalog.md](../dev/ui/catalog.md), prototypes dans `design/prototypes/`. L'ADR 0011
  s'appuie sur ce catalogue.
- **Licences à régler avant une diffusion publique :** espeak-ng (GPLv3, dans sherpa-onnx 1.13.8), modèles
  d'openWakeWord (CC BY-NC-SA 4.0), licence de chaque voix (ADR 0006).
- **Dépôt public :** Benoit a confirmé le 19 novembre 2026 que `Kazo-ca/Jarvis` est public ([ADR 0014](../dev/adr/0014-license-kazo-bar-rule-and-jarvis-py.md)). La
  vérification des versions (US1.18) n'a donc pas besoin de jeton ; la conséquence de l'ADR 0001 qui en
  parlait ne s'applique plus.
- **Échecs de tests connus :** aucun.
- **Vérification sur Windows du chantier 1 : faite** le 19 novembre 2026 par un agent sur le PC de Benoit
  (SDK 10.0.402) : construction sans avertissement, tests unitaires 29/29, Playwright 1/1, hôte Web en
  HTTP 200, hôte Windows ouvert, réactif et fermé sans erreur. Lancé sur une session verrouillée, l'hôte
  Windows mourait : réglé au chantier 4 (voir [pièges](../dev/pitfalls.md)).
- **Choix provisoires du chantier 6 que les ADR 0005 et 0011 ne tranchent pas** (détail dans
  [dev/orb.md](../dev/orb.md#choix-provisoires), à confirmer par Benoit) : erreur = éclair
  puis image fixe (pas la vitesse 0,25 du catalogue) ; mouvement réduit = aucune rotation ni pulsation,
  écoute et parole distinguées de la veille par la lumière seulement ; progression inconnue = cercle vert
  pointillé fixe ; corps de l'orbe à 80 % de la fenêtre ; badge « 99+ ». Le clic reste attrapé par la
  fenêtre WPF, qui le passe à `OrbActivation` de la tranche.
- **Choix provisoires du chantier 7** (détail dans [dev/orb.md](../dev/orb.md#choix-provisoires), à
  confirmer par Benoit) : proportions du prototype gardées (sphère à 30 % du côté, donc environ 48 pixels
  dans la fenêtre de 160 pixels, chiffres des nuages réduits à une texture) ; lumière de chaque aspect
  commune aux deux versions ; erreur et mouvement réduit comme la version légère ; balancement du
  gyroscope en aller-retour de 25 s ; halo en CSS ; page « Orbe » après « Langue » dans les paramètres.
- **Choix du chantier 5 que les ADR 0008 et 0011 ne tranchent pas** (détail dans [dev/shared-ui.md](../dev/shared-ui.md)) :
  `IStringLocalizer` lit la langue de l'assistant à chaque recherche, jamais la culture du fil (les formats
  gardent ceux de Windows) ; une fenêtre se reconstruit entière au changement de langue (`LanguageScope`),
  la barre de titre garde la langue de l'ouverture ; réglage dans la section `Settings.Language`, absent tant
  que l'utilisateur n'a pas choisi (la langue de Windows s'applique) ; les pages de réglages s'enregistrent
  par `SettingsPage` du noyau ; polices Rajdhani 500/600, Inter 400/600, JetBrains Mono 400/500 en `woff2`
  latin (`@fontsource` 5.3.0, licence OFL 1.1 vérifiée) ; le champ de secret ne relit jamais la valeur.
- **Choix du chantier 16 que l'ADR 0001 ne tranche pas** (détail dans [dev/updates.md](../dev/updates.md)) :
  vérification automatique **activée par défaut** (choix de Benoit le 20 novembre 2026 ; seule exception à
  « aucune connexion sortante par défaut » du chantier 17), une minute après le démarrage puis toutes les
  6 heures ; recherche manuelle par « Mises à jour… » dans la zone de notification.
  Pré-versions proposées tant que la version est en 0.x ; le programme de mise à jour est lancé depuis la
  nouvelle version décompressée, sans fenêtre ; une construction de développement n'installe jamais.
- **Choix du chantier 4 que les ADR 0003 et 0013 ne tranchent pas** (détail dans [dev/hosts.md](../dev/hosts.md)) :
  raccourci par défaut `Ctrl+Alt+J` (section `Desktop.Hotkey`) ; pas de contrat pour la zone de notification,
  ses entrées Paramètres et Conversation publient un événement et restent grisées sans tranche qui le traite ;
  le clic sur l'orbe est attrapé par la fenêtre WPF en attendant le chantier 6 ; position de l'orbe dans la
  section `Desktop.OrbWindow` ; journal technique en fichiers texte quotidiens gardés 14 jours ; profil
  WebView2 dans `%USERPROFILE%\.kazo\jarvis\webview` ; une construction de développement ne s'inscrit pas
  au démarrage avec Windows (sauf `KAZO_JARVIS_REGISTER_STARTUP=1`).
- **Choix du chantier 3 que l'ADR 0004 ne tranche pas** (détail dans [dev/storage.md](../dev/storage.md)) :
  sections de `config.json` au premier niveau à côté de `schemaVersion` ; choix du coffre dans la section
  `Secrets.Vault` ; secrets du gestionnaire d'identification nommés `Kazo.Jarvis/<référence>`, 2 560 octets
  au plus ; `secrets.json` chiffré valeur par valeur ; une base plus récente rend le stockage indisponible
  sans y toucher ; `JarvisPaths` reste dans `Adapters` (à remonter dans le noyau si une tranche a besoin
  du dossier des modèles, chantiers 9 et 14).
- **Vérification sur Windows des chantiers 2 et 3 : faite** le 19 novembre 2026 par un agent sur le PC de
  Benoit (SDK 10.0.402) : construction sans avertissement, tests unitaires 252/252 dont les 5 tests `Windows`
  (gestionnaire d'identification, DPAPI, enregistrement des coffres), E2E 5/5, aucune identification de test
  restante, hôte Windows démarré sans plantage et dossiers `%USERPROFILE%\.kazo\jarvis` créés.
  `C:\jarvis\OpenJarvis` est un clone d'OpenJarvis, pas de Jarvis : l'agent a cloné la branche dans
  `C:\jarvis\temp\jarvis-check`.
- **Choix du chantier 2 que l'ADR 0005 ne tranche pas :** une interruption alors que le micro est coupé ou
  absent arrête Jarvis sans démarrer l'écoute ([dev/kernel.md](../dev/kernel.md#cas-que-ladr-0005-ne-couvre-pas)).

## Environnement de développement de Benoit

- GPU : NVIDIA RTX 3060 12 Go. Avec un modèle 14B et Whisper turbo, il reste environ 600 Mio.
- Ollama tourne dans Docker sur le port **11435** (pas 11434). Modèles : `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` et `qwen2.5:14b` (ajoutés pour E7).
- llama-server sur `127.0.0.1:8080`.
- Les secrets, modèles et artefacts jetables vont dans `C:\jarvis\temp` (modèles dans `C:\jarvis\temp\models`),
  jamais dans le dépôt.
