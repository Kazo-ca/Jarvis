# E11 : fenêtre transparente de l'éventail des tâches

Essai jetable pour l'[ADR 0010](../../doc/fr/dev/adr/0010-task-fan-and-orb-click.md), section 7. Une application
WPF (.NET 10) affiche :

- l'orbe : fenêtre transparente de 160 × 160, toujours au premier plan, dans le coin en bas à droite de l'écran
  principal ; un double-clic ouvre ou range l'éventail, un simple clic n'est traité qu'après le délai de
  double-clic du système (`GetDoubleClickTime`) ;
- l'éventail : une seconde fenêtre transparente sans bordure qui couvre toute la zone de travail de l'écran,
  assombrit le bureau et montre 4 panneaux en arc (le quatrième ambre) avec un trait pointillé vers l'orbe ;
- une fenêtre ordinaire, « l'application dessous », sous l'éventail.

Deux rendus (`--render`) : `wpf` (formes et animations WPF plafonnées à 30 images par seconde) et `webview`
(tout l'éventail dans un `WebView2CompositionControl`, comme les vues Razor du produit, animé par une minuterie
JavaScript à 30 images par seconde). Deux façons de tenir la fenêtre (`--mode`) : `recreate` (créée à chaque
ouverture, fermée à chaque rangement) et `hidden` (créée une fois, cachée entre deux ouvertures).

Tout est piloté par de vrais clics de souris envoyés à Windows (`SetCursorPos` et `mouse_event`), sans
intervention humaine : mesure sans éventail, une ouverture de chauffe, éventail fermé, 8 cycles
d'ouverture et de fermeture par double-clic, éventail ouvert et immobile, passage des clics, simple clic. Le
processeur se mesure comme dans l'essai [E6](../e6-orb-window/README.md) : processus WPF et ses processus
enfants WebView2, en pourcentage d'un cœur.

```bash
dotnet build -c Release
cd bin/Release/net10.0-windows10.0.19041.0
./TaskFanWindow.exe --render wpf --mode hidden --cycles 8 --seconds 20
cat results.txt
```

Les fenêtres apparaissent sur l'écran principal pendant environ une minute et demie par montage, le pointeur
de la souris bouge seul, puis tout se ferme et le pointeur revient à sa place. Résultats : [RESULTS.md](RESULTS.md).
