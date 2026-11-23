# ADR 0026 : la vue des projets, des sujets et de leurs fils

## Statut

**Accepté** par Benoit le 2026-11-22 (chantier `stage2/adr-0026-projects-view`), d'après ses six réponses du
même jour sur les maquettes, puis sa réponse « ça me va » aux trois derniers points (section 11).

- **Complète :** [ADR 0013](0013-relais-readiness.md) (la hiérarchie Projet → Sujet → Tâche → Sous-tâche de
  Relais ne change pas ; cet ADR dit comment l'écran la montre et où l'on parle) et
  [ADR 0025](0025-task-agents-and-their-memories.md) (un projet est un dossier local créé dans Jarvis, avec
  son agent et sa mémoire).
- **Modifie :** le plein écran du [catalogue de l'interface](../ui/catalog.md)
  (la colonne « À traiter » devient une entrée du rail, section 7).
- **S'appuie sur :** [ADR 0027](0027-orb-gestures.md) (le double-clic sur l'orbe demande la vue des projets,
  le clic droit ouvre l'éventail).
- **Construction :** juste après l'ADR 0025 (réponse 6), qui définit le projet.

## Contexte

Le plein écran validé le 19 novembre montre ce qui attend Benoit, un élément ouvert et une recherche. Il ne
montre pas **où** vit chaque conversation. Benoit : « l'interface où on voit tous les sujets / projets /
conversations… ajouter ce concept et l'architecture de conversation sur deux niveaux ».

Il a donné l'exemple du travail en cours : « le projet c'est Jarvis, le sujet c'est “Conception vue projets”,
la tâche serait “Préparation documentation”. Tu es dans la sous-tâche “Poser des questions”, tu viens de
terminer la tâche “Lire l'image”, puis “Créer une maquette”. » Il veut, sur un sujet, « une vue claire des
étapes franchies, pouvoir voir et relire ce qui a été fait dans chaque tâche terminée et voir ce qui est prévu
pour la suite ».

Maquettes : [rail des projets](../../../../design/images/projects-rail.jpg),
[tous les projets](../../../../design/images/projects-all.jpg),
[fil du sujet](../../../../design/images/projects-subject-thread.jpg),
[de Général à un projet](../../../../design/images/projects-general-to-project.jpg)
(prototypes dans [`design/prototypes/`](../../../../design/prototypes/)).

## Décision

### 1. Quatre niveaux de travail, deux niveaux de conversation (réponse 1)

Le travail garde les quatre niveaux de Relais : projet, sujet, tâche, sous-tâche. La conversation n'en a que
deux :

| Fil | Avec qui | Ce qu'on y voit |
| --- | --- | --- |
| **Fil du sujet** | l'agent du projet (ADR 0025) et ceux qui font les tâches | les messages de l'utilisateur, les réponses qui lui sont adressées, le début et la fin de chaque tâche |
| **Fil de la tâche** | l'agent qui fait la tâche | tout le travail de la tâche, son résumé, ses fichiers, ses sous-tâches |

Une sous-tâche n'a pas de fil où l'on écrit : on la suit en lecture seule dans sa tâche (réponse 4, règle de
Relais inchangée).

Le sujet reste une étiquette à l'écran (branche, PR, issue) et un fil, pas un écran de plus entre le projet et
les tâches.

### 2. Le fil du projet (réponse 2)

Chaque projet a **un seul fil**, avec l'agent du projet. On y voit aussi les conversations des sujets, comme
dans un projet Claude : un mot de l'agent quand un sujet avance, avec un lien vers le sujet.

### 3. Les tâches d'un sujet

En haut du fil du sujet, une bande liste ses tâches dans l'ordre, numérotées `T1`, `T2`… :

- **faites** (✓, vert) : un clic ouvre leur fil pour relire ce qui a été fait ;
- **en cours** (●) : **toujours visibles**, avec leurs sous-tâches sur une ligne ; ambre quand elles
  attendent l'utilisateur ;
- **prévues** (○, pointillé) : ce que l'agent compte faire ensuite.

« Tout voir » déplie la liste entière quand elle ne tient pas sur une ligne. Les tâches faites peuvent se
replier ; les tâches en cours jamais.

### 4. Répondre à une tâche dans le fil du sujet

On répond à une tâche **dans le fil du sujet**, comme on répond à quelqu'un dans un salon mIRC :
`T3> oui, tu as raison, on fait comme ça`. Le message s'affiche avec l'étiquette `T3` ; un clic sur
l'étiquette ouvre le fil de cette tâche. Le message est rangé aussi dans le fil de la tâche.

- Sans préfixe, le message va à l'agent du projet, dans le fil du sujet.
- À la voix, « pour la tâche 3, … » vaut `T3>`.
- Chaque message de tâche dans le fil du sujet porte son étiquette, l'auteur et l'heure, sur une ligne.

### 5. Le fil d'une tâche

Un clic sur une tâche ouvre son fil dans le panneau de droite : statut, agent, durée, résumé, sous-tâches en
lecture seule, fichiers, messages de la tâche, et le journal complet sur demande. « Retour au sujet » le ferme.

### 6. Le projet « Général » (réponse 3)

Les conversations sans projet (météo, courriels…) vont dans un projet **Général** intégré, qui ne se supprime
pas. Ses sujets ont les mêmes fils que les autres.

Quand Jarvis voit qu'un sujet de Général prend beaucoup de place, **il propose d'en faire un projet** : nom,
dossier proposé, et « le sujet, ses tâches et leurs fils sont déplacés dans le nouveau projet ».
L'utilisateur crée, change le dossier, remet à plus tard ou refuse ; Jarvis ne crée jamais un projet seul.
Un refus n'est plus reproposé pour ce sujet.

### 7. La disposition (réponse 5)

Le plein écran garde la barre d'état et prend quatre colonnes :

1. **Un rail** : ◆ À traiter (tous projets, même nombre que le badge de l'orbe), ▦ Tous les projets, une
   icône par projet avec son badge, Général, + Nouveau projet.
2. **Le panneau du projet** : dossier, agent, fil du projet, sujets actifs (étiquette et barre faites / en
   cours / prévues), sujets terminés.
3. **Au centre** : le chemin Projet › Sujet, le fil du sujet, la bande des tâches, et le champ pour parler ou
   écrire avec le choix de l'agent (ADR 0025, section 4).
4. **À droite** : le fil de la tâche ouverte ; sinon « Retrouver », filtré sur le projet ouvert (Ctrl+K reste
   global).

« Tous les projets » montre une carte par projet : ce qui attend, ce qui tourne avec l'avancement des
sous-tâches, le dernier mot du fil. La demande « dossier sans projet » de l'ADR 0025 (section 6) y a sa carte.

### 8. Ouvrir la vue depuis l'orbe

Les gestes sont fixés par l'[ADR 0027](0027-orb-gestures.md) : un double-clic sur l'orbe envoie
`ProjectsViewRequested`, et la tranche de cette vue y répond en ouvrant le plein écran sur le dernier projet
ouvert (provisoire). Le clic droit ouvre l'éventail, le clic gauche lance l'écoute.

### 9. Ce qui ne change pas

Le modèle de Relais (statuts, rattachement automatique d'une session à un sujet), les demandes d'accord
(ADR 0023), l'éventail (ADR 0010, ouvert au clic droit par l'ADR 0027) et le badge de l'orbe.

### 10. Conséquences

- Les tâches de Jarvis prennent la forme de Relais dès maintenant : chaque `WorkTask` a un projet et un sujet ;
  ce qui n'en a pas va dans Général.
- Une session Claude Code suivie par Jarvis devient une tâche de son sujet ; ses étapes déclarées deviennent
  ses sous-tâches.
- Le plein écran devient une page Razor de plus (tranche à part) ; l'éventail ne change pas.

### 11. Les trois derniers points (réponse « ça me va », recommandations prises)

1. **Le fil du sujet** ne montre que ce qui s'adresse à l'utilisateur, plus le début et la fin de chaque tâche.
   Ce que les agents se disent entre eux reste dans le fil de chaque tâche.
2. **Un sujet** est créé par Jarvis, d'après la branche ou la demande (comme Relais), et aussi par
   l'utilisateur avec « + Nouveau sujet ».
3. **Jarvis propose de faire un projet** d'un sujet de Général à partir de 4 tâches ou 30 messages en une
   semaine. Le seuil est réglable.
