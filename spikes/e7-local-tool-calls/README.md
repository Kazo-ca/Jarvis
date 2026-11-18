# E7 : appels d'outils du modèle local en français

Essai jetable. Envoie 14 demandes parlées (français et anglais) à un modèle Ollama avec 7 outils, dont
« passer la main à Claude », et compte combien de fois il choisit le bon outil.

```bash
dotnet run -c Release -- local-ia-active:latest 3
dotnet run -c Release -- qwen3:14b 3
```

Variable `JARVIS_OLLAMA` pour l'adresse (par défaut `http://localhost:11435`). Résultats : [RESULTS.md](RESULTS.md).
