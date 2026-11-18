# E6 : orbe animé dans une fenêtre WPF transparente

Essai jetable. Une fenêtre WPF transparente, sans bordure et toujours au premier plan affiche un orbe animé
de plusieurs façons, puis mesure le processeur consommé par le processus et tous ses processus enfants
(WebView2) pendant 30 s :

- `--mode webview` : Canvas 2D dans `WebView2CompositionControl` (seul mode WebView2 qui garde la
  transparence par pixel dans une fenêtre WPF transparente, constaté dans Kazo Bar) ;
- `--mode wpf` : le même dessin refait par WPF à chaque image ;
- `--mode wpfanim` : formes WPF construites une fois, seules les rotations et l'opacité sont animées ;
- `--mode none` : fenêtre vide, pour la référence.

```bash
dotnet build -c Release
cd bin/Release/net10.0-windows10.0.19041.0
./OrbWindow.exe --mode webview --fps 30 --state idle --seconds 30
cat results.txt
```

La fenêtre apparaît à (1500, 700) sans prendre le focus et se ferme seule. Résultats : [RESULTS.md](RESULTS.md).
