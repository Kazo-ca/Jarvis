# L'orbe, version légère

Chantier `stage1/6-orb` du [plan de l'étape 1](plans/stage-1.md). Décisions :
[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md) (contrat de l'orbe, règle d'animation) et
[ADR 0011](adr/0011-orb-colors-and-drawing.md) (couleurs, sept aspects, dessin). Dessin de référence : la
section 3 du [catalogue](ui/catalog.md) et le prototype
[`orb-light.html`](../../../design/prototypes/orb-light.html). La version haute qualité arrive au chantier 7.

## La tranche

`src/Kazo.Jarvis.Features/Assistant/Orb/` :

| Fichier | Rôle |
| --- | --- |
| `Orb.razor` (+ `.razor.css`, `.razor.js`, `.resx`) | Le composant. Il ne fait que montrer ses entrées. Les tracés SVG sont calculés en C# (`OrbGeometry`) |
| `OrbView.razor` | L'orbe tel que les hôtes le montrent : nourri par la machine à états (`AssistantStateChanged`) et le niveau sonore, son clic publie l'activation |
| `OrbMotion.cs` | La règle de mouvement de chaque aspect (vitesse des anneaux, pulsations), en C# |
| `OrbGeometry.cs` | Les arcs, graduations et « dents » des trois anneaux, dans une boîte de 200 × 200 |
| `OrbActivation.cs` | Le clic sur l'orbe (US1.6) : publie `ActivationRequested(Click)` |
| `OrbSlice.cs` | Enregistrement |

### Entrées et sorties du composant `Orb`

| Entrée | Source dans `OrbView` |
| --- | --- |
| `Look` | `AssistantState.Look`, la fonction d'aspect du noyau |
| `DelegatedTasks` | `AssistantState.DelegatedTasks` ; l'anneau de progression se dessine au-dessus de zéro |
| `Progress` (0 à 1 ou inconnue) | Inconnue pour l'instant : les tâches déléguées arrivent à l'étape 2 |
| `PendingDecisions` | 0 pour l'instant (étape 2) ; la page de démonstration le simule |
| `Fault` | La cause de la condition « erreur », affichée au survol |
| `ReducedMotion` | Option ; le réglage « réduire les animations » du système s'applique aussi |
| `AnimateIdle` | Règle de l'hôte : `false` sur Windows (ADR 0005, section 6) |
| `SetLevelAsync(niveau)` | Le niveau sonore : le micro en écoute, la voix en parole, au plus 30 fois par seconde |

Sortie : `OnActivate`, le clic. Le début de déplacement (ADR 0005) reste à la fenêtre WPF de l'hôte Windows,
qui attrape l'appui pour distinguer un clic d'un déplacement ([hôtes](hosts.md#fenêtre-de-lorbe)).

Le composant expose son aspect dans `data-look`, et le script pose `data-motion` (`full` ou `reduced`) : ce
sont les points d'appui des tests Playwright.

## Les sept aspects

| Aspect | Couleur | Vitesse des anneaux | Lumière |
| --- | --- | --- | --- |
| Veille | cyan | 0 (image fixe) | faible |
| Écoute | cyan | 1 | monte doucement avec la voix, sans changer de taille |
| Réflexion | violet | 3,5 | |
| Parole | cyan | 1,2, et chaque anneau pulse au hasard | pleine |
| Tâche longue | vert | 0,5 | anneau de progression |
| Erreur | rouge | 0 (image fixe) | un éclair à l'entrée ; la cause au survol |
| Micro coupé | gris | 0 (image fixe) | faible, mention « micro coupé » |

- Vitesse 1 : un tour en 24 s pour l'anneau intérieur, 36 s pour celui du milieu (sens inverse), 60 s pour
  l'extérieur (catalogue, section 3).
- La couleur passe d'un aspect à l'autre en fondu de 0,5 s (`@property --orb-color`) ; la vitesse change en
  douceur sur environ 0,6 s, puis plus rien ne tourne quand elle atteint zéro.
- Le badge ambre des décisions et l'anneau de progression se dessinent par-dessus n'importe quel aspect.

## Coût

- La sphère et le halo sont des dégradés CSS fixes. Seuls les trois calques des anneaux tournent, par la Web
  Animations API sur `transform` : le navigateur les tourne sans script à chaque image.
- À vitesse 0, les animations sont en pause : aucune minuterie et aucun `requestAnimationFrame`. Un
  `requestAnimationFrame` ne tourne que pendant les 0,6 s d'un changement de vitesse.
- Le niveau sonore n'est passé au script qu'en écoute (micro) et en parole (voix) : une orbe immobile ne
  reçoit aucun appel.

## Choix provisoires de ce chantier

Faciles à changer ; à confirmer par Benoit.

1. **Erreur :** l'éclair, puis une image fixe (ADR 0011, section 5). Le catalogue disait « presque
   immobile, vitesse 0,25 », écrit avant le budget en veille : non repris.
2. **Mouvement réduit** (l'ADR 0011, section 9, le laisse ouvert) : aucune rotation, aucune pulsation, pas
   même le ralenti d'un changement de vitesse. L'écoute et la parole se distinguent de la veille par la
   lumière seulement : faible en veille, plus forte en écoute, pleine en parole. À valider à l'œil.
3. **Anneau de progression sans pourcentage :** un cercle vert pointillé fixe. L'ADR des tâches déléguées
   (étape 2) le fixera, avec le cas de plusieurs tâches.
4. **Taille :** le corps de l'orbe occupe 80 % de la fenêtre de 160 pixels, pour laisser la place au halo.
5. **Le badge** affiche « 99+ » au-delà de 99 décisions.
6. **Le réglage « qualité de l'orbe »** (haute ou légère) n'existe pas encore : il arrive avec la version
   haute qualité au chantier 7. Le composant n'a donc pas encore d'entrée « version ».

## Page de démonstration

`/orb` dans l'hôte Web (US1.3), liée depuis la page d'accueil. Elle pilote le composant directement par ses
entrées, sans micro ni modèle : les sept aspects, le nombre de tâches et la progression (connue ou non), les
décisions en attente, la cause d'une erreur, le niveau sonore, le mouvement réduit et la veille animée (pour
essayer seulement : interdite sur Windows). Un compteur montre la sortie « clic ».

## Tests

- Unitaires : `OrbMotionTests` (vitesse de chaque aspect, veille animée en option seulement, mouvement réduit),
  `OrbGeometryTests` (tracés, nombres indépendants de la culture), `OrbActivationTests` (le clic passe par la
  machine à états).
- Playwright (`OrbTests`) : chaque aspect dans `data-look` ; écoute = trois rotations qui tournent ; veille,
  micro coupé et erreur = aucune ; veille animée en option ; mouvement réduit du système et de l'option ;
  badge et anneau de progression par-dessus la parole ; cause de l'erreur au survol ; clic du composant ;
  clic sur l'orbe de la page d'accueil = écoute puis annulation.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (l'hôte Web ne les prouve pas) :

1. **Budget obligatoire (ADR 0005, section 6) :** veille, micro coupé et erreur sous 2 % d'un cœur avec la
   version légère, processus WPF et WebView2 ensemble, méthode de l'essai E6.
2. Coût relevé de l'écoute, de la réflexion, de la parole, et de la tâche longue qui tourne lentement
   pendant toute la tâche (ADR 0011, section 5) ; les noter dans la mémoire du projet.
3. L'orbe s'affiche dans la fenêtre transparente sans fond ni bord, halo compris, et les clics autour
   passent toujours à l'application dessous.
4. Un clic sur l'orbe lance l'écoute (l'orbe s'anime), un second l'annule ; un glissement déplace l'orbe sans
   l'activer.
5. Avec « Afficher les animations » coupé dans les paramètres d'accessibilité de Windows, l'orbe ne tourne
   plus en écoute.
