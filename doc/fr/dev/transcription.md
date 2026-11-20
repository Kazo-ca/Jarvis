# Transcription

Chantier `stage1/10-transcription` (US1.7, ADR [0006](adr/0006-voice-chain.md), sections 3 et 4) : la fin de
phrase et la reconnaissance par Whisper. English: [../../en/dev/transcription.md](../../en/dev/transcription.md).

## Ce qui est construit

```
TurnStarted (voix) ─► UtteranceListener ─► détecteur de voix (Silero ou seuil d'énergie)
                            │                    ─► UtteranceEndpointer : 500 ms de silence ?
                            │
                            ├─► NothingHeard            (aucune parole avant le délai : retour en veille)
                            └─► UtteranceEnded ─► RecognizerKeeper (Whisper) ─► UtteranceTranscribed ─► conversation (chantier 13)
```

### Noyau (`Kernel/Voice`, `Kernel/Messages/Voice`)

| Type | Rôle |
| --- | --- |
| `ISpeechRecognition`, `ISpeechRecognizer` | Contrat natif : charger un modèle Whisper (`RecognizerSpec` : modèle et appareil), puis transcrire un énoncé en 16 kHz mono dans la langue de l'assistant. Le module dit où il tourne vraiment (`Device`) : le processeur si CUDA n'a pas pu se charger. |
| `IVoiceActivityDetection`, `IVoiceActivityDetector` | Contrat natif : dire, trame par trame, si quelqu'un parle (Silero, 512 échantillons soit 32 ms). |
| `SpeechRecognitionException` | Cause nommée : modèle absent (avec le chemin attendu), chargement refusé, transcription en échec. |
| `UtteranceTranscribed` | Ce que l'utilisateur a dit, pour la conversation : tour, texte, langue, durée de l'audio, durée de la transcription. Texte vide quand Whisper n'a reconnu aucun mot. |

### Tranche `Features/Voice/Transcription`

- **`UtteranceListener`** reçoit `TurnStarted` (tour parlé seulement) et s'abonne au flux du micro
  (`IMicrophoneSource`). Le fil de capture ne fait que copier les blocs dans un canal ; le détecteur et Whisper
  tournent sur un fil du pool.
  - **`UtteranceEndpointer`** compte en échantillons, pas en temps d'horloge : la même voix donne toujours la
    même décision. La parole commence après 150 ms de voix sans pause (un clic ou une toux ne compte pas), la
    phrase se termine après le silence réglé (500 ms), elle est coupée après 25 s, et sans parole après 8 s,
    `NothingHeard` ramène en veille.
  - L'audio transcrit va de 300 ms avant le début de la parole (les détecteurs répondent un peu en retard) à la
    fin du silence. Rien avant l'activation : le « Hey Jarvis » n'est pas transcrit.
  - Sans aucun échantillon pendant 3 s (micro débranché), l'écoute s'arrête : ce qui a été dit est transcrit,
    sinon rien n'a été entendu.
  - Un `TurnCancelled` (raccourci, clic, micro coupé, interruption pendant la transcription) arrête tout et
    rien n'est publié.
- **Détecteur :** Silero par défaut. Si l'hôte n'en fournit pas ou si le modèle manque, le **seuil d'énergie**
  (`EnergyVoiceActivityDetector`, le repli de l'ADR 0006) prend le relais et le journal le dit une fois. Le
  seuil compare chaque trame de 30 ms au bruit de fond, le niveau le plus bas des 3 dernières secondes (−55 dB
  supposés au départ), avec 12 dB de marge et −50 dB au minimum.
- **`RecognizerKeeper`** charge le modèle au démarrage, le recharge quand le modèle ou l'appareil change
  (l'ancien est libéré d'abord : la carte graphique n'a qu'environ 600 Mio de marge), et cherche de nouveau un
  modèle absent avant chaque transcription : le fichier peut être déposé sans redémarrer. Une transcription à
  la fois.
- **`TranscriptText`** retire les marqueurs de Whisper (`[BLANK_AUDIO]`, `(musique)`) et les segments dont la
  probabilité « pas de parole » dépasse 0,8 (Whisper invente volontiers une phrase sur du bruit).
- **Échecs :** modèle Whisper absent ou refusé → `TurnFailed` avec la cause `ModelMissing` et le chemin
  attendu ; l'orbe passe en erreur jusqu'au prochain tour réussi.
- **Page des paramètres « Transcription »** (`Voice.Transcription`, après la page « Audio ») : modèle et
  appareil avec l'état du chargement, détecteur, silence de fin de phrase, attente de la parole, et la
  **dernière demande entendue** avec ses mesures (durée de l'audio, durée de la transcription, détecteur). Elle
  sert à essayer le micro et le modèle avant le chantier 13.
- Réglages : section `Voice.Transcription` de `config.json` (`model`, `device`, `detector`,
  `silenceMilliseconds`, `noSpeechTimeoutSeconds`, `maxUtteranceSeconds`).

### Adaptateurs (`Kazo.Jarvis.Adapters/Voice`)

- `WhisperSpeechRecognition` : Whisper.net 1.9.1, bibliothèque native CUDA d'abord puis processeur (comme
  l'essai E4). Le paquet `Whisper.net.Runtime.Cuda` est ajouté par l'hôte Windows seulement ; `Adapters` apporte
  celui du processeur. La langue est fixée à la construction du processeur Whisper, reconstruit quand elle
  change.
- `SileroVoiceActivityDetection` : sherpa-onnx 1.13.8 (déjà fixé pour la synthèse). Silero suit la voix de près
  (`MinSilenceDuration` et `MinSpeechDuration` à 0,1 s) ; c'est la tranche qui compte le silence.

### Modèles

Aucun modèle dans le dépôt (ADR 0006, section 7). L'assistant du premier lancement (chantier 14) les
téléchargera ; d'ici là, ils se copient à la main dans le dossier des modèles :

| Fichier | Où le mettre | Source |
| --- | --- | --- |
| `ggml-large-v3-turbo-q5_0.bin`, `ggml-small.bin`, `ggml-base.bin` | `%USERPROFILE%\.kazo\jarvis\models\whisper\` | `ggerganov/whisper.cpp` sur Hugging Face |
| `silero_vad.onnx` | `%USERPROFILE%\.kazo\jarvis\models\vad\` | Version `asr-models` de `k2-fsa/sherpa-onnx` sur GitHub |

### Hôte Web

`SimulatedSpeechRecognition` (« What time is it? » ou « Quelle heure est-il ? » selon la langue, en 200 ms) et
`SimulatedVoiceActivity` à la place de Silero : le micro simulé joue un son en continu, c'est le bouton « Say a
phrase » qui « parle » pendant 1,2 s. La dernière transcription s'affiche sur la page.

## Choix provisoires de ce chantier

L'ADR 0006 ne les tranche pas ; à confirmer par Benoit :

1. **Attente de la parole de 8 s** après l'activation, puis retour en veille.
2. **Énoncé le plus long : 25 s**, coupé puis transcrit (le tampon du micro garde 30 s).
3. **150 ms de voix** avant de considérer que la parole a commencé.
4. **300 ms gardées avant la parole**, rien avant l'activation.
5. **Silero absent ou illisible : repli silencieux sur le seuil d'énergie** (journal seulement, pas de panne).
6. **Modèle Whisper absent : pas de panne au démarrage**, seulement à la fin d'un tour (`TurnFailed`,
   `ModelMissing`). L'état est visible sur la page.
7. **Texte vide publié tel quel** quand Whisper ne reconnaît aucun mot : la conversation (chantier 13) termine
   alors le tour sans réponse. D'ici là, Jarvis reste en « réflexion » après chaque phrase ; le raccourci
   l'interrompt.
8. Segments écartés au-delà de **0,8** de probabilité « pas de parole ».
9. Modèles dans `models\whisper\` et `models\vad\`.

## Ce qui a été essayé ici

- Silero réel (sherpa-onnx 1.13.8 sous Linux) sur l'enregistrement `jfk.wav` de whisper.cpp, entouré de bruit
  léger : début de la parole trouvé à 1,18 s, 0,17 ms par trame. **Avec 500 ms, la phrase s'est terminée à la
  première pause naturelle de l'orateur (3,9 s)** : à surveiller avec la voix de Benoit, le réglage monte
  jusqu'à 3 s. Le seuil d'énergie, lui, n'a pas trouvé ces pauses (bruit de l'enregistrement au-dessus du
  seuil) : Silero reste le bon choix par défaut.
- Whisper réel n'a pas pu tourner dans le conteneur (Hugging Face inaccessible) : il est couvert par les
  vérifications sur Windows.

## Tests

- Unitaires : `UtteranceEndpointerTests` (silence, pause, clic, délai, coupure), `TranscriptionPiecesTests`
  (seuil d'énergie, nettoyage du texte, réglages), `RecognizerKeeperTests` (chargement unique, changement,
  modèle absent puis trouvé, CUDA indisponible), `UtteranceListenerTests` (tour complet avec la vraie machine à
  états, rien entendu, annulation, interruption pendant la transcription, repli sans Silero, modèle absent,
  tour écrit), `VoiceAdaptersTests` (chemins des modèles, causes nommées, enregistrement).
- Playwright (`TranscriptionTests`) : raccourci, phrase simulée, passage en « réflexion », texte affiché sur la
  page et dans les paramètres.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (lui seulement pour sa voix) :

1. **Modèles déposés** (tableau ci-dessus) : la page « Transcription » affiche « Modèle chargé, exécuté sur :
   Carte graphique (CUDA) ». Noter la durée du chargement au journal.
2. **Fin de phrase avec un micro réel et un peu de bruit** (télévision, ventilateur) : `Ctrl+Alt+J`, une
   demande courte, une longue avec des pauses. La phrase ne doit pas être coupée au milieu ni attendre trop.
   Ajuster le silence si besoin ; **si Silero n'est pas fiable, essayer « Seuil d'énergie » et le noter**
   (ADR 0006, section 3).
3. **Rien dit après l'activation** : retour en veille après 8 s.
4. **Texte reconnu** en français puis en anglais (changer la langue) ; noter la durée de transcription affichée
   (cible de l'essai E4 : 0,43 à 0,68 s avec `large-v3-turbo`).
5. **Repli :** `small` sur la carte graphique, `base` sur le processeur ; l'appareil affiché suit.
6. **Interruption** pendant la transcription (raccourci) : aucune trace du texte de l'ancien tour au journal.
