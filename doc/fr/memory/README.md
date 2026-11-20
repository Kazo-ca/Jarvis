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
- **Prochain numéro d'ADR libre :** 0016. **ADR 0015 (source de suivi du travail : GitHub Issues ou Azure Boards ; `#<id>` pour GitHub, `AB#<id>` pour Azure DevOps dans les commits et les PR) : accepté** par Benoit le 19 novembre 2026 (chantier `stage1/work-tracking-adr`), avec confirmation avant toute création ou modification, fonctionnalité pour la v2 avec Relais. **ADR 0014 (licence Apache 2.0, règle sur Kazo Bar, retrait de `jarvis.py`) : accepté** par Benoit le 19
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
- **Essai à faire au début de l'étape 2 :** E11, coût et fluidité de la fenêtre transparente de l'éventail
  (ADR 0010).
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
  [pièges](../dev/pitfalls.md)) : **un ADR qui remplace cette ligne de l'ADR 0001 est à proposer à Benoit**.
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
- **Prochaine étape :** chantier `stage1/14-first-run`. Les tests Windows en attente (chantiers 4, 5, 6, 7, 8, 9, 10, 11, 12 et 13, [liste](../dev/hosts.md#vérifications-sur-windows)) se font à la prochaine séance avec Benoit. Plan : le [plan de la série de l'étape 1](../dev/plans/stage-1.md),
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
