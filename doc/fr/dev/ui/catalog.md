# Catalogue des designs de l'interface

Designs validés par Benoit le 19 novembre 2026, à partir de maquettes et de prototypes animés. Ce
document les décrit pour qu'on puisse les reproduire dans l'application. Les prototypes HTML sont dans
[`design/prototypes/`](../../../../design/README.md). Ce sont des références visuelles, pas du code de
produit : le code de l'application les portera en composants Razor, avec leur CSS et leur JavaScript à
côté de chaque composant (AGENTS.md, section II).

Les décisions d'architecture restent dans les ADR. L'éventail des tâches est décidé par
l'[ADR 0010](../adr/0010-task-fan-and-orb-click.md). Les couleurs et le dessin de l'orbe sont décidés par
l'[ADR 0011](../adr/0011-orb-colors-and-drawing.md), qui s'appuie sur ce catalogue.

Tout ce qui suit concerne **l'ordinateur seulement**. L'application mobile n'est pas couverte.

## 1. Style commun

| Jeton | Valeur | Usage |
| --- | --- | --- |
| Fond | `#040a12`, halo `#0f3550` en haut | Fond des fenêtres plein écran |
| Cyan | `#5fe6ff` | Couleur de base de Jarvis : veille, écoute, parole, cadres |
| Violet | `#a08cff` | Réflexion |
| Vert | `#5fffb0` | Tâche longue, terminé |
| Rouge | `#ff5f6d` | Erreur |
| Ambre | `#ffb347` | Décision qui attend Benoit (badge, cartes, boutons) |
| Gris | `#7d8b99` | Micro coupé |
| Texte | `#d3f3fb`, secondaire `#7fa6b5` | |

- **Polices :** Rajdhani pour les titres (majuscules espacées), Inter pour le texte, JetBrains Mono pour les
  étiquettes et les chiffres.
- **Détails « hologramme » :** coins en équerre sur les cadres, fines lignes de balayage horizontales
  (opacité 3 à 6 %), fonds translucides en dégradé, lueur de la couleur de l'état.

## 2. L'orbe, version haute qualité

Prototype : [`orb-high-quality.html`](../../../../design/prototypes/orb-high-quality.html).
Image : [`orb-high-quality.jpg`](../../../../design/images/orb-high-quality.jpg).

Trois couches superposées :

1. **Sphère plasma** (Canvas 2D). Dégradé radial (reflet blanc en haut à gauche, couleur de l'état, bord
   lumineux), 18 filaments : des cercles de la sphère déformés par une sinusoïde, qui tournent lentement,
   dessinés en mode additif à faible opacité. Petit cœur blanc au centre, halo autour.
2. **Nuages binaires planétaires** (dans la même toile). Des 0 et 1 placés sur une grille de 42 latitudes
   × 96 longitudes à la surface de la sphère. Un bruit fractal décide où il y a des nuages. Les bandes de
   latitude dérivent à des vitesses différentes, comme sur Jupiter, et la sphère tourne sur un axe incliné
   de 0,38 rad. Vers les bords, les chiffres rapetissent et s'effacent ; la face arrière n'est pas dessinée.
   Les chiffres changent lentement. Avec les nuages, la sphère et les filaments sont plus sombres et plus
   transparents pour qu'ils ressortent.
3. **Anneaux gyroscopiques** (CSS 3D avec SVG). Cinq anneaux translucides, chacun incliné sur un axe
   différent et tournant sur lui-même à sa vitesse ; l'ensemble pivote lentement sur un autre axe, comme un
   gyroscope :

   | Anneau | Taille (côté de la scène) | Inclinaison | Rotation (rad/s à vitesse 1) |
   | --- | --- | --- | --- |
   | Graduation en degrés (0° à 360°, chiffres tous les 30°) | 96 % | `rotateX(68°)` | +0,12 |
   | Arcs épais et pointillé intérieur | 82 % | `rotateX(62°) rotateY(24°)` | −0,35 |
   | Tirets et quatre nœuds | 70 % | `rotateY(68°) rotateX(10°)` | +0,5 |
   | Bande dégradée à 24 repères | 58 % | `rotateX(−48°) rotateY(−32°)` | −0,7 |
   | Couronne intérieure (face à l'écran) | 36 % | aucune | +0,9 |

   Des crochets de visée en équerre entourent la sphère.

## 3. L'orbe, version légère

Prototype : [`orb-light.html`](../../../../design/prototypes/orb-light.html).
Image : [`orb-light.jpg`](../../../../design/images/orb-light.jpg).

Pour les machines où la version haute qualité coûte trop cher. Elle reprend le croquis de Benoit : un cœur
et trois anneaux ouverts, comme les disques d'un cadenas à combinaison.

- **Sphère fixe** en dégradés CSS empilés : reflet blanc, liseré lumineux, ombre en bas à droite, couleur de
  l'état. Halo fixe autour. Rien n'est redessiné à chaque image.
- **Trois anneaux ouverts** en SVG (viewBox 200 × 200), chacun avec un vide et une « dent » à chaque bout :

  | Anneau | Rayon | Tracé | Vide | Sens | Tour complet à vitesse 1 |
  | --- | --- | --- | --- | --- | --- |
  | Intérieur | 44 | épais (6), dégradé, graduation fine | 60° | horaire | 24 s |
  | Milieu | 60 | deux arcs (3,5) de 130° et 155°, piste pointillée | deux vides | antihoraire | 36 s |
  | Extérieur | 78 | fin (1,6), graduation tous les 5° et 30°, curseur | 80° | horaire | 60 s |

- **Animation :** seulement la rotation des anneaux, par la Web Animations API sur `transform` (fait par la
  carte graphique). Le changement de vitesse entre deux états passe par `playbackRate`, en douceur sur
  environ 0,6 s, sans saut. En veille et micro coupé, les animations sont en pause.
- **Parole :** chaque anneau pulse de son côté, au hasard : il grossit de 2,5 à 7 % et s'éclaire, pendant
  220 à 480 ms, toutes les 140 à 660 ms. C'est ce qui distingue la parole de l'écoute.

## 4. Les états de l'orbe

| État | Couleur | Vitesse (haute qualité / légère) | Comportement |
| --- | --- | --- | --- |
| Veille | cyan | 0 / 0 | Immobile (règle acceptée : coût nul en veille) |
| Écoute | cyan | 0,8 / 1 | Rotation douce. La voix monte la lumière doucement, sans changer de taille |
| Réflexion | violet | 2,4 / 3,5 | Rotation rapide |
| Parole | cyan | 1 / 1,2 | Haute qualité : la lumière suit la voix. Légère : pulsations aléatoires des anneaux |
| Tâche longue | vert | 0,45 / 0,5 | Rotation lente et anneau de progression (pourcentage) |
| Erreur | rouge | 0,25 / 0,25 | Un éclair au passage, puis presque immobile |
| Micro coupé | gris | 0 / 0 | Immobile, mention « micro coupé » |

- Le passage d'un état à l'autre se fait en fondu (couleur et vitesse sur 0,3 à 0,6 s).
- Une **décision en attente** ajoute un badge ambre avec le nombre, par-dessus n'importe quel état.
- **Ce que Benoit n'aime pas :** une pulsation de taille qui suit la voix en écoute (elle donnait un effet
  d'avance-recul), et les ondes qui sortent de l'orbe pendant la parole. Les deux ont été retirées.
- **Question ouverte :** animer l'orbe en veille. Les prototypes ont une case pour l'essayer ; il faudra
  mesurer le coût sur l'hôte réel avant de le permettre.

## 5. L'éventail des tâches

Prototype : [`task-fan.html`](../../../../design/prototypes/task-fan.html) (paramètres `?v=A&s=open&c=br`,
`c` = coin `tl`, `tr`, `bl` ou `br`). Décidé par l'ADR 0010, le geste par l'ADR 0027 : un clic droit sur l'orbe fait sortir en arc
les fenêtres des tâches en cours, vers le centre de l'écran quel que soit le coin où se trouve l'orbe ; un
second clic droit les fait rentrer. Bureau assombri, traits fins entre l'orbe et chaque fenêtre, tâche à décider
en ambre avec ses boutons, animation seulement à l'ouverture et à la fermeture.

## 6. Le plein écran (« poste de commande »)

Prototype : [`full-screen.html`](../../../../design/prototypes/full-screen.html).
Image : [`full-screen.jpg`](../../../../design/images/full-screen.jpg).

- **Barre d'état** en haut : état de Jarvis, mot d'éveil actif, modèle local, sessions Claude, budget cloud
  du mois, date et heure.
- **À gauche, « À traiter » :** seulement ce qui demande Benoit, trié par urgence : décisions (ambre),
  erreurs (rouge), terminé non lu (vert). Chaque carte a ses boutons pour agir sans l'ouvrir. Le même
  nombre s'affiche en badge sur l'orbe. En bas, le lien vers les tâches en cours.
- **Au centre, l'élément ouvert :** l'orbe en petit avec le titre, des onglets (conversation, journal,
  fichiers, détails), la conversation avec les demandes d'accord en ambre, et en bas un champ pour parler ou
  écrire.
- **À droite, « Retrouver » :** une seule recherche (Ctrl+K ou à la voix) dans les conversations, les tâches
  et les sessions Claude ; filtres en puces (type, épinglés, période) ; résultats groupés par jour, le mot
  cherché surligné dans l'extrait.

## 7. Propositions écartées

Gardées pour mémoire, à ne pas reprendre sans en reparler avec Benoit : éventail en orbite et en projection
(choix de l'ADR 0010) ; orbe « réacteur », « plasma » seul et « gyroscope » filaire en image fixe ; pluie
binaire verticale (remplacée par les nuages) ; ondes de parole. La version
[`orb-plasma-gyroscope.html`](../../../../design/prototypes/orb-plasma-gyroscope.html) (plasma et anneaux, sans
nuages) est gardée de côté à la demande de Benoit.
