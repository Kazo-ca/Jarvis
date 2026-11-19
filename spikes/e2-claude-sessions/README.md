# E2 / E3 : sessions Claude Code et Claude Desktop

Essai jetable, en lecture seule. Liste les sessions Claude Code du PC (terminal et Claude Desktop) avec leur
état, et suit leurs changements d'état. N'écrit jamais dans `%USERPROFILE%\.claude`.

```bash
dotnet run -c Release                 # liste
dotnet run -c Release -- --watch 60   # suit les transitions pendant 60 s
```

Résultats : [RESULTS.md](RESULTS.md).
