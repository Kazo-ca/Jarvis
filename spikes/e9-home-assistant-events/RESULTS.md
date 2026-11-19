# Résultats E9 — 18 novembre 2026

**E9 : non par MCP, oui par l'API WebSocket.** Les événements de Home Assistant ne peuvent pas arriver par
MCP : la documentation de l'intégration « Model Context Protocol Server » indique que les **notifications ne
sont pas prises en charge** ([Known limitations](https://www.home-assistant.io/integrations/mcp_server/#known-limitations)).
Le plan B est essayé et fonctionne : un client C# de 120 lignes, sans bibliothèque, reçoit les événements et
les déclencheurs de Home Assistant par son API WebSocket.

## Mesures (Home Assistant 2026.10.2, `ha.kazo.ca`, 40 s, lecture seule)

| | |
| --- | --- |
| Connexion et authentification par jeton longue durée | `auth_ok` |
| `subscribe_events` sur `state_changed` | 96 événements en 40 s (91 capteurs, 5 capteurs binaires) |
| `subscribe_trigger` avec un déclencheur `time_pattern` toutes les 10 s | 4 déclenchements sur 4, reçus à moins de 40 ms de la seconde ronde |
| Délai `time_fired` → reçu | non mesurable finement : l'horloge du PC est environ 240 ms en avance sur celle de Home Assistant |

L'essai n'appelle aucun service et ne change rien dans la maison.

## Conséquence pour la conception

- **Commandes et questions** (US2.3, « allume le salon ») : par MCP, comme prévu par la décision n° 14.
- **Déclencheurs** (US3.3) : par l'API WebSocket. `subscribe_trigger` accepte les mêmes déclencheurs que les
  automatisations (changement d'état, seuil numérique, heure, modèle) et c'est Home Assistant qui les évalue :
  Jarvis n'a pas à filtrer le flot d'événements. C'est la voie recommandée.
- Le jeton longue durée va dans le coffre. Un adaptateur Home Assistant porte donc deux canaux (MCP et
  WebSocket) avec la même adresse et le même jeton.

## Relancer

```bash
dotnet run -c Release -- 40 C:\jarvis\temp\home-assistant.md
```

Le fichier contient `Host: https://…` et ``Token: `…` `` ; il reste hors du dépôt.
