# Vue des projets

Chantier `stage2/24-projects-view`. Décision : [ADR 0026](adr/0026-projects-subjects-and-their-threads.md),
sections 3, 5, 7 et 8 ; [ADR 0027](adr/0027-orb-gestures.md) pour le double-clic. Ce premier chantier **lit**
le tableau des tâches et le catalogue des projets : on n'écrit encore dans aucun fil.

![Un projet, son sujet, la bande des tâches et le fil d'une tâche](../../../design/screenshots/projects-subject.png)

## Ce que voit l'utilisateur

- **Un double-clic sur l'orbe** ouvre la vue, en plein écran sur Windows. Elle rouvre là où l'utilisateur
  l'a laissée, tant que Jarvis tourne.
- **Le rail**, à gauche : ◆ « À traiter » avec le nombre de tâches qui attendent, ▦ « Tous les projets », une
  entrée par projet (deux lettres et son nombre en attente), 💬 « Général », et « + » qui ouvre la page
  « Projets » des réglages.
- **« À traiter »** liste les tâches qui attendent l'utilisateur, tous projets confondus, la plus ancienne
  d'abord. Un clic ouvre le projet, le sujet et le fil de la tâche.
- **« Tous les projets »** montre une carte par projet : ce qui attend, jusqu'à trois tâches en cours avec
  l'avancement de leurs sous-tâches, le dernier mot. Un dossier où une session travaille sans projet a sa carte,
  qui ouvre la page « Projets ».
- **Le panneau du projet** : son dossier, le fil du projet (le dernier mot de ses sujets), les sujets actifs
  avec leur barre faites / en cours / prévues, puis les sujets terminés depuis sept jours.
- **Au centre**, le sujet : le chemin Projet › Sujet, la bande des tâches numérotées `T1`, `T2`… (✓ faite,
  ● en cours, ○ prévue, ✕ arrêtée ; ambre quand elle attend), les sous-tâches des tâches en cours sur une
  ligne, puis le fil du sujet. « Tout voir » déplie la bande au-delà de six tâches ; une tâche en cours reste
  toujours visible.
- **À droite**, le fil de la tâche ouverte (statut, qui travaille, durée, résumé, sous-tâches en lecture
  seule, dossier) ; sinon « Retrouver », qui cherche dans les titres et les résumés des tâches du projet.

## D'où viennent les projets, les sujets et les tâches

`ProjectsOverview.From(tâches, catalogue)` est une fonction pure, sans horloge ni stockage.

| Niveau | Règle |
| --- | --- |
| Projet | Celui du catalogue qui tient le dossier de la tâche (`IProjectCatalog.OwnerOf`). Tous les projets du catalogue sont montrés, même sans tâche |
| Général | Projet intégré, toujours dernier : toute tâche dont le dossier n'appartient à aucun projet |
| Sujet | Dans un projet : le sujet de la tâche (`WorkTask.Topic`, donc la branche). Dans Général : la clé de projet de la tâche (le dépôt ou le nom du dossier), sinon son sujet |
| Tâche | Une tâche du tableau sans tâche parente, numérotée dans l'ordre de création dans son sujet |
| Sous-tâche | Une tâche dont la tâche parente existe ; jamais une tâche du sujet |

`Pending` est « prévue », `Done` « faite », `Abandoned` « arrêtée », tout le reste « en cours ». Un sujet est
terminé quand toutes ses tâches sont faites ou arrêtées.

**Le fil du sujet** est pour l'instant calculé : une ligne au début de chaque tâche, une à sa fin avec son
résumé, une quand elle attend l'utilisateur. Les messages n'y sont pas encore.

## Tranche `Projects/View`

| Fichier | Rôle |
| --- | --- |
| `ProjectsOverview` | Projets, sujets, tâches, lignes du fil, d'après le tableau et le catalogue |
| `ProjectsViewState` | Entrée du rail, sujet et tâche ouverts ; gardés en mémoire |
| `ProjectsViewOpener` | Répond à `ProjectsViewRequested` : ouvre la fenêtre `Projects.View` |
| `ProjectsView` | Le composant Razor, son CSS et ses textes |

Noyau : `IProjectCatalog.OwnerOf` ; `DialogRequest.Maximized`, que l'hôte Windows applique à la fenêtre et que
l'hôte Web traduit par une couche plus large. La tranche ne référence ni `Projects/Catalog` ni `Tasks/Board` :
elle passe par `IProjectCatalog`, `ITaskBoard` et `SettingsWindowRequested`.

## Choix provisoires de ce chantier

À confirmer par Benoit :

1. La vue est une fenêtre ordinaire ouverte agrandie, avec sa barre de titre ; pas la barre d'état de la
   maquette (modèle, sessions, dépense, heure).
2. Elle rouvre sur la dernière entrée du rail, oubliée à l'arrêt de Jarvis ; la première fois sur « Tous les
   projets ».
3. Le nom d'un sujet est le nom de sa branche ; une tâche sur la branche principale va dans « Sans sujet ».
   Pas encore d'étiquette (PR, issue) ni de titre choisi.
4. Dans Général, les tâches d'un même dossier ou dépôt forment un sujet.
5. « À traiter » compte les tâches qui attendent. Le badge de l'orbe compte les demandes d'accord ouvertes : les
   deux nombres peuvent différer.
6. « + » et la carte d'un dossier sans projet ouvrent la page « Projets » des réglages.
7. « Retrouver » cherche dans les tâches du projet ouvert seulement ; pas de Ctrl+K.
8. L'agent affiché est toujours « Projet » (ADR 0025 : un seul agent de projet).

## Pas encore construit

- Écrire ou parler dans le fil d'un sujet, `T3>` et « pour la tâche 3 » (section 4) ; le choix de l'agent.
- Les messages dans les fils, les fichiers d'une tâche, le journal complet (section 5).
- Le fil du projet comme conversation avec l'agent du projet (section 2).
- « + Nouveau sujet », les archives (section 11).
- La proposition de faire un projet d'un sujet de Général (section 6).

## Non vérifié

- Rien n'a tourné sur Windows : la fenêtre agrandie, le double-clic réel et son délai.
- Aucune session Claude Code réelle : les tâches viennent des sessions simulées de l'hôte Web.

## Tests

- Unitaires : `ProjectsOverviewTests` (regroupement, numéros, fil, Général, dossier sans projet).
- Playwright : `ProjectsViewTests` (double-clic, session sans projet, tâche qui attend, projet créé depuis la
  vue).

## Vérifications sur Windows

1. Double-clic sur l'orbe : la vue s'ouvre agrandie ; un clic simple lance encore l'écoute après le délai.
2. Lancer Claude Code dans un dossier de projet : la tâche apparaît dans le projet, sous le sujet de sa
   branche.
3. Une permission demandée : le rail et « À traiter » la comptent, un clic ouvre le fil de la tâche.
