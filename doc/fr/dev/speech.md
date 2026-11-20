# Synthèse vocale et page des voix

Chantier `stage1/12-speech` (US1.9 à US1.12, ADR [0006](adr/0006-voice-chain.md), section 5, et
[0009](adr/0009-permission-and-confirmation-model.md), section 9) : Jarvis dit ses réponses à voix haute, et
la page « Voix » des paramètres fait écouter et choisir la voix de chaque langue.
English: [../../en/dev/speech.md](../../en/dev/speech.md).

## Ce qui est construit

```
conversation (chantier 13) ─► SentenceToSpeak, AnswerSentencesEnded ─► SpeechPlayer (tranche Voice/Speech)
                                                                          │ IVoiceSelection : voix de la langue
                                                                          ▼
                                     ISpeechEngine (Piper, Kokoro, ElevenLabs, OpenAI) ─► AudioClip
                                                                          │
               FirstSentenceReady, SpeechEnded ◄──────────────────────────┤
                                     IAudioPlayback (sortie de la page Audio) ◄─┤
                                     ISoundLevelSink (voix) ─► orbe ◄────────────┘
page « Voix » (tranche Voice/TestVoice) ─► IVoiceSelection + ISpeechEngine + IAudioPlayback (même contrat)
```

### Noyau (`Kernel/Speech`, `Kernel/Messages/Speech`)

| Type | Rôle |
| --- | --- |
| `ISpeechEngine` | Contrat natif d'un moteur : son nom (`piper`, `kokoro`, `elevenlabs`, `openai`), s'il envoie le texte hors du PC, ses voix utilisables maintenant, la synthèse d'une phrase en `AudioClip`. Plusieurs moteurs sont inscrits. |
| `SpeechVoice`, `VoiceChoice` | Une voix (moteur, identifiant, nom, langue ou `null` si multilingue, sortie du PC) ; le choix gardé dans les réglages (moteur, voix, vitesse de 0,5 à 2). |
| `SpeechException`, `SpeechFailure` | La cause d'un échec : modèle absent, clé absente ou refusée, service injoignable, voix inconnue, autre. |
| `IVoiceSelection` | La voix de chaque langue, fournie par la tranche `Voice/Speech` qui possède le réglage ; lue et changée par la page « Voix » (le premier lancement n'a pas d'étape « Voix » : les voix Piper des deux langues restent celles par défaut). |
| `SpeechKeys` | Références des clés des voix dans le coffre : `voice.elevenlabs.apikey`, `voice.openai.apikey`. |
| `SentenceToSpeak`, `AnswerSentencesEnded` | Ce que la [conversation](conversation.md) publie : chaque phrase complète dans l'ordre, puis la fin de la réponse. |

### Tranche `Features/Voice/Speech`

- **`SpeechPlayer`** reçoit les phrases d'un tour, les synthétise et les joue dans l'ordre ; la phrase
  suivante est synthétisée pendant que la précédente joue (une d'avance). La voix est fixée à la première
  phrase du tour, d'après la langue de l'assistant.
  - Premier son : `FirstSentenceReady` (l'orbe passe en « parle »). Dernier son après `AnswerSentencesEnded` :
    `SpeechEnded`. Réponse sans phrase : `AnswerFinishedWithoutSpeech`.
  - **Interruption** (`TurnCancelled`, US1.12) : le son s'arrête net (`IAudioPlayback.StopAll`), les
    phrases en attente sont jetées, le niveau retombe à zéro. Les messages tardifs du tour sont ignorés.
  - **Échec** : `TurnFailed` avec la panne `ModelMissing` (fichiers du modèle absents, aucune voix pour la
    langue) ou `VoiceMissing` (clé refusée, service injoignable, aucune sortie audio).
  - **Niveau de la voix vers l'orbe** : l'enveloppe du clip est calculée avant de jouer (`VoiceEnvelope`,
    une valeur par 1/30 s, en décibels de −60 à 0 comme le micro), puis envoyée au rythme du son.
- **`VoiceSelection`** (`IVoiceSelection`) : réglage `Voice.Speech` de `config.json` (voix et vitesse par
  langue). Voix utilisée : celle choisie si elle est là ; sinon la voix Piper par défaut ; sinon une voix
  locale de la langue. **Jamais une voix dans le nuage que l'utilisateur n'a pas choisie** (ADR 0009,
  section 9).
- Voix par défaut (ADR 0006) : `piper:fr_FR-tom-medium` et `piper:en_GB-alan-medium`, vitesse 1.

### Tranche `Features/Voice/TestVoice`

- **Page « Voix »** des paramètres (`Voice.TestVoice`, après « Audio ») : langue (français ou anglais), voix
  de la langue groupées « Sur cet ordinateur » et « Dans le nuage », phrase d'essai modifiable (une par
  langue), vitesse de 0,5 à 2, boutons « Écouter », « Arrêter », « Utiliser pour le français ».
- Une voix dans le nuage affiche que **le texte de chaque réponse est envoyé** au service (ADR 0009,
  section 9). Les clés ElevenLabs et OpenAI se saisissent sur la même page (champ de secret commun) ; leurs
  voix apparaissent dès que la clé est enregistrée.
- La logique est dans `VoiceTrial` (C# d'abord) ; « Arrêter » ne coupe que la phrase de la page, pas la voix
  de Jarvis.

### Adaptateurs (`Kazo.Jarvis.Adapters/Speech`)

| Moteur | Ce qu'il lit | Remarques |
| --- | --- | --- |
| `PiperSpeechEngine` | Tout dossier `models\vits-piper-*` complet (`.onnx`, `tokens.txt`, `espeak-ng-data`) | sherpa-onnx 1.13.8, 4 fils. Toute voix Piper déposée là est listée, nommée d'après son fichier (`fr_FR-tom-medium` donne « Tom (fr_FR, medium) »). |
| `KokoroSpeechEngine` | `models\kokoro-multi-lang-v1_0` | `ff_siwis` en français, quelques voix anglaises (`bm_george`, `bm_lewis`, `bm_daniel`, `bf_emma`, `am_michael`, `af_heart`). Un modèle chargé par langue. |
| `ElevenLabsSpeechEngine` | Clé `voice.elevenlabs.apikey` | Voix du compte (`GET /v1/voices`, relu toutes les 10 min), modèle `eleven_flash_v2_5`, son brut 16 bits à 24 kHz, vitesse ramenée entre 0,7 et 1,2. |
| `OpenAiSpeechEngine` | Clé `voice.openai.apikey` | `gpt-4o-mini-tts`, 11 voix fixes, son brut 16 bits à 24 kHz. |

- Un modèle local est chargé à sa première phrase et gardé en mémoire ; ses générations se suivent (une
  phrase à la fois par modèle).
- Les voix dans le nuage sont multilingues et ne sont listées qu'avec une clé dans le coffre. Délai d'une
  phrase borné à 30 s.

### Hôte Web

`SimulatedSpeechEngine` se fait passer pour Piper (`fr_FR-tom-medium`, `fr_FR-siwis-medium`,
`en_GB-alan-medium`) et produit un bourdonnement dont la durée suit le texte ; `SimulatedCloudSpeechEngine`
liste « Simulated Nova » quand la clé OpenAI des voix est dans le coffre. La page d'accueil a un bouton
« Simulate a spoken answer », qui joue un tour sur la voix seule (sans transcription ni modèle), et affiche la dernière phrase
dite (`#voice-spoken`).

## Essai réel dans le conteneur

Piper `fr_FR-tom-medium` a été chargé et lu par `PiperSpeechEngine` sous Linux le 20 novembre 2026 (modèle
téléchargé hors du dépôt) : 3,4 s de son à 44,1 kHz en 1,3 s pour la première phrase (chargement compris),
0,47 s ensuite, 2,6 s de son à la vitesse 1,5. Ni Kokoro ni les voix dans le nuage n'ont été essayés pour de
vrai.

## Choix provisoires de ce chantier

L'ADR 0006 ne les tranche pas ; à confirmer par Benoit :

1. **Le premier son fait passer l'orbe en « parle »** : c'est la tranche de la voix qui publie
   `FirstSentenceReady`, pas la conversation.
2. **Une voix par tour**, choisie à la première phrase : un changement de réglage s'applique au tour suivant.
3. **Une voix dans le nuage qui échoue** (clé refusée, pas de réseau) **fait échouer le tour** avec la panne
   « voix absente » ; Jarvis ne bascule pas tout seul sur une voix locale au milieu d'une réponse.
4. **Voix choisie absente** (modèle supprimé, clé retirée) : la voix Piper par défaut, ou une autre voix
   locale de la langue, parle à sa place, avec une ligne au journal.
5. **Vitesse par langue**, enregistrée avec la voix, de 0,5 à 2 (ElevenLabs n'accepte que 0,7 à 1,2).
6. **Clé OpenAI des voix séparée** de celle d'un fournisseur de modèle OpenAI.
7. ElevenLabs avec `eleven_flash_v2_5` (le plus rapide) ; OpenAI avec `gpt-4o-mini-tts`.
8. Voix Kokoro anglaises proposées : six voix du modèle, avec la prononciation américaine de l'essai E4.
9. Phrases d'essai : « Bonsoir, Monsieur. Tous les systèmes sont opérationnels. » et « Good evening, sir. All
   systems are operational. »

## Tests

- Unitaires : `SpeechPlayerTests` (ordre, premier son, fin, interruption, échecs, tours anciens, niveau, avec
  la vraie machine à états), `VoiceSelectionTests`, `SpeechOptionsTests`, `VoiceEnvelopeTests`,
  `VoiceTrialTests` (page), `LocalSpeechEnginesTests` (dossiers de modèles), `CloudSpeechEnginesTests`
  (requêtes, son brut, causes d'échec, sans réseau).
- Playwright (`VoicesTests`) : page « Voix » dans les deux langues, voix par défaut enregistrée, voix dans le
  nuage et son avertissement, réponse parlée, interruption.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (**ses oreilles**, l'hôte Web ne les prouve pas). Modèles à placer
dans `%USERPROFILE%\.kazo\jarvis\models` (sources dans l'[ADR 0006](adr/0006-voice-chain.md), section 7) :

1. **Qualité perçue** des voix Piper `fr_FR-tom-medium` et `en_GB-alan-medium` sur le haut-parleur réel,
   puis sur le casque ; Kokoro `ff_siwis` et `bm_george` si installé ; une voix ElevenLabs ou OpenAI si une
   clé est fournie.
2. **Vitesse** : 0,8, 1 et 1,3, garder celle qu'il préfère pour chaque langue.
3. **Délai de la carte son** : entre le clic sur « Écouter » et le premier son, à chaud (deuxième essai).
   Noter le chiffre dans la mémoire.
4. **Arrêt net** : « Arrêter » pendant une longue phrase coupe le son sans craquement.
5. La page n'affiche aucune voix pour une langue dont le modèle manque, et le dit.
