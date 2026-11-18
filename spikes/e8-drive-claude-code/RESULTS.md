# Résultats E8 — 18 novembre 2026

**E8 : façon de faire choisie, exécution complète bloquée par la connexion.** Le pilote C# lance
`claude -p` en flux JSON dans les deux sens, reçoit les événements (`system/init` avec l'identifiant de
session, `assistant`, `result`) et lit le résultat. L'appel au modèle a échoué parce que la connexion du
Claude Code en ligne de commande a expiré sur ce PC : « OAuth session expired and could not be refreshed ».
Les sessions de Claude Desktop, elles, ont leur propre connexion et fonctionnent.

## Recommandation : la ligne de commande, pas de pont vers le SDK

| | Ligne de commande `claude -p` | Claude Agent SDK |
| --- | --- | --- |
| Langage | Processus lancé depuis C#, JSON ligne par ligne | TypeScript ou Python seulement |
| Exécution sans Python | Oui | Non (Python) ou Node.js à embarquer |
| Fonctionnement | C'est le même programme que le SDK lance en dessous | Enveloppe la ligne de commande |
| Conversation suivie | `--input-format stream-json` garde l'entrée ouverte ; `--resume <id>` reprend | Oui |
| Permissions | `--allowedTools`, `--permission-mode`, `--add-dir`, `--permission-prompt-tool` (outil MCP qui répond aux demandes) | Rappel `canUseTool` |
| Plafond | `--max-budget-usd`, `--max-turns` | Oui |
| Sortie structurée | `--json-schema` | Oui |

Le SDK n'apporte rien que la ligne de commande n'offre pas, et il impose un environnement Node ou Python,
contraire à la décision n° 1. Pour US2.10 et US2.15 (confirmer une écriture à la voix ou sur l'orbe), Jarvis
expose un petit outil MCP local passé par `--permission-prompt-tool` : Claude Code l'appelle à chaque demande
de permission, Jarvis demande l'accord et répond oui ou non.

## Ce qui reste à faire après reconnexion

Lancer `claude` dans un terminal, taper `/login`, puis :

```bash
cd spikes/e8-drive-claude-code
dotnet run -c Release
```

L'essai vérifie alors la lecture de `note.txt` (outil autorisé) et le refus d'écrire `done.txt` (outil non
autorisé, attendu dans `permission_denials`). L'outil MCP de permission reste à essayer.
