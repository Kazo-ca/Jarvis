# Premier lancement

Chantier `stage1/14-first-run` (US1.4, [plan de l'étape 1](plans/stage-1.md#14-premier-lancement-stage114-first-run),
[ADR 0006](adr/0006-voice-chain.md), section 7). English: [first-run.md](../../en/dev/first-run.md).

## Ce qui est construit

Deux tranches : `Setup/Models` télécharge les modèles, `Setup/FirstRun` enchaîne les pages des paramètres dans un
assistant. L'assistant ne connaît les pages que par leur clé (`SettingsPage`, ADR 0002, section 3) : chaque page
enregistre elle-même ses choix, comme dans la fenêtre des paramètres.

### Noyau

| Élément | Rôle |
| --- | --- |
| `Kernel/Models` : `ModelDownload`, `IModelInstaller`, `ModelDownloadException` | Un fichier à poser dans le dossier des modèles : source, cible relative, taille, empreinte (`sha256:` ou `sha1:`), fichier simple ou archive `.tar.bz2`. Les causes d'échec : serveur injoignable, empreinte ou taille fausse, disque, autre. |
| `Kernel/Hardware` : `IGraphicsCardProbe`, `GraphicsCard` | La carte NVIDIA (nom, mémoire), ou aucune. |
| `WhisperModelChosen` (`Messages/Voice`) | Le modèle Whisper installé depuis la page des modèles ; la tranche de la transcription, qui possède ce réglage, le prend. |
| `FirstRunWizardRequested` (`Messages/Desktop`) | L'entrée « Assistant de configuration… » de la zone de notification. |

### Adaptateurs

- **`HttpModelInstaller`** (`Adapters/Models`) : écrit le téléchargement dans `models\.downloads\`, vérifie la taille
  puis l'empreinte, et seulement ensuite le déplace à sa place (un fichier faux ou partiel ne remplace jamais un
  modèle). Les voix de sherpa-onnx sont des `.tar.bz2` : décompression bzip2 par SharpZipLib, puis `TarFile` de
  .NET, qui refuse toute entrée hors du dossier ; l'archive est supprimée une fois dépliée. Un client HTTP à lui,
  30 minutes au plus.
- **`NvidiaSmiGraphicsCardProbe`** (`Adapters/Hardware`) : lance `nvidia-smi --query-gpu=name,memory.total`, que le
  pilote NVIDIA installe ; sans lui, pas de CUDA non plus. Windows ne donne la mémoire d'une carte que sur 32 bits
  (4 Go au plus), d'où le pilote.

### Tranche `Features/Setup/Models`

- **`ModelCatalog`** : les modèles, leurs sources épinglées, tailles et empreintes.

| Modèle | Fichiers | Source | Taille |
| --- | --- | --- | --- |
| « Hey Jarvis » | `models\wake-word\` (3 fichiers) | Version 0.5.1 d'openWakeWord sur GitHub | 3,7 Mo |
| Silero | `models\vad\silero_vad.onnx` | Version `asr-models` de sherpa-onnx sur GitHub | 0,6 Mo |
| Whisper large-v3-turbo, small, base | `models\whisper\` | `ggerganov/whisper.cpp` sur Hugging Face (empreinte SHA-1 publiée par whisper.cpp) | 574, 488, 148 Mo |
| Piper Tom (fr), Alan (en) | `models\vits-piper-…\` | Version `tts-models` de sherpa-onnx sur GitHub | 67 Mo chacune |
| Kokoro | `models\kokoro-multi-lang-v1_0\` | Version `tts-models` de sherpa-onnx sur GitHub | 350 Mo |

- **`ModelAdvice`** : ce qui convient à la carte trouvée. Carte NVIDIA d'au moins 15 Go : Whisper turbo sur la carte
  et `qwen3:14b`. De 10 à 15 Go : Whisper turbo sur la carte et un modèle de langage de 7 à 8 milliards de
  paramètres (par exemple `qwen3:8b`). Moins de 10 Go : Whisper `small` sur la carte et le même conseil. Aucune
  carte NVIDIA : Whisper `base` sur le processeur, et le même conseil.
- **`ModelsKeeper`** : un lot de téléchargements à la fois, un fichier après l'autre ; l'état de chaque modèle
  (absent, en attente, téléchargement avec pourcentage, installé, échec avec la cause) ; « Arrêter » supprime le
  fichier en cours et garde ceux déjà posés. Le lot continue si la page se ferme. À la fin, si le Whisper choisi
  est en place, `WhisperModelChosen` (carte graphique si une carte NVIDIA est là, processeur sinon).
- **Page « Modèles vocaux »** (`Setup.Models`, rang 40, après « Voix ») : la carte trouvée et le conseil, les
  modèles par groupe (mot d'éveil, fin de phrase, reconnaissance, voix) avec leur taille et leur état. Cochés
  d'office : le mot d'éveil, Silero, le Whisper conseillé, les deux voix Piper. Kokoro reste un choix. Un seul
  bouton : « Télécharger la sélection (taille) », ou « Utiliser la reconnaissance choisie » quand tout est là.

### Tranche `Features/Setup/FirstRun`

- **Étapes** : Langue (`Settings.Language`), Audio (`Audio.Devices`), Modèles vocaux (`Setup.Models`), Modèles de
  langage (`Llm.Providers`), puis « Prêt ». Une page qu'aucune tranche n'enregistre est sautée. « Précédent » et
  « Suivant » ne font que changer de page ; l'étape survit au changement de langue (`FirstRunWizardState`).
- **`FirstRunLauncher`** ouvre l'assistant au démarrage quand `config.json` n'existe pas ou que l'assistant est resté
  inachevé, et sur demande (zone de notification). Section `Setup.FirstRun` de `config.json` : `pending` passe à
  `true` dès l'ouverture d'un premier lancement et à `false` sur « Terminer ». Un fichier écrit avant ce chantier
  n'a pas cette section : l'assistant ne s'ouvre pas tout seul.

### Ollama sur 11434 puis 11435 (tranche `Llm/Providers`)

`ProviderEditor.FindOllamaAsync` essaie `http://127.0.0.1:11434` puis `:11435` (3 s chacun). Un serveur qui répond
sans le modèle compte (le test dit alors quel modèle tirer). La page « Modèles de langage » cherche toute seule tant
que les fournisseurs sont ceux par défaut ; trouvé sur un autre port, l'adresse est enregistrée tout de suite.
Un bouton « Chercher Ollama sur ce PC » relance la recherche pour tout fournisseur Ollama.

### Hôtes

- **Windows** : entrée « Assistant de configuration… » dans le menu de la zone de notification ; installateur et
  sonde de carte enregistrés par `AddJarvisAdapters`.
- **Web** : `SimulatedModelInstaller` (0,6 s par fichier, en mémoire, rien ne sort de la machine),
  `SimulatedGraphicsCardProbe` (RTX 3060, 12 Go), bouton « Open the first-run wizard ». L'hôte Web simule désormais
  un profil existant, pour que chaque test parte de l'orbe.

## Choix provisoires de ce chantier

À confirmer par Benoit ; chacun se change en une ligne :

1. **Seuils du conseil** : 15 Go de mémoire graphique pour la chaîne complète ; de 10 à 15 Go, Whisper turbo et un
   modèle de langage plus petit ; en dessous, Whisper `small` sur la carte ; sans carte NVIDIA, Whisper `base` sur
   le processeur. Modèle de repli conseillé : `qwen3:8b`. Le premier seuil était de 10 Go ; il a été relevé à la
   recette de l'étape 2 (21 novembre 2026) : sur la carte de 12 Go de Benoit, `qwen3:14b` à côté de Whisper turbo
   ne tenait plus une fois les outils dans la consigne (lecture à 7 jetons par seconde), et Benoit a choisi
   `qwen3:8b` plutôt qu'un Whisper plus léger, qui entend moins bien.
2. **Empreinte SHA-1 pour Whisper** : whisper.cpp ne publie que celle-là. Les autres modèles sont vérifiés en SHA-256.
3. **Nouvelle dépendance SharpZipLib 1.4.2** (MIT), seulement pour le bzip2 des voix de sherpa-onnx, dans `Adapters`.
4. **Ordre des étapes** : langue, audio, modèles vocaux, modèle de langage (le conseil des modèles vocaux sert au
   modèle de langage). Pas d'étape « Voix » : les voix Piper des deux langues sont celles par défaut.
5. **Les deux voix Piper cochées d'office** (le français et l'anglais sont obligatoires en v1), Kokoro non.
6. **Recherche d'Ollama automatique** seulement tant que les fournisseurs sont ceux par défaut, et enregistrement
   immédiat quand il est trouvé sur un autre port.
7. **L'assistant se rouvre** depuis la zone de notification, sans redevenir « en attente ».

## Tests

- Unitaires : `HttpModelInstallerTests` (empreinte SHA-256 et SHA-1, taille, serveur injoignable, archive dépliée,
  cible hors du dossier refusée, annulation sans reste), `NvidiaSmiGraphicsCardProbeTests`, `ModelCatalogTests`
  (chaque fichier là où les adaptateurs le lisent), `ModelAdviceTests`, `ModelsKeeperTests`, `FirstRunLauncherTests`,
  `OllamaSearchTests` (11435 trouvé après 11434), `WhisperChoiceApplierTests`.
- Playwright (`FirstRunTests`) : tout l'assistant en anglais (téléchargement simulé compris), et en français.
- Essai réel dans le conteneur le 20 novembre 2026 : `HttpModelInstaller` a téléchargé, vérifié et posé le mot
  d'éveil, Silero, les deux voix Piper et Kokoro depuis GitHub ; Piper et Kokoro les ont listés et Piper a parlé.
  Hugging Face est bloqué depuis le conteneur : **Whisper n'a pas été téléchargé pour de vrai**.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit :

1. Renommer `%USERPROFILE%\.kazo\jarvis` (par exemple en `jarvis.bak`), lancer Jarvis : l'assistant s'ouvre.
2. Choisir le français, le micro et la sortie ; la page « Modèles vocaux » dit « NVIDIA GeForce RTX 3060, 12 Go » et
   conseille Whisper turbo, avec un modèle de langage plus petit (`qwen3:8b`). Télécharger la sélection : tout passe à « Installé », **Whisper compris** (Hugging Face).
3. Page « Modèles de langage » : « Ollama répond sur le port 11435 ; son adresse est enregistrée. »
4. Terminer, dire « Hey Jarvis » et poser une question : la chaîne complète marche sans rien copier à la main.
5. Fermer l'assistant à mi-chemin sur un profil vide : il revient au démarrage suivant.
6. Remettre le dossier d'origine (`jarvis.bak` → `jarvis`).
