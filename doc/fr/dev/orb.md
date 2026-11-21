# L'orbe

Chantiers `stage1/6-orb` (version légère) et `stage1/7-orb-high-quality` (version haute qualité et réglage
« qualité de l'orbe ») du [plan de l'étape 1](plans/stage-1.md). Décisions :
[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md) (contrat de l'orbe, règle d'animation) et
[ADR 0011](adr/0011-orb-colors-and-drawing.md) (deux versions, couleurs, sept aspects, dessin). Dessins de
référence : sections 2 et 3 du [catalogue](ui/catalog.md) et les prototypes
[`orb-high-quality.html`](../../../design/prototypes/orb-high-quality.html) et
[`orb-light.html`](../../../design/prototypes/orb-light.html).

## La tranche

`src/Kazo.Jarvis.Features/Assistant/Orb/` :

| Fichier | Rôle |
| --- | --- |
| `Orb.razor` (+ `.razor.css`, `.resx`) | Le composant. Il ne fait que montrer ses entrées : les aspects (couleur et lumière), l'anneau de progression, le badge, la mention « micro coupé », et le dessin de la version choisie |
| `OrbHighQuality.razor` (+ `.razor.css`, `.razor.js`) | Le dessin haute qualité : sphère plasma et nuages binaires dans une toile, cinq anneaux gyroscopiques en SVG et CSS 3D |
| `OrbLight.razor` (+ `.razor.css`, `.razor.js`) | Le dessin léger : sphère fixe en dégradés CSS, trois anneaux ouverts |
| `OrbDrawing.cs` | Base des deux dessins : charge leur script, lui passe le mouvement et le niveau sonore |
| `OrbView.razor` | L'orbe tel que les hôtes le montrent : nourri par la machine à états (`AssistantStateChanged`), le niveau sonore et le réglage de qualité ; son clic publie l'activation |
| `OrbMotion.cs` | La règle de mouvement de chaque aspect et de chaque version (vitesse, pulsations), en C# |
| `OrbGeometry.cs` | Les arcs, graduations et « dents » des trois anneaux légers, dans une boîte de 200 × 200 |
| `OrbGyroscopeGeometry.cs` | Les cinq anneaux gyroscopiques (graduation en degrés, arcs, tirets et nœuds, bande, couronne) et leurs vitesses, dans une boîte de 1 000 × 1 000 |
| `OrbQuality.cs`, `OrbOptions.cs`, `OrbQualityKeeper.cs` | Le réglage « qualité de l'orbe » (section `Assistant.Orb` de `config.json`), surveillé pour s'appliquer sans redémarrer |
| `OrbSettingsPage.razor` (+ `.razor.css`, `.resx`) | La page « Orbe » de la fenêtre des paramètres |
| `OrbActivation.cs` | Le clic sur l'orbe (US1.6) : publie `ActivationRequested(Click)` |
| `OrbSlice.cs` | Enregistrement |

### Entrées et sorties du composant `Orb`

| Entrée | Source dans `OrbView` |
| --- | --- |
| `Look` | `AssistantState.Look`, la fonction d'aspect du noyau |
| `Quality` | Le réglage « qualité de l'orbe » : haute qualité par défaut (ADR 0011, section 1) |
| `DelegatedTasks` | `AssistantState.DelegatedTasks` ; l'anneau de progression se dessine au-dessus de zéro |
| `Progress` (0 à 1 ou inconnue) | Inconnue pour l'instant : les tâches déléguées arrivent à l'étape 2 |
| `PendingDecisions` | 0 pour l'instant (étape 2) ; la page de démonstration le simule |
| `Fault` | La cause de la condition « erreur », affichée au survol |
| `ReducedMotion` | Option ; le réglage « réduire les animations » du système s'applique aussi |
| `AnimateIdle` | Règle de l'hôte : `false` sur Windows (ADR 0005, section 6) |
| `SetLevelAsync(niveau)` | Le niveau sonore : le micro en écoute, la voix en parole, au plus 30 fois par seconde |

Sortie : `OnActivate`, le clic. Le début de déplacement (ADR 0005) reste à la fenêtre WPF de l'hôte Windows,
qui attrape l'appui pour distinguer un clic d'un déplacement ([hôtes](hosts.md#fenêtre-de-lorbe)).

Le composant expose son aspect dans `data-look` et sa version dans `data-quality` ; le script pose
`data-motion` (`full` ou `reduced`), et celui de la haute qualité pose `data-loop` (`running` ou `stopped`)
sur son dessin. Ce sont les points d'appui des tests Playwright.

## Le réglage « qualité de l'orbe »

- Section `Assistant.Orb` de `config.json`, valeur `Quality` : `High` (par défaut) ou `Light`. Une autre
  valeur est refusée à l'enregistrement.
- Page « Orbe » de la fenêtre des paramètres, après « Langue ». Le changement s'applique tout de suite à
  l'orbe affiché : `OrbQualityKeeper` surveille la section et `OrbView` change de dessin sans redémarrer.
- Jarvis ne change jamais de version tout seul (ADR 0011, section 1).

## Les huit aspects

| Aspect | Couleur | Vitesse haute qualité / légère | Lumière |
| --- | --- | --- | --- |
| Veille | cyan | 0 / 0 (image fixe) | faible |
| Écoute | cyan | 0,8 / 1 | monte doucement avec la voix, sans changer de taille |
| Réflexion | violet | 2,4 / 3,5 | |
| Parole | cyan | 1 / 1,2 ; la version légère pulse au hasard, la haute qualité suit la voix par la lumière | pleine |
| Tâche longue | vert | 0,45 / 0,5 | anneau de progression |
| Erreur | rouge | 0 / 0 (image fixe) | un éclair à l'entrée ; la cause au survol |
| Micro coupé | gris | 0 / 0 (image fixe) | faible, mention « micro coupé » |
| Confirmation | ambre | 0,6 / 0,7 ; les deux versions pulsent | presque pleine ; passe avant l'aspect de la phase ([ADR 0023](adr/0023-confirmation-request-in-a-turn.md)) |

- La couleur (`--orb-color`) et la lumière (`--orb-light`) de chaque aspect sont dans `Orb.razor.css`, pour
  les deux versions. Elles sont enregistrées par `@property` : elles passent en fondu (0,5 et 0,4 s), et la
  toile de la haute qualité les lit comme une couleur et un nombre calculés.
- **Version légère**, vitesse 1 : un tour en 24 s pour l'anneau intérieur, 36 s pour celui du milieu (sens
  inverse), 60 s pour l'extérieur (catalogue, section 3).
- **Haute qualité**, vitesse 1 : chaque anneau tourne sur lui-même à la vitesse du catalogue (section 2 :
  +0,12, −0,35, +0,5, −0,7 et +0,9 rad/s), le gyroscope entier pivote à 0,18 rad/s et se balance entre −0,5
  et −0,1 rad. La sphère tourne et ses nuages dérivent au même rythme.
- La vitesse change en douceur (environ 0,6 s pour la légère, 0,7 s pour la haute qualité), puis plus rien
  ne tourne quand elle atteint zéro.
- Le badge ambre des décisions et l'anneau de progression se dessinent par-dessus n'importe quel aspect,
  dans les deux versions.

## Coût

- **Les deux versions**, à vitesse 0 (veille, micro coupé, erreur) : les animations sont en pause, aucune
  minuterie et aucun `requestAnimationFrame`. Le niveau sonore n'est passé au script qu'en écoute (micro) et
  en parole (voix) : une orbe immobile ne reçoit aucun appel.
- **Version légère :** la sphère et le halo sont des dégradés CSS fixes. Seuls les trois calques des anneaux
  tournent, par la Web Animations API sur `transform` : le navigateur les tourne sans script à chaque image.
- **Haute qualité :** les cinq anneaux et le gyroscope tournent aussi par la Web Animations API. Seule la
  sphère (plasma, 18 filaments, nuages de 42 × 96 cases) est dessinée par script, dans une toile qui ne
  couvre que la sphère (un tiers du côté de l'orbe), **au plus 30 images par seconde** (ADR 0005, section 5).
  La boucle ne tourne que pendant une rotation, un changement de vitesse ou le fondu de 0,7 s d'un
  changement d'aspect ; ensuite elle dessine une dernière image et s'arrête. Les 0 et 1 des nuages sont de
  petites images préparées une fois par couleur et par taille, pas du texte dessiné à chaque fois. Le halo est
  un dégradé CSS fixe.
- **Mesure indicative** (conteneur Linux sans carte graphique, Chromium sans fenêtre, orbe de 160 pixels,
  temps du fil principal) : veille 0 % pour les deux versions ; écoute, réflexion ou tâche longue environ
  21 % en haute qualité (dont 7 % de script) contre moins de 1 % pour la légère. Le rendu y est entièrement
  logiciel : seule la mesure sur Windows (ADR 0011, section 8) compte.

## Choix provisoires

Faciles à changer ; à confirmer par Benoit.

### Chantier 6 (version légère)

1. **Erreur :** l'éclair, puis une image fixe (ADR 0011, section 5). Le catalogue disait « presque
   immobile, vitesse 0,25 », écrit avant le budget en veille : non repris.
2. **Mouvement réduit** (l'ADR 0011, section 9, le laisse ouvert) : aucune rotation, aucune pulsation, pas
   même le ralenti d'un changement de vitesse. L'écoute et la parole se distinguent de la veille par la
   lumière seulement : faible en veille, plus forte en écoute, pleine en parole. À valider à l'œil.
3. **Anneau de progression sans pourcentage :** un cercle vert pointillé fixe. L'ADR des tâches déléguées
   (étape 2) le fixera, avec le cas de plusieurs tâches.
4. **Taille :** le corps de l'orbe légère occupe 80 % de la fenêtre de 160 pixels, pour laisser la place au
   halo.
5. **Le badge** affiche « 99+ » au-delà de 99 décisions.

### Chantier 7 (haute qualité)

1. **Proportions du prototype gardées :** la sphère fait 30 % du côté de l'orbe, les anneaux la
   débordent jusqu'à 96 %. Dans la fenêtre de 160 pixels, la sphère fait donc environ 48 pixels et les
   chiffres des nuages 1 à 3 pixels : ils se lisent comme une texture, plus comme des 0 et des 1. Agrandir la
   fenêtre de l'orbe se décidera après l'avoir vue sur l'écran de Benoit.
2. **Lumière commune aux deux versions :** les valeurs de lumière de chaque aspect sont celles de la version
   légère (`Orb.razor.css`), pas celles du prototype haute qualité (écoute 1, parole 1,15, micro coupé
   0,35). Les écarts sont faibles.
3. **Erreur et mouvement réduit :** mêmes règles que la version légère (éclair puis image fixe ; aucune
   rotation). Les couleurs et la lumière passent quand même en fondu.
4. **Balancement du gyroscope :** une animation aller-retour de 25 s entre −0,5 et −0,1 rad, au lieu du sinus
   lié à la rotation du prototype ; le navigateur la fait sans script. Les anneaux partent d'angles fixes
   (aléatoires dans le prototype), pour que l'orbe soit le même à chaque démarrage.
5. **Halo** en dégradé CSS fixe plutôt que dessiné dans la toile ; chiffres en JetBrains Mono 500 (la graisse
   600 du prototype n'est pas embarquée).
6. **Réglage « qualité de l'orbe »** sur sa propre page « Orbe » des paramètres, après « Langue ». L'option
   « mouvement réduit » n'y est pas encore : seul le réglage du système s'applique sur les hôtes.

## Page de démonstration

`/orb` dans l'hôte Web (US1.3), liée depuis la page d'accueil. Elle pilote le composant directement par ses
entrées, sans micro ni modèle : les deux versions (haute qualité au départ), les huit aspects, le nombre de
tâches et la progression (connue ou non), les décisions en attente, la cause d'une erreur, le niveau sonore,
le mouvement réduit et la veille animée (pour essayer seulement : interdite sur Windows). Un compteur montre
la sortie « clic ».

## Tests

- Unitaires : `OrbMotionTests` (vitesse de chaque aspect dans les deux versions, veille animée en option
  seulement, mouvement réduit), `OrbGeometryTests` et `OrbGyroscopeGeometryTests` (tracés, vitesses des
  anneaux, nombres indépendants de la culture), `OrbQualityKeeperTests` (haute qualité par défaut, choix
  enregistré, changement annoncé une seule fois, valeur inconnue refusée), `OrbActivationTests` (le clic passe
  par la machine à états).
- Playwright (`OrbTests`) : chaque aspect dans `data-look` ; version légère : écoute = trois rotations,
  veille, micro coupé et erreur = aucune, veille animée en option, mouvement réduit du système et de
  l'option ; haute qualité : écoute = sept rotations et boucle de dessin active, veille, micro coupé et erreur
  = aucune rotation et boucle arrêtée, parole sans pulsation, mouvement réduit ; changement de version sur
  place ; réglage « qualité de l'orbe » appliqué à l'orbe de la page d'accueil sans recharger ; badge et
  anneau de progression par-dessus la parole ; cause de l'erreur au survol ; clic du composant ; clic sur
  l'orbe de la page d'accueil = écoute puis annulation.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (l'hôte Web ne les prouve pas). Méthode de l'essai E6, processus
WPF et WebView2 ensemble ; résultats à noter dans la mémoire du projet.

1. **Budget obligatoire (ADR 0005, section 6) :** veille, micro coupé et erreur sous 2 % d'un cœur, **pour
   chacune des deux versions**.
2. **Mesure de l'ADR 0011, section 8, pour les deux versions :** coût de l'écoute, de la réflexion, de la
   parole et de la tâche longue. Si la haute qualité dépasse nettement 14 à 18 % d'un cœur, ou saccade, la
   version légère devient la version par défaut (une ligne dans `OrbOptions`), et le choix se rediscute avec
   Benoit.
3. L'orbe s'affiche dans la fenêtre transparente sans fond ni bord, halo et anneaux compris, dans les deux
   versions, et les clics autour passent toujours à l'application dessous.
4. Un clic sur l'orbe lance l'écoute (l'orbe s'anime), un second l'annule ; un glissement déplace l'orbe sans
   l'activer.
5. Avec « Afficher les animations » coupé dans les paramètres d'accessibilité de Windows, l'orbe ne tourne
   plus en écoute, dans les deux versions.
6. Dans les paramètres, page « Orbe » : passer de « Haute qualité » à « Légère » change l'orbe tout de suite,
   sans redémarrer, et le choix reste après une relance.
7. La haute qualité est lisible et belle à 160 pixels sur l'écran de Benoit (choix provisoire 1 du
   chantier 7) ; sinon, noter la taille souhaitée.
