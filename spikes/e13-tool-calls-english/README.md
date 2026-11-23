# E13 : appels d'outils du modèle local en anglais

Essai jetable. Rejoue le protocole de l'essai [E7](../e7-local-tool-calls/README.md) (même modèle, réflexion
coupée, température 0,2, contexte 4096, 3 essais par demande) avec trois montages :

- `en` : message système, descriptions d'outils et demandes en anglais ;
- `fr` : tout en français ;
- `mixed` : descriptions d'outils en anglais, message système et demandes en français.

L'outil « passer la main » devient `hand_off_to_claude` avec `kind` (`question` ou `task`) et `reason`.

```bash
dotnet run -c Release -- qwen3:14b 3 all     # ou en, fr, mixed
```

Variable `JARVIS_OLLAMA` pour l'adresse (par défaut `http://localhost:11435`). Journal complet de la mesure :
[run-2026-11-20.txt](run-2026-11-20.txt). Résultats : [RESULTS.md](RESULTS.md).
