# E8 : piloter Claude Code depuis C#

Essai jetable. Lance `claude -p` en flux JSON (entrée et sortie), envoie une demande dans un dossier
temporaire, affiche chaque événement reçu et vérifie qu'une écriture est refusée quand seule la lecture
est autorisée.

```bash
dotnet run -c Release            # dossier temporaire %TEMP%\jarvis-e8
dotnet run -c Release -- C:\un\dossier
```

Demande que `claude` soit connecté (`/login`). Résultats : [RESULTS.md](RESULTS.md).
