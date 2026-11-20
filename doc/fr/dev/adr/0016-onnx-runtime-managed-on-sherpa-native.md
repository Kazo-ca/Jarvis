# ADR 0016 : un seul ONNX Runtime natif, celui de sherpa-onnx

## Statut

Accepté par Benoit le 2026-11-20 (réponse à la question posée dans la conversation, chantier
`stage1/onnx-runtime-adr`).

- **Remplace en partie :** [ADR 0001](0001-target-dotnet-10-and-projects.md), section 4, pour la seule ligne
  `Microsoft.ML.OnnxRuntime` 1.32.0 du tableau des versions. Le reste de l'ADR 0001 ne change pas.
- **Décrit du code déjà fusionné :** le chantier `stage1/9-wake-word` a dû s'écarter de l'ADR 0001 pour que
  Piper continue de se charger. Cet ADR régularise cet écart ; il ne demande aucun changement de code.

## Contexte

- L'ADR 0001 retient `Microsoft.ML.OnnxRuntime` 1.32.0 pour le mot d'éveil et `org.k2fsa.sherpa.onnx`
  1.13.8 pour Piper, Kokoro et Silero. L'essai E4 les avait prouvés chacun dans son propre programme, jamais
  ensemble.
- Les deux paquets livrent une bibliothèque native du même nom (`onnxruntime.dll`, `libonnxruntime.so`) :
  la 1.32.0 pour le paquet de Microsoft, la 1.28.2 pour sherpa-onnx. Dans un même dossier de sortie, l'une
  écrase l'autre.
- Constaté au chantier `stage1/9-wake-word` sous Linux : avec la 1.32, `libsherpa-onnx-c-api.so` ne trouve
  plus le symbole `OrtGetApiBase@VERS_1.28.2` et ne se charge pas. Piper, Kokoro et Silero sont perdus
  ([pièges](../pitfalls.md)).
- Dans l'autre sens, la partie .NET d'ONNX Runtime refuse un moteur natif plus ancien qu'elle (`GetApi`
  rend null) : garder la 1.32 côté .NET sur le moteur 1.28.2 ne marche pas non plus.
- sherpa-onnx ne peut pas monter : la 2.x a retiré espeak-ng, dont les voix françaises ont besoin
  (ADR 0001, ADR 0006).

## Décision

1. **Un seul ONNX Runtime natif dans le processus : celui que livre `org.k2fsa.sherpa.onnx`** (1.28.2 avec
   la version 1.13.8).
2. **Le mot d'éveil référence `Microsoft.ML.OnnxRuntime.Managed`**, la partie .NET seule, sans bibliothèque
   native. Le paquet `Microsoft.ML.OnnxRuntime` n'est plus référencé par aucun projet de `src/` ou de
   `tests/`.
3. **Version : 1.28.0.** La règle est que la version de `Microsoft.ML.OnnxRuntime.Managed` reste **égale ou
   inférieure** à celle du moteur natif de sherpa-onnx, dans la même version mineure.
4. **Les deux versions montent ensemble.** Changer la version de sherpa-onnx oblige à relire celle de son
   moteur natif et à ajuster `Microsoft.ML.OnnxRuntime.Managed` dans le même changement. Le commentaire de
   `Directory.Packages.props` le rappelle.
5. La ligne du tableau de l'ADR 0001 se lit désormais :

| Paquet | Version | Remarque |
| --- | --- | --- |
| `Microsoft.ML.OnnxRuntime.Managed` | 1.28.0 | Mot d'éveil ; moteur natif 1.28.2 fourni par sherpa-onnx 1.13.8 |

## Options écartées

- **Garder `Microsoft.ML.OnnxRuntime` 1.32.0 et isoler les deux bibliothèques natives** (dossiers séparés,
  chargement à la main par `NativeLibrary`). Possible, mais fragile : deux moteurs dans le même processus,
  un chargement différent sous Windows et sous Linux, et une seconde bibliothèque native à publier.
- **Faire tourner le mot d'éveil par sherpa-onnx** (son détecteur de mots-clés). Ce ne sont pas les modèles
  d'openWakeWord que l'ADR 0006 retient et que l'essai E4 a mesurés ; il faudrait tout remesurer.
- **Mettre le mot d'éveil dans un processus à part.** Trop lourd pour un modèle de quelques mégaoctets qui
  écoute en permanence.

## Conséquences

- Piper, Kokoro, Silero et le mot d'éveil tournent dans le même processus, sous Windows comme sous Linux
  (intégration continue). Vérifié au chantier 9 : Piper puis le mot d'éveil
  dans le même processus, « Hey Jarvis. » dit par Piper obtient 0,993.
- Le mot d'éveil n'a pas les nouveautés d'ONNX Runtime 1.29 à 1.32. Les trois modèles d'openWakeWord 0.5.1
  n'en ont pas besoin : ils tournent sur le processeur avec des opérateurs anciens.
- Le mot d'éveil dépend de la bibliothèque native d'un autre paquet. Si sherpa-onnx quitte un jour le
  projet, `Microsoft.ML.OnnxRuntime` (avec sa partie native) revient, par un nouvel ADR.
- **Reste à vérifier sur Windows** (séance avec Benoit, [liste](../wake-word.md#vérifications-sur-windows)) :
  que l'hôte publié ne contient qu'un seul `onnxruntime.dll` et que le mot d'éveil et Piper s'y chargent
  tous les deux.
