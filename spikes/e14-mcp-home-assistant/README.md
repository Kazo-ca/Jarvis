# E14 : client MCP C# contre Home Assistant

Essai jetable. Client MCP écrit avec le paquet NuGet officiel `ModelContextProtocol` 2.4.0, contre le serveur
MCP de Home Assistant (intégration « Model Context Protocol Server »). Lit l'adresse et le jeton dans le même
fichier local que l'essai E9 (hors du dépôt) : `C:\jarvis\temp\home-assistant.md`, lignes `Host: https://…` et
`Token: …`.

```bash
dotnet build -c Release
dotnet run -c Release --no-build -- transport          # HTTP « streamable » puis SSE
dotnet run -c Release --no-build -- tools              # outils, schémas, AIFunction
dotnet run -c Release --no-build -- context            # un appel en lecture (GetLiveContext)
dotnet run -c Release --no-build -- light "<nom>"      # allume une lumière éteinte, puis la remet éteinte
dotnet run -c Release --no-build -- reconnect          # coupure simulée, puis reprise
```

L'essai n'appelle jamais de script, de serrure, d'alarme, de volet ni de thermostat. L'étape `light` ne fait
rien si la lumière n'est pas éteinte au départ. Résultats : [RESULTS.md](RESULTS.md).
