# Mot d'éveil

Chantier `stage1/9-wake-word` (US1.5, ADR [0006](adr/0006-voice-chain.md), section 2) : « Hey Jarvis »
réveille Jarvis, avec un seuil réglable et le score en direct dans les paramètres.
English: [../../en/dev/wake-word.md](../../en/dev/wake-word.md).

## Ce qui est construit

```
micro ─► MicrophoneFeed (noyau) ─► WakeWordListener (tranche) ─► IWakeWordDetector (hôte) ─► score
                                          │ score ≥ seuil
                                          └─► ActivationRequested(WakeWord) ─► machine à états (écoute)
```

### Noyau (`Kernel/Voice`)

| Type | Rôle |
| --- | --- |
| `IWakeWordEngine` | Contrat natif : charge les modèles et donne un détecteur. Lève `WakeWordModelMissingException` (avec la liste des fichiers) si un modèle manque. |
| `IWakeWordDetector` | Note le flux trame par trame (`FrameLength` = 1 280 échantillons, 80 ms à 16 kHz) : score de 0 à 1 pour « hey jarvis » qui se termine maintenant. `Reset` oublie ce qui a été entendu. Un détecteur par flux, pas sûr entre fils. |

### Adaptateur (`Adapters/Voice`)

- **`OnnxWakeWordEngine`** : les trois modèles d'openWakeWord 0.5.1 (`melspectrogram.onnx`,
  `embedding_model.onnx`, `hey_jarvis_v0.1.onnx`) par ONNX Runtime sur le processeur, un fil chacun :
  `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 sur le moteur natif 1.28.2 livré par sherpa-onnx 1.13.8, pour
  n'avoir qu'un `onnxruntime` dans le processus ([pièges](pitfalls.md)).
  Dossier : `%USERPROFILE%\.kazo\jarvis\models\wake-word` (ADR 0004, section 5).
- **`OnnxWakeWordDetector`** : portage en flux du comportement d'openWakeWord (pas de son code) repris de
  l'essai E4 : chaque trame de 80 ms ajoute ses trames de spectrogramme (avec 480 échantillons de contexte),
  une empreinte de 96 valeurs des 76 dernières trames, puis le classifieur note les 16 dernières empreintes
  (environ 1,3 s). Au départ et après `Reset`, les tampons sont remplis comme dans openWakeWord (trames à 1,
  empreintes d'un bruit léger à graine fixe) et les cinq premiers scores valent 0.
- Mesuré dans le conteneur d'intégration (4 fils, sans carte graphique) : **3,4 % d'un cœur** pour 60 s de
  son. L'essai E4 mesurait 1,3 à 2 % sous Windows.

### Tranche `Features/Voice/WakeWord`

- **`WakeWordListener`** (service de fond) :
  - s'abonne au flux du micro ; le fil de capture ne fait que copier les blocs dans une file, les modèles
    tournent sur la tâche du service ; les blocs (50 ms sous Windows) sont recoupés en trames de 80 ms ;
  - publie `ActivationRequested(WakeWord)` quand le score atteint le seuil ;
  - **une détection par activation** : après une détection, le score doit repasser sous le seuil avant
    qu'une autre compte ;
  - **silencieux pendant l'écoute** et quand le micro est coupé : aucune trame n'est notée, et le détecteur
    repart de zéro ensuite (la fin de la demande ne peut pas le réveiller). Il note pendant la réflexion et la
    parole : « Hey Jarvis » interrompt alors Jarvis (US1.12, ADR 0005) ;
  - **modèles absents** : pas de panne de l'assistant (le raccourci et le clic marchent, US1.6), un
    avertissement au journal, l'état « modèles absents » sur la page, et un nouvel essai toutes les 30 s ;
  - expose l'état, le dernier score et la dernière détection pour la page.
- **Page des paramètres « Mot d'éveil »** (`Voice.WakeWord`, après « Audio ») : score en direct avec le
  seuil, meilleur score des 3 dernières secondes, curseur du seuil (0,05 à 0,95 par pas de 0,05, appliqué
  tout de suite), dernière détection (heure et score), état (actif, en pause, micro coupé, modèles absents
  avec les fichiers attendus, échec).
- Réglages : section `Voice.WakeWord` de `config.json` (`threshold`, 0,5 par défaut ; une valeur hors bornes
  est ramenée dans l'intervalle).

### Hôte Web

`SimulatedWakeWordEngine` : un score de 0,02 sur chaque trame du micro simulé ; le bouton « Say “Hey
Jarvis” » de la page d'accueil fait monter les trames suivantes (0,2, 0,6, 0,93, 0,97, 0,7, 0,3).

## Choix provisoires de ce chantier

L'ADR 0006 ne les tranche pas ; à confirmer par Benoit :

1. **Modèles absents = pas de panne** : l'orbe ne passe pas en erreur, la page le dit, nouvel essai toutes
   les 30 s. Depuis le chantier 14, la page « Modèles vocaux » les télécharge ([first-run.md](first-run.md)).
2. Dossier `models\wake-word\` avec les noms de fichiers d'openWakeWord.
3. Une détection par passage au-dessus du seuil, sans délai supplémentaire.
4. Le mot d'éveil reste actif pendant la réflexion et la parole (pour interrompre), en pause seulement
   pendant l'écoute et micro coupé.
5. Curseur de 0,05 à 0,95 par pas de 0,05.
6. Meilleur score des 3 dernières secondes affiché, plus lisible que le score en direct pour régler le seuil.
7. Un détecteur qui échoue en marche s'arrête jusqu'au prochain démarrage (état « échec », journal `Error`),
   plutôt que d'échouer toutes les 80 ms.
8. File d'environ 6 s : si les modèles prennent du retard, le son le plus ancien est jeté (un mot d'éveil en
   retard est pire qu'un mot d'éveil manqué).
9. **`Microsoft.ML.OnnxRuntime.Managed` 1.28.0 au lieu de `Microsoft.ML.OnnxRuntime` 1.32.0** (version de
   l'ADR 0001) : les deux paquets natifs se marchaient dessus et Piper ne se chargeait plus sous Linux
   ([pièges](pitfalls.md)). Piper puis le mot d'éveil dans le même processus : « Hey Jarvis. » à 0,993.
   L'[ADR 0016](adr/0016-onnx-runtime-managed-on-sherpa-native.md) remplace la ligne de l'ADR 0001.

## Tests

- Unitaires : `WakeWordListenerTests` (seuil, une détection par activation, pause pendant l'écoute et micro
  coupé, découpage des blocs, modèles absents puis trouvés, avec la vraie machine à états),
  `OnnxWakeWordEngineTests` (catégorie `Models` : fichiers absents nommés, silence, bruit et son pur sous
  0,2, cinq premiers scores à 0, même résultat après `Reset`). L'intégration continue télécharge les modèles
  et vérifie leur empreinte ([construire](build.md#modèles-pour-les-tests)).
- Test explicite, lancé à la main : une phrase « Hey Jarvis. » de Piper (`en_GB-alan-medium`) passe à
  **0,999** ; « Hey Travis, how are you today? » reste à **0,488**, juste sous 0,5 (0,127 dans l'essai E4,
  qui notait le clip entier d'un coup). Les clips ne sont pas dans le dépôt (licence de la voix).
- Playwright (`WakeWordTests`) : « Hey Jarvis » simulé lance l'écoute ; la page montre le score, l'état,
  la dernière détection et applique le seuil.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (sa voix ; l'hôte Web ne les prouve pas) :

1. Télécharger les modèles par la page « Modèles vocaux » (ou les copier dans `%USERPROFILE%\.kazo\jarvis\models\wake-word`),
   lancer Jarvis : la page « Mot d'éveil » passe à « Jarvis attend « Hey Jarvis » ».
2. **Seuil avec la voix de Benoit** (ADR 0006, section 2) : dire « Hey Jarvis » une dizaine de fois, à
   distance normale, puis parler normalement et laisser jouer de la musique ou une vidéo quelques minutes.
   Noter les meilleurs scores, choisir le seuil, et **l'écrire dans la mémoire du projet**. Si sa
   prononciation ne passe pas même seuil baissé, on s'arrête et on pose la question (entraîner un modèle
   demande un nouvel ADR).
3. « Hey Jarvis » pendant que Jarvis parle (après le chantier 12) : la voix de Jarvis ne le réveille pas, et
   celle de Benoit l'interrompt.
4. Coût du mot d'éveil mesuré à part (méthode de l'essai E6), à noter dans la mémoire.
5. Un seul `onnxruntime.dll` dans le dossier de l'hôte publié, et le mot d'éveil comme Piper s'y chargent
   (ADR 0016).
