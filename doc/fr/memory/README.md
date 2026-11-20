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
  dialogues, attente du déverrouillage, voir [dev/hosts.md](../dev/hosts.md)) livré dans sa PR ; vérification
  sur Windows à faire. Benoit fusionne `main` ; un agent fusionne
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
- **Prochaine étape :** vérification sur Windows du chantier 4 ([liste](../dev/hosts.md#vérifications-sur-windows)),
  puis chantier `stage1/5-shared-ui-language` du [plan de la série de l'étape 1](../dev/plans/stage-1.md),
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
