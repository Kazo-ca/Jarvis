# Résultats E11 — 20 novembre 2026

**E11 : réussi. Recommandation : rendu WPF, fenêtre créée une fois et gardée cachée.** Sur l'écran principal
de Benoit (4K à 150 %, éventail de 3840 × 2088 pixels), l'éventail fermé et l'éventail ouvert immobile coûtent
moins de 0,5 % d'un cœur. L'animation coûte cher mais peu longtemps : environ un tiers d'un cœur pendant
0,4 s. La première image arrive 6 ms après le double-clic avec une fenêtre gardée cachée. Les clics passent
où il faut dans les quatre montages, à une condition : remettre l'orbe au-dessus quand la fenêtre de l'éventail
est activée. Le rendu dans WebView2 marche aussi, gardé caché ; recréé à chaque ouverture, il met 2,4 s à
apparaître.

## Mesures (pourcentage d'un cœur, processus WPF et processus WebView2, méthode de l'essai E6)

Chaque montage : 20 s sans éventail, une ouverture de chauffe, 20 s éventail fermé, 8 cycles ouverture et
fermeture par vrai double-clic sur l'orbe, 20 s éventail ouvert et immobile. Animation : 400 ms à l'ouverture,
300 ms à la fermeture, 30 images par seconde au plus.

| Montage | Sans éventail | Éventail fermé | Ouvert, immobile | Pendant l'ouverture | Pendant la fermeture | Cycle entier ¹ | Double-clic → première image |
| --- | --- | --- | --- | --- | --- | --- | --- |
| WPF, recréée à chaque ouverture | 1,07 % | 0,23 % | 0,08 % | 57 % (42 à 80) | 33 % (26 à 44) | 20 % | 79 ms (60 à 97) |
| **WPF, gardée cachée** | 0,92 % | **0,46 %** | **0,31 %** | **33 % (24 à 40)** | **26 % (19 à 41)** | **13 %** | **6 ms (4 à 10)** |
| WebView2, recréée à chaque ouverture | 0,69 % | 0,23 % | 1,08 % | non mesurable ² | non mesurable ² | — | **2 422 ms** (2 395 à 2 447) |
| WebView2, gardée cachée | 0,62 % | 0,62 % | 0,62 % | 54 % (30 à 90) | 30 % (13 à 53) | 18 % | 21 ms (19 à 23) |

¹ Ouverture, 1,2 s ouvert, fermeture, 1,2 s fermé.
² Les processus WebView2 naissent et meurent pendant la mesure : le calcul par différence ne tient pas.

- La différence entre « sans éventail » et « éventail fermé » (moins de 1 %) est du bruit de mesure : la
  première mesure suit le démarrage. Fermé, l'éventail ne coûte rien de mesurable, comme le veut l'ADR 0010,
  section 5, y compris quand sa fenêtre WPF est gardée cachée. WebView2 caché garde ses processus vivants
  (environ 0,2 % d'un cœur de plus, et sa mémoire, non mesurée).
- Ouvert et immobile, aucune minuterie ne tourne : l'image fixe ne coûte rien, même sur 8 millions de pixels.
- Le coût de l'animation vient de la fenêtre « layered » : chaque image de 3840 × 2088 est recopiée par le
  processeur (constat de l'essai E6). Recréer la fenêtre ajoute environ 25 points pendant l'ouverture.

## Fluidité

Images comptées par `CompositionTarget.Rendering` (images WPF réellement composées) pendant l'animation :

| Montage | Ouverture (400 ms) | Fermeture (300 ms) | Plus long écart entre deux images |
| --- | --- | --- | --- |
| WPF, recréée | 11 à 14 images | 10 à 11 images | 33 à 84 ms |
| WPF, gardée cachée | 9 à 16 images | 10 à 12 images | 33 à 102 ms |
| WebView2, gardée cachée | 16 à 24 copies ³ | 7 à 18 copies ³ | 33 à 85 ms |

³ Pour WebView2, ce sont les copies de la capture de la page dans la fenêtre WPF ; la page elle-même dessine
au plus 30 images par seconde par sa minuterie.

À 30 images par seconde, 400 ms donnent 12 images : la cadence est tenue, avec de temps en temps une image
sautée (un écart de 67 à 102 ms, soit une ou deux images manquées par animation au pire). Personne n'a regardé
l'animation : la fluidité perçue reste à juger par Benoit à la construction de l'éventail.

## Passage des clics (vrais clics de souris envoyés à Windows)

Identique dans les quatre montages :

| Clic | Reçu par | Attendu |
| --- | --- | --- |
| Sur un panneau cyan, éventail ouvert | le panneau | oui |
| Sur le panneau ambre | le panneau | oui |
| Simple clic sur l'orbe, éventail ouvert | l'orbe (après 500 ms), l'éventail reste ouvert | oui |
| Sur le bureau assombri, au-dessus de l'application dessous | l'éventail (il se range), pas l'application | oui |
| Au même endroit, éventail fermé | **l'application dessous** | oui |
| Double-clic sur l'orbe (16 par montage pendant les cycles) | ouverture ou rangement, **jamais un simple clic** (0 sur 64) | oui |

**Piège trouvé :** un clic sur un panneau **active** la fenêtre de l'éventail, qui passe alors au-dessus de
l'orbe dans la bande « toujours au premier plan ». Au premier essai, le clic suivant sur l'orbe est tombé sur le
bureau assombri et a rangé l'éventail. Correction appliquée : à chaque `Activated` de la fenêtre de l'éventail,
l'orbe est remis au sommet (`SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`). Une fenêtre `WS_EX_NOACTIVATE` serait
l'autre voie, mais la touche Échap de l'ADR 0010 demande le focus clavier.

## Simple clic retardé

`GetDoubleClickTime` vaut **500 ms** sur le PC de Benoit. Simple clic sur l'orbe, du bouton enfoncé au
traitement : **497 à 498 ms en moyenne** selon le montage (12 mesures, de 490 à 516 ms). C'est le délai de
l'ADR 0010 : la demi-seconde se sent, mais le mot d'éveil et le raccourci ne sont pas retardés. Le confort
ressenti n'a pas été jugé par un humain.

## Fenêtre créée à chaque ouverture ou gardée cachée

**Gardée cachée.** En WPF, elle ouvre en 6 ms au lieu de 79 ms et coûte 24 points de moins pendant
l'ouverture ; fermée, elle ne coûte rien de mesurable. En WebView2, elle est la seule possible : recréer le
contrôle met 2,4 s avant la première image. Si les panneaux sont des vues Razor (`BlazorWebView`), la fenêtre
de l'éventail doit être créée au démarrage, cachée, et ne jamais être détruite.

## Pièges rencontrés

- **Un gestionnaire `CompositionTarget.Rendering` abonné en permanence fait dessiner WPF à chaque image.** Au
  premier passage ([results-2026-11-20-first-pass.txt](results-2026-11-20-first-pass.txt)), l'éventail fermé
  coûtait 3 à 5 % d'un cœur pour cette seule raison, et le montage WebView2 recréé a dépassé le délai d'attente
  de 6 s. Refait en ne s'abonnant que pendant l'animation, avec 20 s d'attente.
- Une horloge d'animation WPF laissée attachée continue de tourner : après l'animation, l'essai retire les
  animations et pose les valeurs finales (`BeginAnimation(..., null)`).
- `PointToScreen` donne des pixels physiques : l'application déclare `PerMonitorV2` dans son manifeste pour que
  les clics envoyés tombent juste à 150 %.

## Non vérifié

- Le jugement humain de la fluidité et du confort du simple clic.
- Les autres écrans de Benoit (trois autres écrans, à une autre échelle) et un portable.
- La mémoire des montages (WebView2 caché garde ses processus).
- Les vues Razor réelles dans `BlazorWebView` : l'essai utilise une page HTML dans `WebView2CompositionControl`,
  le même moteur.
- Le mouvement réduit de Windows (l'éventail apparaît d'un coup : rien à mesurer).

Relevé complet de la mesure retenue : [results-2026-11-20.txt](results-2026-11-20.txt).
