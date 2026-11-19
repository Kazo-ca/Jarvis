# ADR 0011 : couleurs et dessin de l'orbe

## Statut

Accepté par Benoit le 2026-11-19 (proposé le même jour, chantier `spikes/14-orb-look-adr`). Sur la question
de la tâche longue, il a choisi la rotation lente du prototype (section 5). Une première version de cette
page disait l'inverse : elle a été corrigée le jour même, dès sa réponse reçue.

- **Sources :** le [catalogue des designs](../ui/catalog.md), validé par Benoit le 2026-11-19 sur prototypes
  animés ([`design/prototypes/`](../../../../design/README.md)) ; sa demande du même jour d'écrire cet ADR à
  partir de ce catalogue ; section « L'orbe et ses sept états » de la [conception](../plans/design.md) ;
  essai E6 ([résultats](../../../../spikes/e6-orb-window/RESULTS.md)).
- **Remplace en partie :** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md), sur trois points :
  la technique de dessin (sa section 5), les satellites des tâches déléguées (ses sections 2 et 6), et la
  liste des aspects (sa section 2). Sa machine à états, ses transitions et son budget en veille restent
  valables ; ce budget ne vaut plus pendant une tâche longue (section 5).
- **Complète :** [ADR 0010](0010-task-fan-and-orb-click.md), dont la section 9 renvoyait ici les valeurs du
  cyan et de l'ambre et le signal d'une décision en attente.

## Contexte

- L'ADR 0005 a fixé les états et le contrat de l'orbe, et renvoyé ses couleurs et son dessin à un ADR écrit
  sur maquette.
- Benoit a dessiné et validé l'orbe sur des prototypes animés, en deux versions : une « haute qualité » et
  une « légère ». Le catalogue les décrit en détail. Cet ADR ne recopie pas ces détails : il dit ce qui est
  décidé et renvoie au catalogue pour le dessin exact.
- Les prototypes ne suivent pas tous les règles déjà acceptées. Ils dessinent avec Canvas 2D, SVG et CSS,
  alors que l'ADR 0005 dit « Canvas 2D ». Ils font tourner l'orbe pendant une tâche longue, alors que le
  budget en veille interdit toute animation continue hors de l'écoute, de la réflexion et de la parole.
- Essai E6 : dans la fenêtre transparente de l'hôte Windows, une image fixe coûte 1,3 % d'un cœur, et un
  orbe animé à 30 images par seconde 14 à 18 %. Le coût des deux nouvelles versions n'est pas mesuré.

## Décision

### 1. Deux versions, un seul composant

| Version | Dessin | Pour qui |
| --- | --- | --- |
| Haute qualité | Sphère plasma, nuages binaires à sa surface, cinq anneaux gyroscopiques | Version par défaut |
| Légère | Sphère fixe, trois anneaux ouverts qui tournent en sens opposés | Machines où la première coûte trop cher |

- Le dessin exact de chaque version est celui du catalogue (ses sections 2 et 3) et de son prototype.
- C'est toujours **un seul composant Razor** (ADR 0005). La version est un réglage de la tranche de l'orbe,
  « qualité de l'orbe », modifiable dans les paramètres. Jarvis ne change pas de version tout seul.
- Les deux versions reçoivent les mêmes entrées et montrent les mêmes aspects.

### 2. Couleurs

| Couleur | Valeur | Usage |
| --- | --- | --- |
| Cyan | `#5fe6ff` | Couleur de base : veille, écoute, parole, cadres |
| Violet | `#a08cff` | Réflexion |
| Vert | `#5fffb0` | Tâche longue |
| Rouge | `#ff5f6d` | Erreur |
| Gris | `#7d8b99` | Micro coupé |
| Ambre | `#ffb347` | Décision qui attend l'utilisateur |

- Ces valeurs sont définies une seule fois et reprises par l'orbe, par l'éventail (ADR 0010) et par les
  autres écrans du catalogue.
- L'ambre d'une décision et le rouge d'une erreur sont deux couleurs distinctes : la réserve de l'ADR 0010
  est levée.
- Les polices du catalogue (Rajdhani, Inter, JetBrains Mono) sont embarquées dans l'application : aucune
  n'est téléchargée à l'exécution. Leur licence se vérifie avant de les embarquer.

### 3. Sept aspects

| Aspect | Couleur | Comportement |
| --- | --- | --- |
| Veille | Cyan | Immobile, faible luminosité |
| Écoute | Cyan | Rotation douce. La voix fait monter la lumière doucement, sans changer la taille de l'orbe |
| Réflexion | Violet | Rotation rapide |
| Parole | Cyan | Haute qualité : la lumière suit la voix. Légère : chaque anneau pulse de son côté, au hasard |
| Tâche longue | Vert | Rotation lente et anneau de progression |
| Erreur | Rouge | Un éclair à l'entrée, puis immobile. La cause s'affiche au survol |
| Micro coupé | Gris | Immobile, avec la mention « micro coupé » |

- Les vitesses de rotation de chaque aspect sont celles du catalogue (sa section 4).
- Le passage d'un aspect à l'autre se fait en fondu : la couleur et la vitesse changent en 0,3 à 0,6 s.
- **Écartés par Benoit, à ne pas reprendre :** une pulsation de taille qui suit la voix en écoute, et des
  ondes qui sortent de l'orbe pendant la parole. Les autres propositions écartées sont dans le catalogue
  (sa section 7).

### 4. Fonction d'aspect

« Tâche longue » devient un aspect à part entière. La fonction pure de l'ADR 0005 devient :

1. si la phase est Écoute, Réflexion ou Parole : l'aspect de cette phase ;
2. sinon, s'il y a une erreur : « erreur » ;
3. sinon, si le micro est coupé : « micro coupé » ;
4. sinon, s'il y a au moins une tâche déléguée : « tâche longue » ;
5. sinon : « veille ».

Deux éléments se dessinent par-dessus l'aspect, quel qu'il soit :

- **L'anneau de progression**, dès qu'il y a une tâche déléguée. Il remplace les satellites de l'ADR 0005.
  Jarvis peut donc écouter ou parler pendant qu'une tâche avance, et l'anneau reste visible.
- **Le badge ambre**, avec le nombre de décisions en attente. C'est le signal d'une décision quand
  l'éventail est fermé (ADR 0010).

### 5. Animation

Cette table complète celle de l'ADR 0005 (sa section 6). Le budget en veille ne change pas : moins de 2 %
d'un cœur sur Windows, quand aucune tâche déléguée ne tourne.

| Aspect | Règle sur Windows |
| --- | --- |
| Veille, micro coupé | Image fixe, dans les deux versions |
| Erreur | L'éclair d'entrée, puis image fixe |
| Tâche longue | **Rotation lente, comme le prototype**, pendant toute la tâche. L'anneau de progression avance avec elle |
| Écoute, réflexion, parole | Animation complète |
| Mouvement réduit | Aucune rotation et aucune pulsation ; la couleur et la luminosité changent quand même |

- **Tâche longue :** choix de Benoit, à qui la question a été posée. L'orbe tourne lentement tant qu'une
  tâche déléguée travaille, même pendant des dizaines de minutes. D'après l'essai E6, cela peut coûter 6 à
  18 % d'un cœur pendant ce temps : ce coût est accepté. La cadence de dessin de cet aspect se règle par la
  mesure, pour garder une rotation fluide au moindre coût.
- La rotation s'arrête dès que l'aspect change : en erreur et micro coupé, l'orbe est fixe même si une tâche
  travaille.
- **Animer la veille** reste interdit sur Windows : le budget en veille l'empêche. Les prototypes ont une
  case pour l'essayer ; l'autoriser demanderait une mesure et un nouvel ADR.
- **Dessin par script** (sphère et nuages de la version haute qualité) : au plus 30 images par seconde,
  comme le dit l'ADR 0005. Les prototypes dessinent à la cadence de l'écran ; le composant la limite.
- **Rotation des anneaux de la version légère :** elle est faite par le moteur du navigateur, sur des
  calques déjà dessinés, sans script à chaque image. La limite de 30 images par seconde ne s'y applique pas.
  Son coût dans la fenêtre transparente n'est pas connu : il se mesure.

### 6. Technique de dessin

- L'ADR 0005 disait : « Son dessin est en Canvas 2D ». Le composant dessine maintenant avec **Canvas 2D, SVG
  et CSS**, selon la version : Canvas 2D pour la sphère et les nuages, SVG et transformations CSS pour les
  anneaux, dégradés CSS pour la sphère de la version légère.
- Le reste ne change pas : le CSS et le JavaScript sont à côté du composant, et le JavaScript ne fait que
  dessiner. Aucune règle d'état n'y est écrite.
- Les prototypes sont des références visuelles, pas du code de produit. Le composant les porte ; il ne les
  référence pas.

### 7. Entrées du composant

Aux entrées de l'ADR 0005 s'ajoutent :

- la version (haute qualité ou légère) ;
- la progression de la tâche longue, de 0 à 1, ou « inconnue » ;
- le nombre de décisions en attente.

Le nombre de tâches déléguées reste une entrée. Le niveau sonore sert comme le dit la section 3 : il règle
la lumière, jamais la taille.

### 8. Mesure à l'étape 1

- Avant de déclarer la tranche de l'orbe terminée, les deux versions sont mesurées sur l'hôte réel, avec la
  méthode de l'essai E6 : en veille, puis en écoute, en réflexion, en parole et en tâche longue.
- En veille, les deux versions doivent tenir le budget de l'ADR 0005.
- Si la version haute qualité dépasse nettement, en animation, les 14 à 18 % d'un cœur acceptés par
  l'ADR 0005, la version légère devient la version par défaut, et la mémoire du projet le note.

### 9. Ce que cet ADR ne décide pas

- **L'anneau de progression quand il y a plusieurs tâches, ou une tâche sans pourcentage** (une session
  Claude Code n'en donne pas). L'ADR des tâches déléguées, à l'étape 2, le fixe.
- **Le signe fixe de l'écoute et de la parole en mouvement réduit.** Elles partagent le cyan avec la veille
  et ne s'en distinguent alors que par la luminosité. À valider sur prototype pendant la tranche de l'orbe.
- **La taille de l'orbe à l'écran** et la zone qui reçoit les clics. Les anneaux de la version haute qualité
  débordent largement de la sphère : la fenêtre de l'orbe (ADR 0003) doit les contenir.
- **Le plein écran « poste de commande »** du catalogue. Il aura son ADR avec l'étape qui le construit.
- **Le mobile.** Le catalogue ne couvre que l'ordinateur ; la version montrée sur le téléphone se choisit à
  l'étape 4.

## Conséquences

- L'orbe a sept aspects exclusifs, au lieu de six aspects et d'une superposition. Les satellites
  disparaissent.
- La veille, l'écoute et la parole ont la même couleur. Elles se distinguent par le mouvement et la lumière,
  pas par la teinte. La réflexion, la tâche longue, l'erreur et le micro coupé ont chacun la leur.
- Pendant une tâche longue, l'affichage coûte comme une phase animée, aussi longtemps que la tâche dure. Le
  budget de moins de 2 % d'un cœur ne vaut que sans tâche déléguée. L'ADR 0005 prévoyait des satellites
  immobiles : cette règle est remplacée.
- Il y a deux dessins à tenir à jour et à tester. La page de démonstration (US1.3) montre les deux versions
  et les sept aspects.
- **Risque :** la version haute qualité redessine à chaque image une grille de 42 × 96 chiffres. Son coût
  dans la fenêtre transparente peut dépasser ce que l'essai E6 a mesuré. La section 8 prévoit ce cas.
- La section « L'orbe et ses sept états » de la conception décrivait l'ancien dessin (anneaux qui pulsent,
  satellites). Elle est mise à jour avec l'acceptation de cet ADR.
