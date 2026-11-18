# Résultats E6 — 18 novembre 2026

**E6 : échec sur la cible.** Aucun montage essayé ne tient moins de 2 % d'un cœur avec un orbe animé en
veille. La transparence de WPF passe par une fenêtre « layered » : chaque image modifiée est recopiée par le
processeur, quelle que soit la technique de dessin. Seule une image fixe tient la cible.

## Mesures (pourcentage d'un cœur, processus WPF + processus WebView2, 30 s après 8 s de stabilisation)

| Montage | Images par seconde | Processeur |
| --- | --- | --- |
| Fenêtre transparente vide | — | 0,8 à 1,1 % |
| WebView2 composition, image fixe | 0 | **1,3 %** |
| WebView2 composition, Canvas 2D (minuterie) | 2 | 6,3 % |
| WebView2 composition, Canvas 2D (minuterie) | 5 | 7,0 % |
| WebView2 composition, Canvas 2D (minuterie) | 10 | 8,8 % |
| WebView2 composition, Canvas 2D (`requestAnimationFrame`) | 15 | 11,7 % |
| WebView2 composition, Canvas 2D (`requestAnimationFrame`) | 30 | 18,3 % |
| WebView2 composition, état « parole » | 30 | 13,9 % |
| WebView2 composition, Canvas 2D (`requestAnimationFrame`) | 60 | 27,0 % |
| WPF, dessin refait à chaque image | 15 | 4,0 % |
| WPF, dessin refait à chaque image | 30 | 5,8 % |
| WPF, animations de transformations seulement | 15 | 4,1 % |
| WPF, animations de transformations seulement | 30 | 7,1 % |
| WPF, animations de transformations seulement | 60 | 9,8 % |

Avec WebView2, environ la moitié du coût est dans le processus WPF : c'est la capture
(`GraphicsCaptureSession`) que le contrôle de composition utilise pour recopier la page dans la fenêtre.
Dès que le contenu bouge, ce coût ne descend pas sous 4 à 5 %, même à 2 images par seconde.

## Ce que ça veut dire pour la conception

La décision n° 6 (« Canvas ou WebGL dans la bibliothèque Razor partagée ») et la cible « moins de 2 % en
veille » ne tiennent pas ensemble sur Windows avec une fenêtre WPF transparente. Pistes, de la plus simple à
la plus lourde :

1. **Veille immobile.** L'orbe en veille est une image fixe (ou ne change qu'à la transition d'état), et
   l'animation complète ne tourne qu'en écoute, réflexion et parole, qui durent quelques secondes.
   Coût en veille mesuré : 1,3 %. Le composant Razor partagé reste tel que prévu.
2. **Accepter environ 6 % d'un cœur** pour une veille animée lentement (2 images par seconde).
3. **Rendu natif par le GPU pour l'orbe de bureau** (DirectComposition ou Win2D avec chaîne d'échange
   transparente, hors fenêtre « layered »). Non essayé : demande un essai de plus et un deuxième
   composant d'orbe que le mobile et l'hôte Web ne partagent pas.

## Non vérifié

- Le passage des clics autour de l'orbe (US1.1) n'est pas remesuré ici : Kazo Bar l'a établi pour
  `WebView2CompositionControl` en alpha zéro.
- WebGL n'a pas été essayé ; il passerait par la même capture.
- Les chiffres viennent d'un PC de bureau à 16 fils ; un portable consommera davantage.

## Pièges rencontrés

- Une boucle `requestAnimationFrame` qui ne dessine rien réveille quand même le moteur 60 fois par seconde.
  En dessous de 10 images par seconde, utiliser une minuterie.
