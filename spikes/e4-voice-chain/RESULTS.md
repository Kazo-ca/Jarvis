# Résultats E4 et E5 — 18 novembre 2026

**E4 : réussi, avec deux réserves.** La chaîne complète tourne en C# sans Python. Délai mesuré entre la fin
de la phrase et le premier son : **1,6 à 1,8 s** à chaud avec Whisper large-v3-turbo sur GPU, qwen3 14B et
Piper. La cible de 2 s est tenue. Les réserves : le mot d'éveil reconnaît mal un « hey jarvis » prononcé à la
française (voix de synthèse), et Kokoro est trop lent sur le processeur.

**E5 : réussi, marge mince.** Whisper et le modèle 14B cohabitent sur la RTX 3060 : 11,4 à 11,7 Go sur 12,3 Go.
Whisper sur le processeur n'est pas acceptable (3,7 s pour small, 15 s pour turbo).

## Pile exécutée

- .NET 10.0.402, Windows 11 26200, RTX 3060 12 Go, processeur 16 fils.
- `Whisper.net` 1.9.1 avec `Whisper.net.Runtime.Cuda` (chargé : `Cuda`).
- `org.k2fsa.sherpa.onnx` **1.13.8** pour Piper et Kokoro (voir le piège plus bas sur la 2.0).
- `Microsoft.ML.OnnxRuntime` 1.32.0 pour les trois modèles openWakeWord, portés en C# (`WakeWordDetector.cs`).
- Ollama 11435 dans Docker : `local-ia-active` (qwen2.5-coder 14B Q4_K_M) puis `qwen3:14b`.
- Phrases d'essai fabriquées par la synthèse (pas de micro) : la question française dure 5,3 s.

## Mot d'éveil (openWakeWord « hey_jarvis_v0.1 », seuil usuel 0,5)

| Clip | Score max |
| --- | --- |
| « Hey Jarvis » Kokoro anglais | **0,997** |
| « Hey Jarvis » Piper anglais | **0,997** |
| « Hey Jarvis » Kokoro voix française | 0,156 |
| « Hé Jarvis » Piper français | 0,037 |
| « Hey Travis, how are you today? » | 0,127 |
| Phrase française sans mot d'éveil | 0,000 |

Coût : 1,3 à 2 % du temps réel sur un cœur. Le portage C# détecte franchement en anglais et rejette les
phrases voisines. **À vérifier avec ta vraie voix** : les voix françaises de synthèse prononcent « Jarvis » à
la française et ne déclenchent pas. Si ta prononciation ne passe pas non plus, il faudra baisser le seuil ou
entraîner un modèle (hypothèse n° 9 de la conception, prévue pour la v2).

## Chaîne complète (à chaud = modèle déjà chargé)

Total = 500 ms de silence attendu par la détection de fin de phrase + transcription + première phrase du
modèle + synthèse de cette phrase.

| Whisper | Appareil | Modèle local | Total à chaud | Transcription |
| --- | --- | --- | --- | --- |
| small | GPU | qwen2.5-coder 14B | 1,5 à 2,2 s | 0,24 à 0,5 s |
| large-v3-turbo q5 | GPU | qwen2.5-coder 14B | 2,2 s | 0,43 à 0,5 s |
| large-v3-turbo q5 | GPU | **qwen3 14B** | **1,6 à 1,8 s** | 0,43 à 0,68 s |
| base | CPU | qwen2.5-coder 14B | 2,2 à 2,8 s | 1,05 à 1,1 s |
| small | CPU | qwen2.5-coder 14B | 5,2 à 5,7 s | 3,7 à 3,9 s |
| large-v3-turbo q5 | CPU | qwen2.5-coder 14B | 16,6 à 17,2 s | 15,2 s |

Transcription exacte dans tous les cas : « Quelle est la capitale du Canada et combien d'habitants y vivent ? ».

**Premier appel à froid : 70 à 115 s**, le temps qu'Ollama charge le modèle de 9 Go. Kazo.Jarvis devra
précharger le modèle au démarrage et le garder en mémoire (`keep_alive`).

## Synthèse vocale (sherpa-onnx, processeur, 4 fils)

| Voix | Chargement | Une phrase d'environ 3,5 s | Facteur temps réel |
| --- | --- | --- | --- |
| Piper fr_FR tom medium | 0,6 s | 0,31 à 0,40 s | 0,09 |
| Piper en_GB alan medium | 0,9 s | 0,16 s | 0,05 |
| Kokoro v1.0 ff_siwis (fr) | 1,3 s | 1,6 s | 0,51 |
| Kokoro v1.0 bm_george (en) | 1,8 s | 1,7 s | 0,50 |

Kokoro sur processeur ajoute 1,3 s au délai et fait sortir de la cible. À essayer : le paquet sherpa-onnx
CUDA, ou garder Piper par défaut et Kokoro en option « qualité ».

## Mémoire GPU (E5)

| Situation | Utilisé / 12 288 Mio |
| --- | --- |
| Rien de chargé | 2 375 Mio (bureau, navigateur) |
| Whisper small seul | 3 140 Mio |
| qwen 14B (9,5 Go, contexte 4096) + Whisper small | 11 389 Mio |
| qwen 14B + Whisper large-v3-turbo q5 | 11 507 à 11 687 Mio |

Il reste environ 600 Mio. Un contexte plus long que 4096 jetons, ou un jeu lancé en même temps, fera
déborder le modèle sur la mémoire centrale et le ralentira fortement. Recommandation : contexte plafonné à
8 k, Whisper turbo ou small sur GPU, et un modèle 7-8B proposé en repli dans l'assistant de configuration.

## Non vérifié

- Micro réel, bruit ambiant, et détection de fin de phrase par Silero VAD (le délai de 500 ms est une hypothèse).
- Lecture audio réelle : le premier son est compté à la fin de la synthèse de la phrase, sans le tampon de la carte son.
- Qualité perçue des voix (fichiers produits dans `out/`, non publiés).

## Pièges rencontrés

- **sherpa-onnx 2.0 a retiré espeak-ng** (PR 4055). Piper et Kokoro hors anglais échouent : « no frontend
  available ». Rester sur 1.13.8, ou fournir soi-même les phonèmes. espeak-ng est sous GPLv3, à garder en
  tête pour la licence de Kazo.Jarvis.
- Kokoro multilingue : la langue `en-gb` n'existe pas dans ses données, utiliser `en-us` avec une voix britannique.
- NAudio 3 passe ses interfaces en `Span<float>`.
