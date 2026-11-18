# Résultats E8 — 18 novembre 2026

**E8 : réussi.** Le pilote C# lance `claude -p` en flux JSON dans les deux sens, sans Node ni Python, et
obtient tout ce dont Jarvis a besoin : l'identifiant de session, chaque appel d'outil, le texte, le coût, et
la liste des actions refusées. Avec seulement l'outil `Read` autorisé, Claude a lu `note.txt` et répondu
« le code du portail est **4711** » ; ses deux tentatives d'écrire `done.txt` ont été refusées et le fichier
n'existe pas.

## Mesures (Claude Code 2.1.312, première exécution réussie après `/login`)

| | |
| --- | --- |
| Lecture du fichier (premier `tool_use`) | moins de 9 s après le lancement |
| Réponse finale | 20,9 s, résultat à 25,1 s |
| Coût | 0,64 $ (contexte et hooks de l'utilisateur chargés, voir plus bas) |
| Écritures refusées | 2, listées dans `result.permission_denials` avec le chemin et le contenu |

Événements reçus : `system/init`, `assistant`, `user` (résultats d'outils), `system/permission_denied`,
`rate_limit_event`, `system/task_summary`, `system/post_turn_summary`, `system/hook_started`,
`system/hook_response`, `result/success`. `system/permission_denied` arrive **pendant** le tour : Jarvis peut
l'annoncer tout de suite, sans attendre la fin.

## Recommandation : la ligne de commande, pas de pont vers le SDK

| | Ligne de commande `claude -p` | Claude Agent SDK |
| --- | --- | --- |
| Langage | Processus lancé depuis C#, JSON ligne par ligne | TypeScript ou Python seulement |
| Exécution sans Python | Oui | Non (Python) ou Node.js à embarquer |
| Fonctionnement | C'est le même programme que le SDK lance en dessous | Enveloppe la ligne de commande |
| Conversation suivie | `--input-format stream-json` garde l'entrée ouverte ; `--resume <id>` reprend | Oui |
| Permissions | `--allowedTools`, `--permission-mode`, `--add-dir`, `--permission-prompt-tool` | Rappel `canUseTool` |
| Plafond | `--max-budget-usd`, `--max-turns` | Oui |

Pour US2.10 et US2.15 (confirmer une écriture à la voix ou sur l'orbe), Jarvis expose un petit outil MCP local
passé par `--permission-prompt-tool` : Claude Code l'appelle à chaque demande, Jarvis demande l'accord et
répond oui ou non. **Pas encore essayé** ; le refus simple, lui, est prouvé.

## À retenir pour la conception

- **Les hooks et réglages de l'utilisateur s'appliquent aux sessions lancées par Jarvis.** Ici, un hook de
  Benoit a intercepté la première écriture avant le refus de permission. Pour un comportement prévisible
  (et moins cher), Jarvis devra choisir ses sources de réglages (`--setting-sources`) et son propre fichier
  de réglages.
- La connexion du CLI peut expirer à part de Claude Desktop : Jarvis doit détecter « OAuth session expired »
  et demander `/login`.
- Une session lancée par Jarvis apparaît dans le registre d'E2 (`entrypoint` différent), donc dans la liste
  des sessions du PC sans code de plus.
