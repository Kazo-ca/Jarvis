# Vue des projets

Chantier `stage2/24-projects-view`. Décision : [ADR 0026](adr/0026-projects-subjects-and-their-threads.md),
sections 3, 5, 7 et 8 ; [ADR 0027](adr/0027-orb-gestures.md) pour le double-clic. Ce premier chantier **lit**
le tableau des tâches et le catalogue des projets. Le chantier `stage2/26-subject-thread-writing` y ajoute
l'écriture dans le fil d'un sujet (section 4).

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
  seule, messages, fichiers écrits, journal complet sur demande, dossier) ; sinon « Retrouver », qui cherche dans les titres et les résumés des tâches du projet.

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

**Le fil du sujet** mêle, du plus ancien au plus récent, des lignes calculées (une au début de chaque tâche,
une à sa fin avec son résumé, une quand elle attend l'utilisateur) et les messages écrits dans le fil.

## Écrire dans le fil d'un sujet

Chantier `stage2/26-subject-thread-writing` (ADR 0026, section 4).

- Sous le fil, une ligne de saisie. Le message part comme une demande tapée ordinaire
  (`TextSubmitted`), avec l'adresse du fil : `ThreadAddress(Projet, Sujet, Tâche, Étiquette, Titre)`.
- **`T3> oui, on fait comme ça`** répond à la tâche `T3` du sujet (`SubjectAnswer.Parse`) : le préfixe est
  retiré, le message porte l'étiquette `T3` dans le fil et apparaît aussi dans le fil de la tâche. Une tâche
  que le sujet n'a pas est refusée et le texte reste dans la saisie.
- **Qui répond.** Dans un projet du catalogue, l'agent « Projet » reçoit la demande directement, sans passage
  de main, avec le dossier et la mémoire du projet, et une ligne qui dit le sujet (et la tâche, avec son titre).
  Dans Général, l'agent Jarvis répond comme dans la fenêtre de conversation, avec la même ligne.
- **Où c'est gardé.** Le tour va au journal de la conversation avec son adresse (`ConversationTurn.Thread`).
  `ISubjectThreads` (noyau), tenu par la tranche `Conversation/Dialogue`, rend les tours d'un fil ; la vue ne
  référence pas la conversation. Le même tour se voit donc aussi dans la fenêtre de conversation.
- L'état de l'assistant n'accepte un texte qu'au repos : pendant une réponse, la vue dit de réessayer et garde
  le texte.

## Répondre à la session d'une tâche (`T3>`)

Chantier `stage2/27-answer-task-session` (ADR 0026, section 4 ; réponses de Benoit du 23 novembre 2026).
Quand le message répond à une tâche (`T3> …`), la conversation demande d'abord à
`IClaudeTasks.ContinueAsync(tâche, message, langue)` :

| La tâche | Ce qui se passe | Ce que dit Jarvis dans le fil |
| --- | --- | --- |
| Session lancée par Jarvis, terminée (faite ou bloquée) | La session est **reprise** avec le message (`claude --resume <session>`), dans le même dossier, avec le budget et le plafond habituels. La même tâche repasse « en cours » (`TaskChange.Reopen`), puis finit comme une autre | « Je transmets votre réponse à la session Claude Code de … » |
| Session lancée par Jarvis, encore en cours | Rien n'est lancé : en mode sans écran une session ne lit pas de message avant d'avoir fini | Elle travaille encore ; renvoyer le message après |
| Session lancée à la main (crochets) | Rien : ce n'est pas une session de Jarvis | Il ne peut pas lui écrire ; le message reste dans le fil |
| Session inconnue, dossier plus autorisé, trop de sessions, plafond atteint, Claude Code introuvable | Rien | La cause |
| Autre tâche (pas une session Claude Code) | — | L'agent local répond, comme pour un message au sujet |

Dans les quatre premiers cas aucun modèle n'est appelé : la phrase est fixe, le tour est gardé avec son
adresse et une entrée de passage de main vers « Claude Code » (échec `NotContinued` quand rien n'est repris).
Le message part **mot pour mot**, sans résumé ni rappel de la mémoire du projet : la session a déjà son
contexte. La fin de la session reprise est annoncée comme celle d'une session neuve.

Écart : le tableau des tâches ne ramenait jamais une tâche terminée (ADR 0013). `TaskChange.Reopen` le permet,
demandé seulement par `ClaudeTaskRunner` une fois la session relancée ; un crochet tardif n'y arrive toujours
pas. Une reprise qui ne démarre pas laisse la tâche telle qu'elle a fini.

## Proposer de faire un projet d'un sujet de Général

Chantier `stage2/28-general-project-offer` (ADR 0026, sections 6 et 11, point 3).

- Dans le panneau de « Général », une carte par sujet qui a pris **4 tâches ou 30 messages en 7 jours**
  (`ProjectOffer.For`, fonction pure). Elle propose un nom (celui du sujet, modifiable) et un dossier (celui
  où travaillent le plus de ses tâches) et dit que le sujet, ses tâches et leurs fils sont déplacés.
- **Créer** appelle `IProjectCatalog.CreateAsync(nom, dossier)` et ouvre le nouveau projet. **Changer le
  dossier** ouvre la page « Projets ». **Plus tard** cache la proposition 7 jours. **Refuser** ne la repropose
  plus pour ce sujet. Jarvis ne crée jamais un projet seul.
- Un sujet dont aucune tâche n'a de dossier n'est pas proposé : il n'y a pas où faire le projet.
- Les seuils et les réponses sont dans la section de réglages `Projects.View` (`ProjectsViewOptions` :
  `OfferTasks`, `OfferMessages`, `OfferDays`, `LaterDays`, `Refused`, `Later`).

**Ce qui est « déplacé ».** Les tâches suivent d'elles-mêmes : une tâche appartient au projet qui tient son
dossier. Les messages gardent l'adresse où ils ont été écrits ; `ThreadHomes.HomeOf` dit où les montrer :

1. une réponse à une tâche (`T3>`) est montrée dans le sujet où se trouve cette tâche ;
2. un message au sujet reste là où il a été écrit tant que ce sujet existe ;
3. quand les tâches d'un sujet de Général sont parties dans un projet, le message va avec la tâche qui était
   la plus récente au moment où il a été écrit (la plus ancienne s'il les précède toutes).

Noyau : `IProjectCatalog.CreateAsync`, `ISubjectThreads.All`.

## « + Nouveau sujet »

Chantier `stage2/29-new-subject` (ADR 0026, section 11, point 2).

- Sous les sujets actifs d'un projet (et de « Général »), « + Nouveau sujet » demande un nom. Le sujet existe
  aussitôt, sans tâche, avec son fil : on y écrit et l'agent du projet répond.
- Il est gardé dans les réglages `Projects.View` (`UserSubject` : projet, clé `subject:<nom>`, nom, date). La
  clé vient du nom, sans accents, et reste unique dans le projet.
- **Ses tâches.** Un travail remis à Claude Code depuis le fil d'un sujet d'un projet devient une tâche de ce
  sujet : `ClaudeTaskRequest.Topic` porte la clé du sujet, et la tâche est créée avec ce sujet. Cela vaut pour
  tout sujet d'un projet, pas seulement ceux faits à la main. Dans « Général », le sujet d'une session reste
  son dossier.
- Un sujet fait à la main reste ouvert tant qu'il n'a pas de tâche ; ensuite il se termine comme les autres,
  quand toutes ses tâches sont faites ou arrêtées.

## Les fichiers et le journal d'une tâche

ADR 0026, section 5. Le fil d'une tâche montre **les fichiers qu'elle a écrits** et, sur demande, son
**journal complet**.

- Le journal est tenu par qui fait le travail. Aujourd'hui : `ClaudeTaskRunner`, pour les sessions Claude Code
  **lancées par Jarvis**. Il note le démarrage (et chaque reprise), chaque outil utilisé avec ce sur quoi il
  porte (`Write C:\src\bar\NOTES.md`, `Bash dotnet test`), chaque fichier une fois écrit, un outil qui échoue
  ou qui est refusé, ce que la session dit à la fin d'un tour, et la fin.
- Un fichier n'est listé qu'après une écriture **réussie** (`Write`, `Edit`, `MultiEdit`, `NotebookEdit`) : une
  écriture refusée n'écrit rien. Les fichiers créés par une commande `Bash` ne sont pas connus.
- Le journal ne garde **jamais le contenu** écrit ni le résultat d'un outil : seulement l'outil et sa cible.
  Une ligne fait au plus 500 caractères, sur une seule ligne ; un texte qui ressemble à un secret
  (`SecretShapes`) est remplacé par `[…]`. Un journal s'arrête à 500 lignes.
- Il vit dans la collection `Tasks.Log` de `jarvis.db`, sur cet ordinateur seulement, et il est effacé quand
  sa tâche quitte le tableau.
- Une session lancée **à la main** n'a pas de journal : ses hooks disent son état, pas ses outils. Le fil de
  la tâche le dit.
- À l'écran : « Fichiers écrits » (chaque fichier une fois, chemin depuis le dossier de la tâche), puis
  « Journal complet (N) » qui déplie les lignes avec leur heure, et « Replier ».

| Élément | Où | Rôle |
| --- | --- | --- |
| `ITaskLog`, `TaskLogEntry`, `TaskLogKind` | noyau, `Tasks/` | Le contrat : ajouter une ligne, lire le journal d'une tâche |
| `TaskLog` | tranche `Tasks/Journal` | Le journal dans `jarvis.db` : limites, masque, effacement |
| `ToolNote` | tranche `ClaudeCode/Launch` | Ce qu'on garde d'un outil : son nom, sa cible, le fichier écrit |

Le dossier de la tranche s'appelle `Journal` et non `Log` : `.gitignore` écarte tout dossier `log/`.

## Tranche `Projects/View`

| Fichier | Rôle |
| --- | --- |
| `ProjectsOverview` | Projets, sujets, tâches, lignes du fil, d'après le tableau et le catalogue |
| `ProjectsViewState` | Entrée du rail, sujet et tâche ouverts ; gardés en mémoire |
| `ProjectsViewOpener` | Répond à `ProjectsViewRequested` : ouvre la fenêtre `Projects.View` |
| `ProjectsView` | Le composant Razor, son CSS et ses textes |
| `SubjectAnswer` | Le préfixe `T3>` d'un message |
| `ProjectOffer`, `ProjectsViewOptions` | La proposition de faire un projet, ses seuils, les refus et les reports |
| `ThreadHomes` | Dans quel sujet un message est montré aujourd'hui |
| `UserSubject` | Un sujet fait par l'utilisateur, et sa clé |

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
9. Un message écrit dans un fil est un tour ordinaire de la conversation, étiqueté : il n'y a pas une
   conversation séparée par sujet, et la mémoire de conversation le voit comme les autres.
10. `T3>` adressé à une session encore en cours est refusé avec une phrase ; le message n'est pas mis en
    attente pour être remis à la fin.
11. Pas de choix de l'agent dans le fil, et rien à la voix (« pour la tâche 3 »).
12. Les seuils de la proposition se règlent dans le fichier de réglages, pas encore dans une page.
13. « Plus tard » vaut 7 jours. « Changer le dossier » mène à la page « Projets » au lieu d'un champ dans la
    carte.
14. Un message écrit au sujet de Général (sans `T3>`) suit, après la création du projet, la tâche la plus
    récente au moment où il a été écrit.
15. La proposition n'est montrée que dans le panneau de « Général » ; Jarvis ne la dit pas à voix haute et
    l'orbe ne la signale pas.
16. Un sujet fait à la main ne se renomme pas et ne se supprime pas encore (les archives ne sont pas
    construites) ; un sujet d'un projet supprimé n'est plus montré mais reste dans les réglages.
17. Une session Claude Code lancée **à la main** garde le sujet de sa branche : seuls les travaux lancés par
    Jarvis depuis un fil prennent le sujet de ce fil.
18. Le journal d'une tâche garde l'outil et sa cible, pas le détail du travail (ni contenu écrit, ni sortie des
    commandes) ; 500 lignes de 500 caractères au plus par tâche.
19. Une commande `Bash` est notée en entier dans le journal (coupée à 500 caractères), sauf si elle ressemble
    à un secret.
20. Les fichiers d'une tâche sont une liste de chemins : pas de lien pour les ouvrir, pas d'aperçu.
21. Tant que le tableau ne garde aucune tâche, les journaux restants ne sont pas effacés (un tableau vide peut
    être un tableau pas encore lu).

## Pas encore construit

- Parler dans le fil d'un sujet, « pour la tâche 3 » (section 4) ; le choix de l'agent.
- Le journal d'une session lancée à la main, et les fichiers créés par une commande (section 5).
- Le fil du projet comme conversation avec l'agent du projet (section 2).
- Les archives ; renommer ou supprimer un sujet (section 11).

## Non vérifié

- Rien n'a tourné sur Windows : la fenêtre agrandie, le double-clic réel et son délai.
- Aucune session Claude Code réelle : les tâches viennent des sessions simulées de l'hôte Web. En particulier,
  `claude --resume` avec un message n'a jamais été lancé pour de vrai depuis Jarvis.

## Tests

- Unitaires : `ProjectsOverviewTests` (regroupement, numéros, fil, Général, dossier sans projet) ;
  `SubjectAnswerTests` (préfixe `T3>`) ; `ConversationEngineTests` (l'agent « Projet » répond dans le fil d'un
  projet, la ligne du sujet et de la tâche, les messages d'un fil) ; `AssistantStateMachineTests` (l'adresse
  suit le tour).
- Playwright : `ProjectsViewTests` (double-clic, session sans projet, tâche qui attend, projet créé depuis la
  vue, message écrit dans le fil d'un sujet et `T1>` vers une session lancée à la main) ; `ClaudeTasksTests`
  (`T1>` reprend une session terminée lancée par Jarvis).
- `UserSubjectTests` (clé, sujet sans tâche, tâches de sa clé) et le scénario Playwright du nouveau sujet
  (création, message, travail remis à Claude Code qui devient sa tâche T1).
- `ProjectOfferTests` (seuils, dossier proposé, refus, report, messages qui suivent les tâches) et le scénario
  Playwright de la proposition (quatre sessions terminées, création, fil qui suit, « Plus tard »).
- Unitaires de la reprise : `ClaudeTaskRunnerTests` (reprise, session en cours, session à la main, reprise
  qui échoue) et `ConversationEngineTests` (aucun modèle appelé, la phrase, le repli sur l'agent local).

- Journal : `TaskLogTests` (ordre, redémarrage, coupe, masque, limite, effacement avec la tâche),
  `ToolNoteTests` (ce qu'on garde de chaque outil), `ClaudeTaskRunnerTests` (les lignes écrites par une
  session) ; le scénario Playwright du nouveau sujet ouvre le fil de la tâche (fichier `NOTES.md`, journal
  déplié puis replié, contenu jamais montré) et celui de la session à la main vérifie « pas de journal ».

## Vérifications sur Windows

1. Double-clic sur l'orbe : la vue s'ouvre agrandie ; un clic simple lance encore l'écoute après le délai.
2. Lancer Claude Code dans un dossier de projet : la tâche apparaît dans le projet, sous le sujet de sa
   branche.
3. Une permission demandée : le rail et « À traiter » la comptent, un clic ouvre le fil de la tâche.
