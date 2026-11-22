# ADR 0027 : les gestes sur l'orbe (remplace la section 3 de 0010)

## Statut

Accepté par Benoit le 2026-11-22 (chantier `stage2/16-orb-gestures-adr`). Les trois gestes sont sa demande ;
les points marqués « provisoire » sont des choix d'application qu'il peut changer.

- **Source :** message de Benoit du 2026-11-22 : « Interaction avec l'orbe : j'ai quelques changements à
  faire : 1- clic droit affiche l'éventail ; 2- double clic affiche la vue projets que l'on est en train de
  concevoir ; 3- un clic gauche fait comme le "wake word", Jarvis écoute tout ce que l'on dit. »
- **Remplace :** la section 3 de l'[ADR 0010](0010-task-fan-and-orb-click.md) (« Simple clic et double-clic
  sur l'orbe »). Le reste de l'ADR 0010 (l'éventail, ses panneaux, sa fenêtre, son animation) ne change pas.
- **Complète :** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) : l'orbe gagne une sortie
  « clic droit » ; sa table des transitions ne change pas.
- **Ne décide pas :** la vue des projets elle-même (son ADR est en cours d'écriture) ; les gestes sur le
  téléphone (ADR du mobile, à l'étape 4).

## Contexte

L'ADR 0010 donnait le double-clic à l'éventail des tâches. Benoit conçoit maintenant une vue des projets et
des conversations, qui devient la vue principale : elle prend le double-clic, le geste le plus naturel pour
« ouvrir ». L'éventail, qui se consulte d'un coup d'œil, passe au clic droit.

## Décision

### 1. Clic gauche : Jarvis écoute

- Un clic gauche sur l'orbe fait ce que fait le mot d'éveil : Jarvis écoute. C'est le comportement de
  l'ADR 0005, qui ne change pas : au repos le clic lance l'écoute, pendant que Jarvis réfléchit ou parle il
  l'interrompt et lance l'écoute (US1.6, US1.12).
- **Provisoire :** un clic pendant l'écoute l'annule toujours, comme le dit l'ADR 0005. Le mot d'éveil, lui,
  est ignoré pendant l'écoute. Sans cela, il n'y aurait plus aucun moyen d'arrêter une écoute lancée par
  erreur avec la souris.

### 2. Clic droit : l'éventail des tâches

- Un clic droit sur l'orbe ouvre l'éventail. Un second clic droit le range.
- Il se range aussi par la touche Échap et par un clic sur le bureau assombri, comme avant.
- Un clic droit n'attend aucun délai et ne lance jamais l'écoute.
- L'orbe n'a pas de menu contextuel : le menu de Jarvis reste celui de l'icône de la zone de notification.
- L'éventail n'est toujours pas un état de l'assistant (ADR 0010, section 3, dernier point, repris ici).

### 3. Double-clic : la vue des projets

- Un double-clic sur l'orbe demande la vue des projets, par un message du noyau, `ProjectsViewRequested`.
  La tranche de la vue des projets y répond quand elle existe.
- Un double-clic n'est jamais aussi un simple clic : l'orbe attend le délai de double-clic du système avant
  de traiter un clic gauche, comme le disait l'ADR 0010. Le mot d'éveil, le raccourci et le clic droit ne
  sont pas retardés.
- **Provisoire : tant qu'aucune tranche ne répond à ce message**, il n'y a ni double-clic ni délai : le
  clic gauche agit tout de suite. C'est la règle que l'ADR 0010 appliquait avant que l'éventail soit
  construit. Le délai revient de lui-même le jour où la vue des projets est enregistrée.

### 4. Dans les hôtes

- **Windows :** la fenêtre de l'orbe attrape le clic droit comme elle attrape déjà l'appui gauche, et le
  donne à la tranche de l'orbe.
- **Web de test :** le composant de l'orbe reçoit le clic droit du navigateur et retient son menu.
- **Mobile :** rien ne change à cette étape ; il n'y a pas d'éventail sur le téléphone (ADR 0010, section 4).

## Conséquences

- Le message `TaskFanToggleRequested` vient désormais du clic droit. La tranche de l'éventail ne change pas.
- Tant que la vue des projets n'existe pas, le clic gauche redevient immédiat : l'attente d'une demi-seconde
  disparaît pour l'instant.
- Les tests de l'éventail ouvrent l'éventail par un clic droit. Le double-clic reste couvert par des tests
  unitaires, avec une tranche de remplacement qui répond au message.
- Les documents qui décrivent le double-clic (orbe, éventail, hôtes, fonctionnalités) sont mis à jour avec le
  code.
