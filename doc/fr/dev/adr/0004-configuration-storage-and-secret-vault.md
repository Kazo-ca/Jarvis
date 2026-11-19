# ADR 0004 : configuration, stockage et coffre de secrets

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** récits US1.8, US1.14 et US1.16 de la [conception](../plans/design.md) ; règles de sécurité
  n° 2, 3 et 9 ; hypothèses n° 1 et 2 ; configuration, stockage et coffre de Kazo Bar ; réponse de Benoit du
  2026-11-19 (les données vont dans `%USERPROFILE%`, pas dans `%APPDATA%`).
- **Complète :** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (les contrats sont dans le
  noyau, les mises en œuvre dans `Adapters`).

## Contexte

- Kazo Bar a un seul fichier de configuration par utilisateur (`config.json` avec un `schemaVersion`), des
  migrations avec copie de sauvegarde, et un coffre au choix : Gestionnaire d'identification Windows ou
  fichier chiffré par DPAPI. La configuration ne garde qu'une référence vers chaque secret.
- Jarvis doit garder la conversation (US1.14), puis une mémoire (US2.4) et un journal des exécutions
  (US3.14). Tout doit pouvoir se consulter et s'effacer (règle n° 9).
- Avec des tranches verticales, chaque tranche possède ses réglages et ses données.
- Les modèles vocaux pèsent de quelques Mo à plusieurs centaines de Mo.
- Kazo Bar range ses fichiers dans `%APPDATA%\Kazo\Bar`. Benoit a demandé que ceux de Jarvis soient dans
  son dossier d'utilisateur, `%USERPROFILE%`, comme le font Claude Code (`.claude`) et OpenJarvis
  (`.openjarvis`).

## Décision

### 1. Emplacements

```
%USERPROFILE%\.kazo\jarvis\
  config.json          réglages
  backups\             copies faites avant une migration, ou d'un fichier illisible
  data\jarvis.db       conversation, puis mémoire et journal
  secrets.json         seulement si le coffre « fichier chiffré » est choisi
  models\              modèles téléchargés (mot d'éveil, reconnaissance, voix)
  logs\                journaux techniques
```

- **Une seule racine, dans le dossier de l'utilisateur** : `%USERPROFILE%\.kazo\jarvis`. Rien n'est écrit
  dans `%APPDATA%` ni dans `%LOCALAPPDATA%`.
- Un utilisateur par installation : tout est rangé dans le profil de l'utilisateur Windows (hypothèse n° 1).
- Les tests reçoivent un dossier temporaire à la place de ces racines.

### 2. Configuration

- **Un seul fichier**, `config.json`, avec un `schemaVersion`. Il ne contient aucun secret.
- **Une section par tranche**, nommée `<Domaine>.<Tranche>`. La tranche déclare sa classe d'options, ses
  valeurs par défaut et sa validation. Une tranche ne lit pas la section d'une autre.
- Le noyau donne le contrat : lire une section, la modifier, être prévenu d'un changement. Un réglage modifié
  s'applique sans redémarrer l'application.
- Une section inconnue est conservée à l'enregistrement. Un fichier écrit par une tranche retirée ou par
  une version plus récente ne perd rien.
- **Migrations** comme dans Kazo Bar : un fichier plus ancien est copié dans `backups\`, migré, puis
  réécrit.
- **Un fichier illisible, ou écrit par une version plus récente, n'est jamais écrasé.** L'application
  démarre sur les valeurs par défaut, n'enregistre rien, et nomme le problème (US1.17).
- L'écriture est atomique : fichier temporaire, puis remplacement.
- L'absence de `config.json` lance l'assistant de configuration du premier lancement (US1.4).

### 3. Stockage des données

- Les réglages sont en JSON. **Les données qui grossissent sont dans une base SQLite**, `data\jarvis.db` :
  la conversation dès l'étape 1, puis la mémoire et le journal.
- Raison : ajouter un tour, relire une période, effacer un élément ou tout effacer sans réécrire un fichier
  entier, et sans perte si l'application s'arrête au milieu d'une écriture.
- Le noyau donne un contrat de **magasin de documents par collection** : ajouter, lire par identifiant, lire
  une période, supprimer un document, vider une collection. Il n'expose pas de SQL.
- Chaque tranche possède ses collections et le format de ses documents. Une tranche ne lit pas la collection
  d'une autre : elle le demande par un message.
- La mise en œuvre SQLite est dans `Adapters`. L'hôte Web utilise un magasin en mémoire.
- Le schéma de la base a un numéro de version. Avant une migration, le fichier est copié dans `backups\`.
- L'historique est gardé sans limite de durée (hypothèse n° 2). Un bouton efface tout : conversation,
  mémoire et journal. Les réglages et les secrets ne sont pas touchés par ce bouton.
- La base n'est pas chiffrée. Elle est protégée par le profil Windows de l'utilisateur, comme les données
  de Kazo Bar.

### 4. Coffre de secrets

- Contrat du noyau, repris de Kazo Bar : créer ou remplacer, lire, supprimer, lister les références (jamais
  les valeurs).
- **Deux coffres au choix de l'utilisateur** dans les paramètres : le Gestionnaire d'identification Windows
  (par défaut) ou un fichier `secrets.json` chiffré par DPAPI. Changer de coffre déplace les secrets.
- La configuration ne garde qu'une **référence** (`secretRef`) : 1 à 100 lettres, chiffres, `.`, `-` ou `_`.
- Un secret saisi n'est plus jamais affiché. Il n'apparaît dans aucun journal ni message d'erreur.
- Secrets de l'étape 1 : clés d'API des modèles cloud et des voix cloud. Viendront ensuite les jetons
  OAuth, le jeton de Home Assistant, les jetons des appareils jumelés et des webhooks.
- L'hôte Web utilise un coffre simulé en mémoire.

### 5. Modèles téléchargés

- Les modèles vont dans `%USERPROFILE%\.kazo\jarvis\models`. Ils ne sont ni dans le dépôt, ni dans le paquet
  d'installation. La liste et le téléchargement sont décrits dans l'[ADR 0006](0006-voice-chain.md).

### 6. Journaux et vie privée

- Les journaux techniques vont dans `%USERPROFILE%\.kazo\jarvis\logs`. Ils ne contiennent ni audio, ni
  secret. Au niveau normal, ils ne contiennent pas le texte des conversations.
- **Le tampon d'écoute reste en mémoire. Aucun son capté n'est écrit sur disque** (règle n° 2).

## Conséquences

- On reprend de Kazo Bar ce qui a fait ses preuves (fichier unique, migrations, fichier jamais écrasé,
  coffre au choix) sans reprendre son code tel quel : les contrats sont réécrits pour le noyau.
- SQLite ajoute une dépendance native, isolée dans `Adapters`. Le noyau et les tranches ne la voient pas.
- Le magasin de documents est volontairement simple. Si la mémoire de l'étape 2 demande une recherche plein
  texte, un nouvel ADR l'ajoutera.
- Tout Jarvis tient dans un seul dossier, facile à trouver, à sauvegarder et à supprimer. Ce dossier ne suit
  pas un profil itinérant, et s'écarte de l'emplacement de Kazo Bar.
- Benoit a choisi SQLite plutôt que des fichiers JSON comme Kazo Bar (question posée dans la conversation,
  réponse du 2026-11-19).
