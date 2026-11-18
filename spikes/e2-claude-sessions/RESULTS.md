# Résultats E2 et E3 — 18 novembre 2026

**E2 : réussi. E3 : réussi.** Chaque processus Claude Code, lancé dans un terminal ou par Claude Desktop,
s'inscrit dans `%USERPROFILE%\.claude\sessions\<pid>.json` et y tient son état à jour. Les sessions de code
de Claude Desktop sont des processus Claude Code comme les autres (`entrypoint: "claude-desktop"`) : elles se
lisent exactement de la même façon. Aucune écriture et aucun hook ne sont nécessaires pour la liste et l'état.

## Ce que donne le registre

Champs utiles de `sessions/<pid>.json` (Claude Code 2.1.3xx) :

| Champ | Usage pour Jarvis |
| --- | --- |
| `pid`, `procStart` | Vérifier que le processus vit encore |
| `sessionId` | Clé de la session et nom du fichier de transcription |
| `cwd` | Dossier de travail (dossiers autorisés, annonces par dossier) |
| `name`, `nameSource` | Nom affiché (titre donné dans Claude Desktop) |
| `status` | `busy`, `idle` ou `waiting` |
| `waitingFor` | Raison de l'attente quand `status = waiting` |
| `statusUpdatedAt`, `updatedAt` | Dernière activité |
| `entrypoint` | `cli`, `sdk-cli`, `claude-desktop`, etc. |
| `hostSessionId` | Identifiant de la session côté Claude Desktop (`local_…`) |

Les valeurs `busy` / `idle` / `waiting` et les raisons d'attente viennent du code de Claude Code (fonction
qui calcule l'état : `input needed`, `worker request`, `sandbox request`, `dialog open`, ou le motif du
dialogue de permission affiché).

La transcription est dans `projects/<cwd où chaque caractère non alphanumérique devient '-'>/<sessionId>.jsonl`.
Lire les 256 derniers Kio suffit pour trouver le dernier message de l'assistant : c'est la matière du
résumé demandé par US2.13.

## Mesures

- 14 sessions lues (dont 2 en cours) en **73 ms**, avec le dernier message de chacune.
- Mode suivi (`FileSystemWatcher` sur le dossier) pendant un `claude -p` lancé à côté :
  `new -> idle -> busy -> idle`, puis suppression du fichier à la fin du processus. Une session de Claude
  Desktop est passée `idle -> busy` dans la même seconde que son activité.
- L'état `waiting` n'a pas été observé en direct (aucune session n'attendait de permission pendant l'essai).
  Sa présence est établie par le code de Claude Code, pas par une mesure.

## Limites et risques

- **Format interne, non documenté.** Une mise à jour de Claude Code peut le changer. Kazo.Jarvis doit
  isoler cette lecture derrière un adaptateur, tolérer les champs absents, et garder un plan B : les hooks
  officiels `Notification` (permission demandée) et `Stop` (tour terminé), ajoutés aux réglages utilisateur,
  qui appellent le serveur local de Jarvis.
- Seules les sessions dont le processus tourne apparaissent. Une session fermée ou archivée n'a plus de
  fichier ; son historique reste dans `projects/`.
- Les fichiers `sessions/*.key` et `ide/*.lock` contiennent des secrets : Jarvis ne doit jamais les lire.

## Recommandation

Lire le registre pour la liste et l'état (zéro configuration), et proposer en option les hooks
`Notification` et `Stop` pour une notification immédiate qui résiste aux changements de format.
