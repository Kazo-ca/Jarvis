# Construire et tester

2026-11-19 · Chantier `stage1/1-solution`. S'appuie sur l'[ADR 0001](adr/0001-target-dotnet-10-and-projects.md)
(projets) et l'[ADR 0002](adr/0002-vertical-slices-kernel-and-dependency-rules.md) (règles de dépendance).

## Prérequis

- SDK .NET **10.0.402** ou une bande plus récente de .NET 10 (fixé par `global.json`, `rollForward: latestFeature`).
- Pour lancer l'hôte Windows : Windows 10 version 2004 ou plus récent, x64, et le moteur WebView2.
- Pour les tests Playwright : le navigateur Chromium de Playwright, installé une fois (plus bas).

## Arborescence

```
Kazo.Jarvis.slnx
global.json
Directory.Build.props        version du produit, options du compilateur
Directory.Packages.props     versions des paquets de src/ et tests/
src/Kazo.Jarvis.Kernel       noyau : messages, machine à états, contrats
src/Kazo.Jarvis.Features     tranches verticales : Features/<Domaine>/<Tranche>/
src/Kazo.Jarvis.Adapters     mises en œuvre réelles des contrats (hôte Windows seulement)
src/Kazo.Jarvis.Windows      hôte WPF + BlazorWebView
src/Kazo.Jarvis.Web          hôte Web de test, services simulés, cible de Playwright
tests/Kazo.Jarvis.Tests.Unit tests unitaires et tests d'architecture
tests/Kazo.Jarvis.Tests.E2E  tests Playwright contre l'hôte Web
.github/workflows/ci.yml     intégration continue sous Linux
```

`Updater` arrive avec le chantier 16, `Mobile` à l'étape 4. Les essais de `spikes/` restent hors de la
solution et gardent leurs propres versions de paquets (`spikes/Directory.Packages.props`).

## Commandes

Depuis la racine du dépôt :

| But | Commande |
| --- | --- |
| Tout construire | `dotnet build Kazo.Jarvis.slnx` |
| Tests unitaires et d'architecture | `dotnet test tests/Kazo.Jarvis.Tests.Unit` |
| Installer le navigateur de Playwright (une fois, après une construction) | `pwsh tests/Kazo.Jarvis.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install chromium` |
| Tests Playwright | `dotnet test tests/Kazo.Jarvis.Tests.E2E` |
| Lancer l'hôte Windows | `dotnet run --project src/Kazo.Jarvis.Windows` |
| Lancer l'hôte Web | `dotnet run --project src/Kazo.Jarvis.Web` (http://localhost:5180) |

Les tests Playwright démarrent eux-mêmes l'hôte Web sur un port libre : rien à lancer avant.

## Tests propres à Windows

Un test qui a besoin de Windows (coffre Windows, fenêtres, audio réel) porte `[Category("Windows")]`.
L'intégration continue les exclut (`--filter "TestCategory!=Windows"`) ; ils tournent sur le PC de Benoit, où
un agent fait les vérifications sur Windows de chaque chantier par le contrôle à distance.

## Intégration continue

`.github/workflows/ci.yml` tourne sous Linux (réponse n° 2 du [plan de l'étape 1](plans/stage-1.md)) à
chaque PR, et à chaque poussée sur `main` et sur une branche `*/base` :

1. installe le SDK de `global.json` ;
2. construit toute la solution en Release, hôte Windows compris (`EnableWindowsTargeting`) : il est compilé,
   pas lancé ;
3. lance les tests unitaires et d'architecture, sauf ceux marqués `Windows` ;
4. installe Chromium et lance les tests Playwright contre l'hôte Web ;
5. garde les résultats des tests une semaine en cas d'échec.

Une PR ne se fusionne dans une branche `*/base` que si ce travail est vert (AGENTS.md, IV.5 et IV.7).

## Tests d'architecture

Dans `tests/Kazo.Jarvis.Tests.Unit/Architecture/`. Ils font partie du seuil de fusion et ne se désactivent
jamais (ADR 0002, section 7).

| Règle | Test |
| --- | --- |
| Sens des dépendances entre projets ; l'hôte Web ne charge pas `Adapters` ; tout projet de `src/` a sa règle | `ProjectReferenceTests` (fichiers `.csproj`) |
| `Features` ne référence ni `Adapters` ni un hôte ; `Adapters` ne référence pas `Features` ; le noyau ne référence que la bibliothèque de base et les abstractions `Microsoft.Extensions` | `AssemblyReferenceTests` (assemblys compilés) |
| Aucun paquet autre que `Microsoft.Extensions.*.Abstractions` dans le noyau ; aucun projet ne référence un essai | `ProjectReferenceTests` |
| Aucun composant Razor ni fichier de style ou de script dans le noyau | `KernelContentTests` |
| Une tranche n'utilise aucun type d'une autre tranche (`Features/Shared/` excepté), corps des méthodes compris ; aucun type directement dans un dossier de domaine | `SliceIsolationTests` (NetArchTest) |
| Aucun `<style>`, `<script>` ou `style="…"` dans un fichier `.razor` | `RazorMarkupTests` |

Chaque règle qui porte sur du code est aussi essayée sur des exemples qui la violent
(`SliceIsolationSamples.cs`, cas de `RazorMarkupTests`) : elle est prouvée capable d'échouer alors que les
projets sont encore presque vides.

**Précision sur `<script>` :** le document d'un hôte (`src/Kazo.Jarvis.Web/Components/App.razor`) doit
charger le script de Blazor. Il est le seul fichier autorisé à contenir une balise `<script src="…"></script>`
vide, qui charge un fichier. Un script écrit dans la balise reste interdit partout, comme le style. La liste
est dans `RazorMarkupRule.HostDocuments`.
