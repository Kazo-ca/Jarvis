# Pannes

Chantier `stage1/15-faults` (US1.17, [plan de l'étape 1](plans/stage-1.md#15-pannes-stage115-faults),
[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md), sections 1 et 3). English:
[faults.md](../../en/dev/faults.md).

## Ce qui est construit

Chaque tranche pose et lève ses propres causes de panne ; la nouvelle tranche `Assistant/Faults` les nomme, dit quoi
faire, ouvre le bon réglage, et pose la seule cause qu'aucune tranche ne possède : un fichier de réglages inutilisable.
La condition « erreur » de l'ADR 0005 n'a pas changé : une seule panne à la fois, la dernière posée, levée quand sa
cause disparaît ou qu'un tour réussit.

### Noyau

| Élément | Rôle |
| --- | --- |
| `FaultOrigin` (`Kernel/Assistant`) | La partie de Jarvis qui a trouvé la panne : micro, réglages, mot d'éveil, transcription, modèle de langage, voix. Une même cause vient de plusieurs endroits (Whisper, Piper ou Ollama sans modèle donnent tous `ModelMissing`) : l'origine choisit le bon texte et le bon réglage. |
| `AssistantFault.Origin`, `AssistantFault.Matches` | L'origine, facultative (sans elle, l'origine habituelle de la cause s'applique). `Matches` dit si une cause levée désigne cette panne. |
| `FaultResolved(Cause, Origin)` | L'origine est facultative : donnée, elle ne lève que la panne de cette origine (Whisper retrouvé ne lève pas une voix absente). |
| `SettingsWindowRequested(PageKey)` | La fenêtre des paramètres s'ouvre sur la page demandée, ou passe à cette page si elle est déjà ouverte. |
| `FaultWindowRequested` (`Messages/Desktop`) | L'entrée « Résoudre le problème… » de la zone de notification. |

La table des transitions n'a pas changé : aucun nouvel état, donc pas de nouvel ADR (ADR 0005, « Conséquences »).

### Tranche `Features/Assistant/Faults`

- **`FaultAdvice`** : pour chaque cause et origine, le titre, l'explication, l'action corrective et la page des
  paramètres qui règle la panne.

| Cause (origine) | Titre | Page ouverte |
| --- | --- | --- |
| `MicrophoneMissing` | Micro absent | Audio (`Audio.Devices`) |
| `ModelUnreachable` (modèle de langage) | Modèle de langage injoignable | Modèles de langage (`Llm.Providers`) |
| `ModelMissing` (modèle de langage) | Modèle de langage absent | Modèles de langage |
| `ModelMissing` (transcription) | Modèle de transcription absent | Modèles vocaux (`Setup.Models`) |
| `ModelMissing` (voix) | Voix absente | Modèles vocaux |
| `ModelMissing` (mot d'éveil) | Mot d'éveil absent | Mot d'éveil (`Voice.WakeWord`) |
| `VoiceMissing` | Voix muette | Voix (`Voice.TestVoice`) |
| `ConfigurationUnreadable` | Réglages illisibles (illisible, version plus récente, migration ratée) | aucune : le texte nomme `config.json` et dit quoi faire |

- **`FaultWindow`** (fenêtre « Problème de Jarvis ») : la panne en cours, son explication, « À faire : », le bouton
  « Ouvrir la page « … » » et le détail technique. Elle suit l'état : quand la cause disparaît, elle dit « Le problème
  est réglé ».
- **`FaultWindowOpener`** : ouvre la fenêtre sur `FaultWindowRequested`, et quand une activation est refusée faute de
  micro (`ActivationRefused` avec `MicrophoneMissing`) : un clic sur l'orbe ou le raccourci qui ne fait rien dit
  pourquoi. Un micro coupé n'ouvre rien : c'est un choix, et l'orbe le montre.
- **`ConfigurationFaultReporter`** : au démarrage, si `config.json` ne peut pas s'enregistrer (illisible, écrit par une
  version plus récente, migration ratée), pose `ConfigurationUnreadable` avec la raison.

### Qui pose et qui lève chaque cause

| Cause | Posée par | Levée par |
| --- | --- | --- |
| Micro absent | `Audio/Devices` (`MicrophoneService`) | le même, dès qu'un micro répond |
| Modèle injoignable | `Conversation/Dialogue` (tour raté) ; `Llm/Providers` après **deux** chargements ratés de suite (`LanguageModelFaultWatcher`) | un chargement réussi (`LanguageModelFaultWatcher`), un tour réussi |
| Modèle de langage absent | les mêmes, **dès le premier** chargement raté | les mêmes |
| Whisper absent | `Voice/Transcription` (tour raté) | Whisper chargé (`TranscriptionFaultWatcher`), un tour réussi |
| Voix muette ou absente | `Voice/Speech` (tour raté) | une voix entendue jusqu'au bout dans la page « Voix » (`VoiceTrial`), un tour réussi |
| Réglages illisibles | `Assistant/Faults` au démarrage | un démarrage sur un fichier lisible, un tour réussi |

Les modèles du mot d'éveil absents ne sont toujours pas une panne (choix du chantier 9) : la page « Mot d'éveil » le
dit. La ligne du tableau `FaultAdvice` sert si un jour une tranche la pose.

### Hôtes

- **Windows** : l'entrée « Résoudre le problème… » de la zone de notification n'apparaît que pendant une panne, en
  tête du menu.
- **Web** : un bouton par cause simulée (serveur du modèle arrêté, modèle de langage supprimé, fichier Whisper
  supprimé, voix cassée, `config.json` illisible), le bouton « Solve the problem (tray menu) », et `#fault` qui affiche
  la cause et l'origine en cours. Le connecteur simulé a `ForcedFailure`, la transcription simulée `Missing`, les
  réglages en mémoire `Simulate`.

## Choix provisoires de ce chantier

À confirmer par Benoit ; chacun se change en une ligne :

1. **Où la panne se lit** : une fenêtre « Problème de Jarvis », ouverte par l'entrée de la zone de notification (visible
   seulement pendant une panne) et toute seule quand un clic ou le raccourci est refusé faute de micro. Rien ne s'ouvre
   tout seul pour les autres causes : l'orbe rouge et son survol suffisent. Pas de bulle de notification.
2. **Serveur injoignable au démarrage** : la panne ne se pose qu'après deux chargements ratés de suite (au moins 15 s),
   pour que l'orbe ne rougisse pas à chaque ouverture de session pendant que Docker démarre Ollama. Un modèle absent se
   pose tout de suite.
3. **La page « Voix » lève la panne de la voix** quand une voix y est entendue jusqu'au bout.
4. **Réglages illisibles** : aucun bouton (aucune page ne règle un fichier que Jarvis refuse d'écraser) ; le texte dit
   de corriger ou renommer `config.json` puis de relancer. Pas de bouton « Repartir des réglages par défaut » : ce
   serait effacer un fichier, à décider par Benoit.
5. **Origine dans le noyau** (`FaultOrigin`) plutôt que des clés de page dans les messages : les tranches ne nomment
   que ce qu'elles sont, `Assistant/Faults` décide du texte et de la page.
6. **Textes** : ceux des fichiers `FaultWindow.resx` et `FaultWindow.fr.resx` ; l'action du modèle injoignable renvoie
   au bouton « Tester la connexion » de la page des modèles de langage, qui nomme la cause précise.

## Tests

- Unitaires : `FaultsTests` (trois textes pour chaque cause et origine dans les deux langues, chaque page nommée existe,
  la bonne page par cause, fenêtre ouverte par l'entrée et par un refus faute de micro, page demandée choisie dans la
  fenêtre des paramètres, `config.json` illisible ou plus récent posé au démarrage), `LanguageModelFaultWatcherTests`,
  `TranscriptionFaultWatcherTests`, `VoiceTrialFaultTests`, et la machine à états (une origine ne lève que la sienne).
- Playwright (`FaultsTests`) : chaque cause simulée dans l'hôte Web, son titre, son action, le bouton qui ouvre la
  bonne page, puis la panne levée par la disparition de la cause ou par un tour réussi.
- Captures (français) dans les fichiers du projet, dossier `stage1-15/`.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit :

1. Débrancher le micro : l'orbe rougit, l'entrée « Résoudre le problème… » apparaît en tête du menu de la zone de
   notification ; `Ctrl+Alt+J` ouvre la fenêtre « Problème de Jarvis » ; « Ouvrir la page « Audio » » ouvre les
   paramètres sur Audio ; rebrancher : la fenêtre dit « Le problème est réglé » et l'entrée disparaît.
2. Arrêter le conteneur Ollama, relancer Jarvis : l'orbe ne rougit qu'après environ 15 s ; redémarrer Ollama : l'orbe
   redevient bleue au prochain essai de chargement (de 15 s à 5 min selon le nombre d'échecs) ou au prochain tour.
3. Ouvrir une session Windows avec Docker qui démarre Ollama : l'orbe ne doit pas rougir.
4. Écrire un `config.json` invalide (copie de côté), relancer : panne « Réglages illisibles » ; remettre le fichier.
