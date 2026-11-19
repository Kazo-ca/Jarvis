# ADR 0001 : cible .NET 10 et liste des projets

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 1, 3, 34 et 36 de la [conception](../plans/design.md) ; essais E4, E5 et E6
  ([synthèse](../spikes.md)).
- **Complété par :** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (règles de dépendance)
  et [ADR 0003](0003-hosts-and-native-services.md) (hôtes).

## Contexte

- Kazo.Jarvis est une application C# autonome, sans Python à l'exécution (décision n° 1).
- Les essais ont tourné sur le SDK .NET 10.0.402. La chaîne vocale (E4) et la fenêtre de l'orbe (E6) y
  fonctionnent.
- Kazo Bar sert de modèle pour les hôtes. Son hôte Windows exige la cible `net10.0-windows10.0.19041.0` :
  avec `net10.0-windows` seul, `WebView2CompositionControl` fait mourir le processus au démarrage.
- Les bibliothèques natives de la chaîne vocale (Whisper.net avec CUDA, sherpa-onnx, ONNX Runtime) n'ont été
  essayées qu'en x64.
- Une version de paquet prise par erreur peut tout casser : sherpa-onnx 2.0 a retiré espeak-ng, et les voix
  françaises ne fonctionnent plus ([pièges](../pitfalls.md)).

## Décision

### 1. Plateforme

- **.NET 10** (version à support long), C# dans sa dernière version, types de référence annulables activés.
  Le SDK est fixé par un fichier `global.json` (`rollForward: latestFeature`).
- **Windows 10 version 2004 (build 19041) ou plus récent, x64 seulement.** Ni x86 ni ARM64. Les essais n'ont
  tourné que sur Windows 11.
- **Android** pour l'application mobile, à l'étape 4. Pas d'iOS.
- **Aucun Python ni Node.js à l'exécution.** Les scripts Python de l'utilisateur (étape 3) utilisent son
  propre interpréteur : Kazo.Jarvis n'en installe pas et n'en dépend pas.

### 2. Arborescence

```
Kazo.Jarvis.slnx
global.json
Directory.Build.props        version unique du produit, options communes du compilateur
Directory.Packages.props     versions des paquets de src/ et de tests/
src/                         les projets du produit
tests/                       les projets de tests
spikes/                      essais jetables, hors de la solution
build/                       scripts de publication
doc/
```

### 3. Projets

| Projet | Type | Cible | Référence | Créé à |
| --- | --- | --- | --- | --- |
| `src/Kazo.Jarvis.Kernel` | Bibliothèque | `net10.0` | aucun projet | Étape 1 |
| `src/Kazo.Jarvis.Features` | Bibliothèque Razor | `net10.0` | Kernel | Étape 1 |
| `src/Kazo.Jarvis.Adapters` | Bibliothèque | `net10.0` | Kernel | Étape 1 |
| `src/Kazo.Jarvis.Windows` | Exécutable WPF avec `BlazorWebView` | `net10.0-windows10.0.19041.0`, `win-x64` | Kernel, Features, Adapters | Étape 1 |
| `src/Kazo.Jarvis.Web` | Application web Blazor | `net10.0` | Kernel, Features | Étape 1 |
| `src/Kazo.Jarvis.Updater` | Exécutable Windows | `net10.0-windows` | aucun projet | Étape 1 (US1.18) |
| `src/Kazo.Jarvis.Mobile` | MAUI Blazor Hybrid | `net10.0-android` | Kernel, Features | Étape 4 |
| `tests/Kazo.Jarvis.Tests.Unit` | NUnit | `net10.0` | Kernel, Features, Adapters | Étape 1 |
| `tests/Kazo.Jarvis.Tests.E2E` | NUnit et Playwright | `net10.0` | Web | Étape 1 |

- Le rôle de chaque projet et le sens des dépendances sont fixés par l'ADR 0002. Les hôtes sont décrits dans
  l'ADR 0003.
- `Kazo.Jarvis.Mobile` n'est pas créé avant l'étape 4 : l'essai E10 n'est pas fait.
- `Kazo.Jarvis.Updater` ne fait que remplacer les fichiers et relancer l'application. La vérification des
  versions et le téléchargement se font dans l'application (une tranche et son adaptateur). Le programme de
  mise à jour ne référence donc aucun autre projet et reste petit.
- Les essais de `spikes/` ne sont jamais référencés par un projet de `src/` ou de `tests/`.

### 4. Règles de construction

- `Directory.Build.props` porte la version unique du produit, comparée aux versions publiées sur GitHub.
- Les versions des paquets de `src/` et de `tests/` sont centralisées dans `Directory.Packages.props`. Les
  essais gardent leurs propres versions.
- Versions de départ, celles que les essais ont prouvées :

| Paquet | Version | Remarque |
| --- | --- | --- |
| `Whisper.net`, `Whisper.net.Runtime`, `Whisper.net.Runtime.Cuda` | 1.9.1 | E4 |
| `org.k2fsa.sherpa.onnx` | 1.13.8 | Ne pas passer à 2.x sans un nouvel ADR (espeak-ng retiré) |
| `Microsoft.ML.OnnxRuntime` | 1.32.0 | E4, mot d'éveil |
| `NAudio` | 3.3.0 | Interfaces en `Span<float>` |
| `Microsoft.AspNetCore.Components.WebView.Wpf` | 10.0.110 | Version vérifiée dans Kazo Bar |

- Tests : NUnit, comme dans Kazo Bar. Les tests Playwright visent l'hôte Web.
- Publication : autonome (`--self-contained`), `win-x64`, l'application et son programme de mise à jour dans
  le même dossier, comme Kazo Bar.

## Conséquences

- Tout ce qui est écrit pour l'étape 1 se construit avec un seul SDK, sur le PC de Benoit comme ailleurs.
- `Kazo.Jarvis.Kernel`, `Features`, `Adapters`, `Web` et les tests restent en `net10.0` : ils se compilent et
  se testent sans Windows. Seuls l'hôte Windows et le programme de mise à jour demandent Windows.
- Un portable sans carte NVIDIA ou une machine ARM64 n'est pas une cible. Le repli sans carte graphique est
  décrit dans l'[ADR 0006](0006-voice-chain.md).
- Monter une version de paquet de la chaîne vocale est une décision, pas une mise à jour de routine : il faut
  refaire les mesures de l'essai E4.
- Tant que le dépôt est privé, la vérification des versions sur GitHub (US1.18) demande un jeton rangé dans
  le coffre. Benoit a accepté l'hypothèse n° 7 de la conception (dépôt public) ; le dépôt est encore privé.
