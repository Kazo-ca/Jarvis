# E12 : permissions et hooks des sessions Claude Code lancées par Jarvis

Essai jetable. Un programme console C# joue le rôle de Jarvis :

- un serveur local sur `127.0.0.1:47812` avec un serveur MCP (`/mcp`, paquet officiel
  `ModelContextProtocol.AspNetCore` 2.4.0, HTTP « streamable », sans état) qui expose l'outil de permission
  `approve`, et les routes des hooks (`/hook/<nom>`) ;
- il pilote `claude -p` en flux JSON (comme l'essai [E8](../e8-drive-claude-code/README.md)) dans le dossier
  jetable `C:\jarvis\temp\e12-sandbox`, avec `--permission-prompt-tool mcp__jarvis__approve`,
  `--mcp-config` et `--strict-mcp-config`, et des réglages propres à Jarvis passés par `--settings` ;
- la règle de l'essai : l'outil `approve` refuse tout ce dont l'entrée contient « deny », accepte le reste.

Les réglages de Jarvis (`jarvis-claude-settings.json`), la configuration MCP et le journal (`e12-log.jsonl`,
`e12-summary.txt`) sont écrits dans `C:\jarvis\temp\e12-jarvis`, hors du dépôt. Rien n'est écrit dans
`%USERPROFILE%\.claude\settings.json`.

```bash
dotnet build -c Release
dotnet run -c Release --no-build -- perm haiku          # permissions et hooks
dotnet run -c Release --no-build -- src-project haiku   # ce que charge --setting-sources project
dotnet run -c Release --no-build -- all haiku           # tous les scénarios
```

Scénarios : `perm`, `src-none` (`--setting-sources ""`), `src-user`, `src-project`, `src-local`,
`src-project-local`, `src-default` (sans l'option), `src-none-append` (`--append-system-prompt-file CLAUDE.md`),
`src-project-disable` (`disableAllHooks`). Demande que `claude` soit connecté (`/login`) et `curl` dans le
`PATH`. Résultats : [RESULTS.md](RESULTS.md).
