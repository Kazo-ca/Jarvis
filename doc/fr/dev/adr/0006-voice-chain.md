# ADR 0006 : chaîne vocale (mot d'éveil, reconnaissance, synthèse)

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 15, 16, 17 et 18 de la [conception](../plans/design.md) ; récits US1.5, US1.7,
  US1.9 à US1.13 ; essais E4 et E5 ([résultats](../../../../spikes/e4-voice-chain/RESULTS.md)) ; décisions
  n° 3 et 4 des [essais](../spikes.md#décisions-prises-le-18-novembre-2026) (Piper par défaut, mot d'éveil à
  régler avec la voix de Benoit).
- **Complète :** [ADR 0003](0003-hosts-and-native-services.md) (capture et lecture audio),
  [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (phases et interruption).
- **Complété par :** [ADR 0007](0007-llm-providers-and-tool-format.md) (modèle local),
  [ADR 0008](0008-languages-and-localization.md) (langue de la reconnaissance et de la voix).

## Contexte

- L'essai E4 a fait tourner toute la chaîne en C#, sans Python : mot d'éveil openWakeWord porté en C#,
  Whisper.net, modèle local, Piper et Kokoro par sherpa-onnx.
- Délai mesuré entre la fin de la phrase et le premier son : **1,6 à 1,8 s** à chaud, avec Whisper
  large-v3-turbo sur la carte graphique, qwen3 14B et Piper. La cible de 2 s est tenue.
- Deux réserves : un « hey jarvis » prononcé à la française par une voix de synthèse n'est pas reconnu
  (score de 0,04 à 0,16, contre 0,997 en anglais), et Kokoro sur le processeur ajoute 1,3 s.
- Essai E5 : Whisper turbo et le modèle 14B tiennent ensemble sur la RTX 3060, avec 11,4 à 11,7 Go occupés
  sur 12,3. Sur le processeur, seul Whisper `base` reste supportable (1,05 à 1,1 s de transcription).
- L'essai n'a utilisé ni micro réel, ni haut-parleur : les phrases étaient fabriquées par la synthèse.

## Décision

### 1. Vue d'ensemble

```
micro ─► tampon circulaire ─► mot d'éveil ─► fin de phrase ─► reconnaissance ─► modèle (ADR 0007)
                                                                                     │ phrase par phrase
haut-parleur ◄─ lecture ◄─ synthèse ◄────────────────────────────────────────────────┘
```

- Chaque maillon est un contrat du noyau, avec un adaptateur réel et un simulé. Les tranches de la voix
  s'enchaînent par les messages de l'ADR 0005 : aucune ne référence l'autre.
- Capture en 16 kHz mono, le format attendu par le mot d'éveil et par Whisper.
- **Le tampon d'écoute reste en mémoire et n'est jamais écrit sur disque.** Quand le micro est coupé
  (US1.13), le flux de capture est fermé : aucun son n'est capté.

### 2. Mot d'éveil

- Modèle **« hey_jarvis_v0.1 » d'openWakeWord**, avec ses deux modèles amont (spectrogramme et empreintes),
  exécutés par ONNX Runtime sur le processeur. Coût mesuré : 1,3 à 2 % du temps réel sur un cœur.
- Seuil par défaut 0,5, réglable. Un indicateur montre le score en direct (US1.5).
- **Le seuil se règle avec la voix de Benoit pendant l'étape 1.** Si sa prononciation ne passe pas, on baisse
  le seuil. Si cela ne suffit pas, entraîner un modèle est une décision à part, par un nouvel ADR ; le
  raccourci et le clic restent utilisables entre-temps (US1.6).
- Une seule détection par appel : le détecteur se tait pendant l'écoute.
- Le même mot d'éveil sert en français et en anglais (hypothèse n° 9 de la conception).

### 3. Fin de phrase

- La fin de phrase est détectée par le silence (US1.7), avec un détecteur d'activité vocale Silero exécuté
  par sherpa-onnx, déjà présent pour la synthèse.
- Valeurs de départ, réglables : 500 ms de silence terminent la phrase ; sans parole après l'activation,
  l'écoute s'arrête après un délai et revient en veille.
- **Non prouvé par l'essai E4**, qui supposait 500 ms sans détecteur. C'est la première vérification de
  l'étape 1 avec un micro réel. Repli : un seuil d'énergie simple.

### 4. Reconnaissance

- **Whisper local par Whisper.net**, avec l'exécution CUDA quand une carte NVIDIA est présente.
- Modèle et appareil réglables (US1.7) :

| Modèle | Appareil | Transcription mesurée | Usage |
| --- | --- | --- | --- |
| `large-v3-turbo` (q5) | Carte graphique | 0,43 à 0,68 s | **Par défaut** |
| `small` | Carte graphique | 0,24 à 0,5 s | Si la mémoire graphique manque |
| `base` | Processeur | 1,05 à 1,1 s | Repli sans carte NVIDIA ; le délai total passe à 2,2 à 2,8 s |

- `small` et `large-v3-turbo` sur le processeur ne sont pas proposés (3,7 s et 15 s).
- La langue est fixée par le réglage de langue, sans détection automatique (ADR 0008).
- Le modèle est chargé une fois au démarrage et gardé en mémoire.

### 5. Synthèse

- **Piper par défaut**, par sherpa-onnx **1.13.8** : `fr_FR-tom-medium` en français, `en_GB-alan-medium` en
  anglais. Une phrase de 3,5 s est produite en 0,16 à 0,40 s sur le processeur.
- **Kokoro en option « qualité »** (`ff_siwis`, `bm_george`) tant qu'il ne tourne pas sur la carte
  graphique : sur le processeur, il met 1,6 à 1,7 s par phrase et fait sortir de la cible.
- **Voix cloud** (ElevenLabs, OpenAI) : des adaptateurs derrière le même contrat, listés seulement si une
  clé est dans le coffre (US1.10). Choisir une voix cloud envoie le texte des réponses hors de la machine ;
  l'écran de choix le dit.
- La réponse du modèle est découpée en phrases. Chaque phrase est synthétisée puis jouée dans l'ordre, et la
  synthèse commence dès la première phrase complète (US1.9).
- **Interruption** (US1.12) : la lecture s'arrête net et les phrases en attente sont jetées.
- La page de test des voix (US1.11) utilise le même contrat que la conversation.

### 6. Budget de délai et de mémoire

- Cible : **moins de 2 s** entre la fin de la phrase et le premier son, à chaud, avec la configuration par
  défaut. Elle se remesure avec le micro et le haut-parleur réels.
- Le premier appel à froid prend 70 à 115 s, le temps de charger le modèle local. Le préchargement est
  décrit dans l'ADR 0007.
- Mémoire graphique : il reste environ 600 Mio avec la configuration par défaut sur une carte de 12 Go.
  L'assistant de configuration propose en repli Whisper `small` et un modèle local plus petit (ADR 0007).

### 7. Modèles

- Aucun modèle n'est dans le dépôt ni dans le paquet d'installation. L'assistant de configuration les
  télécharge dans le dossier des modèles ([ADR 0004](0004-configuration-storage-and-secret-vault.md)),
  affiche leur taille et vérifie leur empreinte.

| Modèle | Source |
| --- | --- |
| `melspectrogram.onnx`, `embedding_model.onnx`, `hey_jarvis_v0.1.onnx` | Version 0.5.1 d'openWakeWord sur GitHub |
| `ggml-large-v3-turbo-q5_0.bin`, `ggml-small.bin`, `ggml-base.bin` | `ggerganov/whisper.cpp` sur Hugging Face |
| `vits-piper-fr_FR-tom-medium`, `vits-piper-en_GB-alan-medium`, `kokoro-multi-lang-v1_0` | Modèles de synthèse de sherpa-onnx sur GitHub |

## Conséquences

- La chaîne par défaut tient la cible sur le PC de Benoit. Sans carte NVIDIA, elle fonctionne, mais plus
  lentement que la cible.
- sherpa-onnx reste en 1.13.8. La version 2.0 a retiré espeak-ng, dont Piper et Kokoro ont besoin hors de
  l'anglais.
- **À vérifier au début de l'étape 1,** parce que l'essai ne l'a pas fait : le mot d'éveil avec la voix de
  Benoit, le micro réel et le bruit ambiant, la fin de phrase par Silero, le délai réel de la carte son, la
  qualité perçue des voix, et le mot d'éveil pendant que Jarvis parle.
- **Licences à régler avant toute diffusion publique** (hypothèse n° 7 de la conception). Rien ne bloque un
  usage personnel.
  - espeak-ng, utilisé par Piper et Kokoro dans sherpa-onnx 1.13.8, est sous GPLv3.
  - Les modèles préentraînés d'openWakeWord sont sous CC BY-NC-SA 4.0 (usage non commercial), d'après leur
    fiche sur Hugging Face consultée le 2026-11-18.
  - Chaque voix a sa propre licence.
- Ne livrer aucun modèle avec l'application évite de redistribuer ces fichiers, au prix d'un téléchargement
  au premier lancement.
