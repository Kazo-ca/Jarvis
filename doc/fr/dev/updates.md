# Mises à jour

Chantier `stage1/16-updates` (US1.18, [ADR 0001](adr/0001-target-dotnet-10-and-projects.md), section 3).
Mécanisme propre à Kazo.Jarvis, copié de celui de Kazo Bar puis adapté ([ADR 0014](adr/0014-license-kazo-bar-rule-and-jarvis-py.md)) :
l'application vérifie les versions et télécharge, `Kazo.Jarvis.Updater` remplace les fichiers et relance.

## Parcours

1. L'utilisateur ouvre **Mises à jour…** dans le menu de la zone de notification (ou clique sur l'avis d'une
   nouvelle version). Le menu publie `UpdatesWindowRequested` ; la tranche ouvre sa fenêtre par
   `IDialogService`.
2. **Rechercher les mises à jour** lit les versions publiées de `Kazo-ca/Jarvis` sur GitHub, sans jeton (le
   dépôt est public). La plus récente qui dépasse la version en cours et qui a un paquet
   `Kazo.Jarvis-<version>-win-x64.zip` est proposée, avec le lien vers ses notes.
3. **Installer la version X** télécharge le paquet dans `%USERPROFILE%\.kazo\jarvis\updates\`, le compare à
   l'empreinte SHA-256 donnée par GitHub, puis le décompresse. Jarvis reste ouvert pendant ce temps : un
   téléchargement lent ou raté ne le prive de rien.
4. L'hôte Windows lance `Kazo.Jarvis.Updater.exe` **depuis le dossier décompressé de la nouvelle version**,
   puis quitte comme par le menu (10 secondes au plus).
5. Le programme de mise à jour attend la fin de Jarvis (30 secondes, puis il le termine s'il tourne depuis
   le dossier d'installation), déplace les fichiers de la version en cours dans
   `.kazo-update-backup-<date>` du dossier d'installation, copie la nouvelle version, puis relance Jarvis.
   Au moindre échec, il remet l'ancienne version en place, la relance et affiche un message.
6. Au démarrage suivant, Jarvis efface la copie de l'ancienne version et le dossier `updates\`.

Le journal du programme de mise à jour est `%USERPROFILE%\.kazo\jarvis\logs\updater.log`. Les réglages et les
données (`%USERPROFILE%\.kazo\jarvis`) ne sont jamais touchés : le programme refuse un dossier d'installation
qui les chevauche.

## Où est le code

| Partie | Emplacement |
| --- | --- |
| Contrats `IReleaseSource`, `IUpdateLauncher`, données `PublishedRelease`, `ReleasePackage` | `Kernel/Updates` |
| Messages `UpdatesWindowRequested`, `UpdateFound` | `Kernel/Messages/Updates` |
| Tranche : versions, choix de la version, service, vérification automatique, fenêtre, textes | `Features/Maintenance/Updates` |
| Lecture de GitHub, téléchargement, empreinte, décompression | `Adapters/Updates/GitHubReleaseSource` |
| Lancement du programme de mise à jour, avis, nettoyage | `Windows/Updates` |
| Simulations (versions publiées, coupure du réseau, lancement enregistré) | `Web/Simulated` |
| Programme de mise à jour (aucune référence de projet) | `src/Kazo.Jarvis.Updater` |
| Script de publication | `build/Publish-Release.ps1` |

Le code de `Kazo.Jarvis.Updater/Installing` n'utilise que .NET : il est aussi compilé dans les tests unitaires,
qui tournent sous Linux.

## Choix que l'ADR 0001 ne tranche pas

- **Vérification automatique activée par défaut** (réglage `Maintenance.Updates`, `Automatic`) : **choix de
  Benoit le 20 novembre 2026**. Elle a lieu une minute après le démarrage puis toutes les 6 heures, et un avis
  de la zone de notification annonce chaque nouvelle version une fois. La demande ne contient que la lecture
  de la liste des versions publiques ; c'est la seule exception à « aucune connexion ne sort par défaut »
  (plan de l'étape 1, chantier 17). Une case de la fenêtre des mises à jour la désactive.
- **Pré-versions proposées** (`IncludePreReleases`, activé) tant que Kazo.Jarvis est en 0.x et ne publie que
  des pré-versions.
- **Le programme de mise à jour part de la nouvelle version**, pas du dossier installé : il ne remplace jamais
  les fichiers qu'il exécute, et la nouvelle version s'installe avec son propre programme de mise à jour. Kazo
  Bar copie tout son dossier ailleurs pour la même raison ; ici, la décompression faite par l'application
  donne ce dossier gratuitement.
- **Pas de fenêtre pour le programme de mise à jour** : le téléchargement, seule étape longue, se voit dans
  la fenêtre de Jarvis ; le remplacement des fichiers prend quelques secondes. Une boîte de message seulement
  en cas d'échec.
- **Une construction de développement n'installe jamais** (lancée depuis `bin\Debug` ou `bin\Release`) : la
  fenêtre le dit, et rien n'est téléchargé.
- **Échec nommé** : GitHub injoignable (pas de réseau, limite de 60 requêtes par heure sans jeton), fichier
  abîmé, programme de mise à jour qui ne démarre pas. Une vérification automatique qui échoue n'est
  qu'écrite au journal.

## Publier une version

Sous Windows (ou Linux avec PowerShell 7) :

```powershell
./build/Publish-Release.ps1 -Version 0.2.0
gh release create v0.2.0 artifacts/Kazo.Jarvis-0.2.0-win-x64.zip --prerelease --title "Kazo.Jarvis 0.2.0"
```

Le script publie l'application et le programme de mise à jour, autonomes, en `win-x64`, dans le même
dossier, puis le compresse. L'étiquette est `v<version>` et le nom du paquet ne change pas : la vérification
les lit tous les deux.

## Vérification sur Windows

Ce que l'hôte Web ne prouve pas (plan de l'étape 1, chantier 16) : une mise à jour de bout en bout entre deux
versions de test.

1. Publier deux versions de test dans des dossiers locaux : `./build/Publish-Release.ps1 -Version 0.0.2-test.1
   -Output C:\jarvis\temp\release-1`, puis la même chose avec `0.0.2-test.2` dans `release-2`.
2. Décompresser la première dans `C:\jarvis\temp\jarvis-installed` et la lancer.
3. Publier la seconde sur GitHub comme pré-version (`gh release create v0.0.2-test.2 … --prerelease`), ou
   la faire servir par un faux serveur.
4. Mises à jour… → Rechercher → Installer. Attendu : Jarvis se ferme, revient en `0.0.2-test.2` en moins de
   30 secondes, ses réglages intacts, `updater.log` sans erreur.
5. Relancer : la copie `.kazo-update-backup-*` et `updates\` disparaissent.
6. Supprimer la pré-version de test sur GitHub.
