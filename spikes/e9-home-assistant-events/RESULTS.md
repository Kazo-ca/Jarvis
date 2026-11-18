# Résultats E9 — 18 novembre 2026

**E9 : la réponse est non, par la documentation ; le plan B reste à essayer.** Les événements de Home
Assistant ne peuvent pas arriver par MCP. La documentation de l'intégration « Model Context Protocol Server »
liste ses limites : outils et invites pris en charge, une seule ressource en lecture (photo de l'état des
entités exposées), **notifications non prises en charge**. Sans notification, un client MCP ne peut que
demander l'état, pas être prévenu d'un changement.

Source : [mcp_server, « Known limitations »](https://www.home-assistant.io/integrations/mcp_server/#known-limitations).

## Conséquence pour la conception

- **Commandes et questions** (US2.3, « allume le salon ») : par MCP, comme prévu par la décision n° 14.
- **Déclencheurs** (US3.3) : par l'API WebSocket de Home Assistant, `subscribe_trigger` (mêmes déclencheurs
  que les automatisations : changement d'état, seuil numérique, heure, etc.) ou `subscribe_events` filtré
  sur `state_changed`. Il faut un jeton d'accès longue durée, rangé dans le coffre.
- Interroger l'état toutes les quelques secondes par MCP serait possible mais lent et coûteux ; à écarter.

## Ce qui demande Benoit

L'essai du plan B demande l'adresse de Home Assistant et un jeton d'accès longue durée (Profil, Sécurité,
Jetons d'accès longue durée). `config.local.yaml` de jarvis.py n'en contient pas, et je n'ai pas repris les
jetons des serveurs MCP configurés dans Claude. Une fois le jeton déposé dans `C:\jarvis\temp`, l'essai est
court : se connecter en WebSocket depuis C#, s'abonner à un déclencheur sur une lumière, l'allumer, mesurer
le délai.
