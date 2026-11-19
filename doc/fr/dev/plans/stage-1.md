# Plan de la série de l'étape 1 : orbe, voix, modèle local et paramètres

2026-11-19 · Proposé, en attente de la validation de Benoit (chantier `spikes/15-stage-1-plan`). S'appuie sur
l'ADR 0011 (couleurs et dessin de l'orbe), accepté le même jour.

## But

À la fin de cette série, on dit « hey jarvis » devant le PC, on pose une question, et le modèle local répond
à voix haute en français ou en anglais. Rien ne sort de la machine. Ce sont les récits US1.1 à US1.18 du
[document de conception](design.md#étape-1--orbe-voix-modèle-local-et-paramètres).

Tout ce qui se construit ici est déjà décidé par les ADR acceptés 0001 à 0009 et par l'ADR 0011 (couleurs et
dessin de l'orbe). Ce plan ne prend aucune décision d'architecture : il dit **dans quel ordre** construire, **en
combien de morceaux**, et **comment savoir qu'un morceau est fini**. L'ADR 0010 (éventail) n'est pas
construit à l'étape 1.

## Branches et fusion

- Série `stage1`, branche de base `stage1/base`, partie de `main` (question n° 1 plus bas).
- Un chantier par branche `stage1/<n>-<sujet>`, une PR par chantier vers `stage1/base`.
- Un agent fusionne lui-même sa PR dans `stage1/base` quand la construction et les tests sont verts
  (AGENTS.md, IV.5). Seul Benoit fusionne `stage1/base` dans `main`, à la fin de la série.
- Les chantiers suivent l'ordre du tableau. Ceux que le tableau marque comme indépendants peuvent avancer en
  parallèle, chacun dans sa branche.

## Ce que chaque chantier doit livrer

Un chantier n'est fini que si tout ceci est vrai :

1. `dotnet build` et `dotnet test` sont verts sur le PC de Benoit, y compris les tests d'architecture de
   l'ADR 0002 et le test des clés de traduction de l'ADR 0008. Aucun test désactivé.
2. Chaque tranche a ses tests unitaires, avec des contrats simulés (aucun micro, modèle ou réseau réel).
3. Tout parcours visible a son test Playwright contre l'hôte Web.
4. Chaque contrat natif ajouté a sa mise en œuvre réelle et sa mise en œuvre simulée dans l'hôte Web
   (ADR 0003). Un contrat n'est ajouté que par le chantier qui en a besoin.
5. Tout texte visible existe en anglais (`.resx`) et en français (`.fr.resx`).
6. Les **vérifications sur Windows** du chantier sont faites sur le PC de Benoit et leur résultat est écrit
   dans la PR (ce qui a été vérifié, par qui, ce qui a échoué).
7. La documentation touchée est à jour dans les deux langues, la mémoire du projet aussi, et tout piège
   rencontré entre dans `pitfalls.md`.
8. Aucun code de `spikes/` n'est référencé. On reprend le comportement prouvé par les essais, on réécrit le
   code proprement dans sa tranche.

## Vue d'ensemble

| N° | Chantier (branche `stage1/…`) | Récits | ADR | Dépend de | Taille |
| --- | --- | --- | --- | --- | --- |
| 1 | `1-solution` : squelette de la solution | — | 0001, 0002 | — | M |
| 2 | `2-kernel` : bus de messages et machine à états | — | 0002, 0005 | 1 | M |
| 3 | `3-settings-storage-vault` : configuration, base SQLite, coffre | US1.16 | 0004 | 2 | L |
| 4 | `4-hosts` : hôte Windows, hôte Web, zone de notification, raccourci | US1.1, US1.2, US1.6 | 0003 | 3 | L |
| 5 | `5-shared-ui-language` : style commun, traductions, réglage de langue | US1.4 (langue) | 0008, 0011 | 4 | M |
| 6 | `6-orb` : tranche de l'orbe, version légère, page de démonstration | US1.3, US1.6 (clic) | 0005, 0011 | 5 | L |
| 7 | `7-orb-high-quality` : version haute qualité et mesures | US1.3 | 0011 | 6 | M |
| 8 | `8-audio-devices` : micro, sortie audio, coupure du micro | US1.13 | 0003, 0006 | 5 | M |
| 9 | `9-wake-word` : mot d'éveil et indicateur de score | US1.5 | 0006 | 8 | M |
| 10 | `10-transcription` : fin de phrase et Whisper | US1.7 | 0006, 0008 | 8 | M |
| 11 | `11-llm-providers` : fournisseurs et modèle local par défaut | US1.8, US1.9 (modèle) | 0007 | 5 | L |
| 12 | `12-speech` : synthèse et page de test des voix | US1.10, US1.11 | 0006, 0009 | 8 | L |
| 13 | `13-conversation` : le tour complet, l'interruption, la fenêtre de conversation, la personnalité | US1.9, US1.12, US1.14, US1.15 | 0005, 0007, 0008 | 6, 9, 10, 11, 12 | L |
| 14 | `14-first-run` : assistant du premier lancement et téléchargement des modèles | US1.4 | 0004, 0006, 0008 | 13 | M |
| 15 | `15-faults` : pannes nommées et actions correctives | US1.17 | 0005 | 14 | S |
| 16 | `16-updates` : mises à jour et publication | US1.18 | 0001 | 4 | M |
| 17 | `17-acceptance` : recette de l'étape sur le PC de Benoit | toutes | — | tous | S |

Tailles : S, quelques heures ; M, une journée ; L, plus d'une journée. Ce sont des ordres de grandeur.

Indépendants une fois leurs dépendances faites : 6 et 7 (l'orbe) d'un côté, 8 à 12 (voix et modèle) de
l'autre, et 16 (mises à jour) à tout moment après 4.

```
1 ─ 2 ─ 3 ─ 4 ─ 5 ─┬─ 6 ─ 7 ─────────────────┐
                   ├─ 8 ─┬─ 9 ───────────────┤
                   │     ├─ 10 ──────────────┤
                   │     └─ 12 ──────────────┼─ 13 ─ 14 ─ 15 ─ 17
                   └─ 11 ────────────────────┘
               4 ─ 16 ───────────────────────────────────────┘
```

## Détail des chantiers

### 1. Squelette de la solution (`stage1/1-solution`)

- `Kazo.Jarvis.slnx`, `global.json` (SDK 10.0.402, `rollForward: latestFeature`), `Directory.Packages.props`
  avec les versions de l'ADR 0001 ; `Directory.Build.props` existe déjà.
- Les projets de l'étape 1 de l'ADR 0001 : `Kernel`, `Features`, `Adapters`, `Windows`, `Web`,
  `Tests.Unit`, `Tests.E2E`. `Updater` attend le chantier 16, `Mobile` l'étape 4.
- L'hôte Windows ouvre une fenêtre vide, l'hôte Web sert une page vide, Playwright ouvre cette page.
- Les **tests d'architecture** de l'ADR 0002, section 7, tous écrits ici : ils sont verts sur un dépôt
  presque vide et le resteront.
- Intégration continue selon la réponse à la question n° 2.
- **Vérification sur Windows :** les deux hôtes démarrent, les tests passent.

### 2. Noyau (`stage1/2-kernel`)

- Le bus en mémoire de l'ADR 0002, section 5 : événements et demandes, typé, asynchrone ; un abonné qui
  échoue n'empêche pas les autres et l'erreur est journalisée.
- La machine à états de l'ADR 0005 : phase, conditions, identifiant de tour, table des transitions, un seul
  écrivain, message `AssistantStateChanged`, et la fonction d'aspect telle que l'ADR 0011 la complète
  (aspect « tâche longue »).
- Le contrat du niveau sonore (0 à 1, au plus 30 fois par seconde), hors du bus.
- **Tests :** chaque ligne de la table des transitions, les paires ignorées, un message d'un tour annulé,
  la fonction d'aspect pour toutes les combinaisons de phase et de conditions.

### 3. Configuration, stockage et coffre (`stage1/3-settings-storage-vault`)

- Racine `%USERPROFILE%\.kazo\jarvis` et ses sous-dossiers (ADR 0004, section 1) ; un dossier temporaire
  pour les tests.
- Configuration : contrat du noyau, fichier `config.json` dans `Adapters` : `schemaVersion`, une section par
  tranche, sections inconnues conservées, écriture atomique, copie dans `backups\` avant migration, fichier
  illisible ou plus récent jamais écrasé (l'application démarre sur les valeurs par défaut et le signale).
- Magasin de documents : contrat du noyau, SQLite dans `Adapters`, magasin en mémoire pour l'hôte Web,
  schéma versionné, bouton « tout effacer » (le bouton lui-même vient avec la fenêtre de conversation, au
  chantier 13).
- Coffre (US1.16) : contrat du noyau, Gestionnaire d'identification Windows (par défaut) et fichier
  `secrets.json` chiffré par DPAPI, changement de coffre qui déplace les secrets, coffre en mémoire pour
  l'hôte Web. Aucune valeur dans un journal ou un message d'erreur.
- **Tests :** sur dossier temporaire ; les tests du coffre Windows sont marqués Windows et tournent sur le PC
  de Benoit.

### 4. Hôtes (`stage1/4-hosts`)

- Hôte Windows (ADR 0003, section 2) : une seule instance ; fenêtre de l'orbe transparente, sans bordure, au
  premier plan, hors de la barre des tâches, sans prise de focus, déplaçable, position mémorisée (US1.1),
  avec un orbe provisoire (un disque) ; contrat de dialogue (une fenêtre par clé, même fournisseur de
  services) ; message clair si WebView2 manque.
- Zone de notification (US1.2) : clic gauche affiche ou cache l'orbe ; menu Paramètres, Conversation, Couper
  le micro (inactif jusqu'au chantier 8), Quitter.
- Raccourci global (US1.6) : contrat, réglage du raccourci, événement d'activation vers la machine à états.
- Démarrage avec Windows : activé par défaut, réglage pour le couper (hypothèse n° 3).
- Hôte Web : services simulés, dialogues par-dessus la page, bouton qui simule le raccourci.
- **Vérifications sur Windows :** les clics autour de l'orbe atteignent l'application dessous ; le raccourci
  marche quand une autre application a le focus ; un second lancement montre l'orbe déjà ouvert ; la position
  revient au redémarrage ; coût de la fenêtre immobile (méthode de l'essai E6).

### 5. Style commun et langue (`stage1/5-shared-ui-language`)

- `Features/Shared/` : couleurs et polices embarquées de l'ADR 0011 (section 2) ; cadre des pages de
  réglages ; champ de secret (sert aux fournisseurs, aux voix cloud et plus tard aux connecteurs).
- Traductions (ADR 0008) : `.resx` et `IStringLocalizer`, anglais neutre, test des clés.
- Tranche `Settings/Language` : réglage `fr` ou `en`, proposé d'après la langue de Windows, appliqué sans
  redémarrer.
- **Vérification sur Windows :** les fenêtres ouvertes rechargent leurs textes au changement de langue dans
  `BlazorWebView`. Si ce n'est pas fiable, un nouvel ADR dit que l'interface change au prochain démarrage
  (ADR 0008).

### 6. Orbe, version légère (`stage1/6-orb`)

- Tranche `Orb` : un composant pour les trois hôtes, avec les entrées et sorties de l'ADR 0005 et la qualité
  de l'ADR 0011.
- Version légère d'abord : la plus simple à porter, et celle qui sert de repli. Les sept aspects de
  l'ADR 0011, fondus, image fixe dans les aspects qui l'exigent, mouvement réduit, cause de l'erreur au
  survol, anneau de progression et badge ambre des décisions (avec des données simulées).
- Le clic sur l'orbe publie l'activation (US1.6). Pas de double-clic ni de délai : l'éventail n'existe pas
  encore (ADR 0010, section 3).
- Page de démonstration dans l'hôte Web (US1.3) : les sept états, les tâches simulées, le badge, la veille
  animée en option.
- **Tests Playwright :** chaque aspect (le composant expose son aspect courant dans un attribut), le
  mouvement réduit, le clic.
- **Vérification sur Windows :** veille, micro coupé et erreur sous 2 % d'un cœur avec la version légère
  (ADR 0005, obligatoire) ; coût des phases animées relevé.

### 7. Orbe, version haute qualité (`stage1/7-orb-high-quality`)

- Portage du prototype haute qualité (mode 5) : sphère plasma et nuages binaires en Canvas 2D, anneaux
  gyroscopiques en SVG et CSS 3D, tracés générés une fois.
- Réglage « Qualité de l'orbe » (haute par défaut, légère), changé sans redémarrer.
- **Vérification sur Windows :** les mesures de l'ADR 0011 (section 8), pour les deux versions, écrites dans
  la mémoire du projet. Si la haute qualité saccade ou coûte nettement plus, le choix par défaut se
  rediscute avec Benoit.

### 8. Périphériques audio (`stage1/8-audio-devices`)

- Contrats de capture et de lecture (ADR 0003) ; mise en œuvre dans l'hôte Windows avec NAudio 3.3
  (`Span<float>`, voir les pièges) ; capture en 16 kHz mono, tampon circulaire en mémoire, jamais écrit sur
  disque.
- Choix du micro et de la sortie dans les paramètres.
- Coupure du micro (US1.13) : le flux est fermé, la condition « micro coupé » est posée, l'entrée du menu de
  la zone de notification devient active.
- Niveau sonore du micro vers l'orbe.
- **Vérifications sur Windows :** micro réel ; micro coupé, Windows n'indique plus d'application qui écoute ;
  micro débranché pendant que Jarvis tourne.

### 9. Mot d'éveil (`stage1/9-wake-word`)

- Tranche `Voice/WakeWord` (US1.5) : les trois modèles d'openWakeWord par ONNX Runtime, détection en
  continu, seuil réglable (0,5 au départ), indicateur de score en direct dans les réglages, une détection par
  activation, silencieux pendant l'écoute.
- **Vérifications sur Windows :** avec la voix de Benoit, le seuil retenu est écrit dans la mémoire du projet
  (ADR 0006, section 2) ; coût du mot d'éveil mesuré à part. Si sa prononciation ne passe pas même seuil
  baissé, on s'arrête et on pose la question (entraîner un modèle demande un nouvel ADR).

### 10. Transcription (`stage1/10-transcription`)

- Tranche `Voice/Transcription` (US1.7) : fin de phrase par Silero (sherpa-onnx), 500 ms de silence, délai
  sans parole qui ramène en veille ; Whisper.net avec CUDA ; modèle et appareil réglables (`large-v3-turbo`
  sur carte graphique par défaut, `small`, `base` sur processeur) ; langue imposée par le réglage ; modèle
  chargé une fois.
- La transcription est publiée pour la conversation.
- **Vérifications sur Windows :** fin de phrase avec un micro réel et un peu de bruit. Si Silero n'est pas
  fiable, repli sur un seuil d'énergie (ADR 0006, section 3), et la PR le dit.

### 11. Fournisseurs LLM (`stage1/11-llm-providers`)

- Tranche `Llm/Providers` (US1.8) : liste de fournisseurs (nom, type, adresse, modèle, options, référence de
  clé) ; adaptateurs `ollama`, `openai-compatible`/`openai` et `anthropic` sur `IChatClient` ; bouton
  « Tester la connexion » qui nomme la cause.
- Modèle local par défaut (ADR 0007, section 5) : `qwen3:14b`, réflexion coupée, contexte 4096,
  température 0,3, préchargé au démarrage et gardé en mémoire ; message « le modèle se charge » si Ollama l'a
  libéré.
- Les fournisseurs cloud se configurent et se testent ; ils ne répondent pas à l'étape 1.
- L'analyseur tolérant des appels d'outils attend l'étape 2, avec le premier outil.
- **Vérifications sur Windows :** Ollama sur le port 11435 et `llama-server` sur 8080 ; mesure d'un modèle
  de repli de 7 à 8 milliards de paramètres, à proposer par l'assistant du premier lancement (ADR 0007).

### 12. Synthèse et page de test des voix (`stage1/12-speech`)

- Tranche `Voice/Speech` (US1.10) : Piper par défaut (`fr_FR-tom-medium`, `en_GB-alan-medium`), Kokoro en
  option, sherpa-onnx **1.13.8** ; file de phrases jouées dans l'ordre ; arrêt net et file vidée sur
  interruption ; niveau sonore de la voix vers l'orbe.
- Voix cloud ElevenLabs et OpenAI derrière le même contrat, listées seulement si une clé est dans le coffre,
  avec l'avertissement que le texte quitte la machine (ADR 0009, section 9).
- Tranche `Voice/TestVoice` (US1.11) : voix par moteur et par langue, phrase d'essai modifiable, vitesse,
  voix par défaut de chaque langue.
- **Vérifications sur Windows :** qualité perçue des voix sur le haut-parleur réel ; délai de la carte son.

### 13. Conversation (`stage1/13-conversation`)

- Tranche `Conversation` : le tour complet par messages, sans qu'aucune tranche n'en référence une autre :
  activation, écoute, transcription, réponse du modèle en flux, découpage en phrases, synthèse dès la
  première phrase (US1.9).
- Interruption (US1.12) par le mot d'éveil, le raccourci ou le clic : génération annulée, parole coupée net,
  nouveau tour.
- Fenêtre de conversation (US1.14) : historique dans la base, zone de saisie, modèle qui a répondu à chaque
  tour, bouton « tout effacer » (ADR 0004).
- Ce qui est enregistré à chaque réponse : fournisseur, modèle, jetons, durée (ADR 0007, section 7).
- Personnalité (US1.15) : message système du majordome en français et en anglais, modifiable et
  réinitialisable par langue, plus les règles techniques fixes (ADR 0007, section 6).
- Une demande écrite est-elle aussi dite à voix haute : réponse à la question n° 3, que l'ADR 0005 renvoie à
  ce plan.
- **Test Playwright :** un tour complet simulé dans l'hôte Web, avec une interruption.
- **Vérifications sur Windows :** délai entre la fin de la phrase et le premier son, avec le micro et le
  haut-parleur réels (cible sous 2 s à chaud) ; mot d'éveil pendant que Jarvis parle. S'il n'est pas
  reconnu de façon fiable, l'interruption pendant la parole passe par le raccourci et le clic seulement, et un
  nouvel ADR le dit (ADR 0005).

### 14. Premier lancement (`stage1/14-first-run`)

- Tranche `Setup/FirstRun` (US1.4), lancée quand `config.json` n'existe pas : langue, micro, sortie audio,
  fournisseur et modèle local (avec le test de connexion), téléchargement des modèles dans `models\` avec leur
  taille affichée et leur empreinte vérifiée (ADR 0006, section 7).
- Replis proposés quand la mémoire graphique manque ou sans carte NVIDIA : Whisper `small` ou `base`, modèle
  local de repli mesuré au chantier 11.
- **Test Playwright :** tout l'assistant dans l'hôte Web, en français et en anglais.
- **Vérification sur Windows :** premier lancement sur un profil vide (dossier `.kazo\jarvis` renommé).

### 15. Pannes (`stage1/15-faults`)

- Chaque tranche pose déjà ses causes de panne en les construisant. Ce chantier fait le tour : micro absent,
  modèle injoignable, modèle absent, voix manquante, configuration illisible ; chaque cause a son texte, son
  action corrective et son lien vers le bon réglage (US1.17) ; la condition se lève quand la cause disparaît
  ou qu'un tour réussit.
- **Tests Playwright :** chaque cause simulée dans l'hôte Web.

### 16. Mises à jour (`stage1/16-updates`)

- Tranche `Updates` (US1.18) : comparaison de la version avec les versions publiées sur GitHub (jeton dans le
  coffre tant que le dépôt est privé), téléchargement, lancement du programme de mise à jour.
- Projet `Kazo.Jarvis.Updater` : remplace les fichiers et relance l'application, comme Kazo Bar.
- Script de publication dans `build/` : autonome, `win-x64`, l'application et le programme de mise à jour
  dans le même dossier (ADR 0001).
- **Vérification sur Windows :** une mise à jour de bout en bout entre deux versions de test.

### 17. Recette de l'étape (`stage1/17-acceptance`)

- Sur le PC de Benoit, le but de l'étape en français puis en anglais : « hey jarvis », une question, la
  réponse à voix haute.
- Vérifier qu'aucune connexion ne sort de la machine avec la configuration par défaut.
- Tableau des mesures finales dans la mémoire du projet : délai, processeur en veille et en phase animée
  pour les deux orbes, mémoire graphique, seuil du mot d'éveil.
- `features.md` passe l'étape 1 dans « Disponibles », la mémoire du projet dit que la série est finie.
- Benoit fusionne `stage1/base` dans `main`.

## Vérifications sur Windows, en un coup d'œil

Les vérifications que l'hôte Web ne peut pas faire, rangées par chantier. Elles reprennent la liste « À
vérifier au début de l'étape 1 » de la mémoire du projet.

| Vérification | Chantier |
| --- | --- |
| Clics autour de l'orbe vers l'application dessous | 4 |
| Raccourci global quand une autre application a le focus | 4 |
| Rechargement des fenêtres au changement de langue | 5 |
| Orbe en veille sous 2 % d'un cœur, les deux versions | 6, 7 |
| Micro réel, coupure réelle | 8 |
| Mot d'éveil avec la voix de Benoit, seuil retenu | 9 |
| Fin de phrase par Silero avec un micro réel | 10 |
| Ollama sur 11435, modèle de repli mesuré | 11 |
| Qualité des voix, délai de la carte son | 12 |
| Délai de bout en bout sous 2 s ; mot d'éveil pendant la parole | 13 |
| Premier lancement sur un profil vide | 14 |
| Mise à jour de bout en bout | 16 |
| Aucune connexion sortante par défaut | 17 |

## Questions posées à Benoit

Posées dans la conversation le 2026-11-19 ; ses réponses seront écrites ici.

1. **Base de la série.** Recommandé : Benoit fusionne `spikes/base` dans `main`, puis `stage1/base` part de
   `main`, comme le veut AGENTS.md.
2. **Intégration continue.** Recommandé : GitHub Actions sur Linux (construction de tous les projets, tests
   unitaires hors Windows, Playwright contre l'hôte Web) ; les tests marqués Windows et les vérifications
   restent sur le PC de Benoit.
3. **Réponse à une demande écrite.** Recommandé : dite à voix haute seulement si la demande était parlée ;
   une demande écrite reçoit une réponse écrite.
4. **Qui fait les vérifications sur Windows.** Recommandé : un agent sur le PC de Benoit (Contrôle à
   distance), Benoit seulement pour ce qui demande sa voix ou ses oreilles (chantiers 9, 10, 12, 13, 17).

## Hors de cette série

Outils, MCP, passage de main, Claude, sessions Claude Code, éventail des tâches (ADR 0010) et essai E11 :
étape 2. Tâches planifiées, déclencheurs, Google : étape 3. Application mobile : étape 4. Plein écran
« poste de commande » : pas encore planifié.
