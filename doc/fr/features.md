# Fonctionnalités

## Disponibles

*(aucune pour l'instant)*

## En développement

| Fonctionnalité | Étape | Référence |
| --- | --- | --- |
| Orbe, voix, modèle local et paramètres : construit et fusionné dans `main` le 20 novembre 2026, recette sur Windows à faire | 1 | [Conception](dev/plans/design.md), [plan de la série](dev/plans/stage-1.md) |
| Outils, MCP, passage de main et sessions Claude. Construit : la boucle d'outils, le point de décision des permissions, les outils date et heure et calcul ([détail](dev/tools.md)) ; la demande de confirmation avant une action sensible ou critique, à la voix ou au clic, et le journal des permissions ([détail](dev/confirmation.md)) ; la lecture de fichiers dans les dossiers autorisés, la recherche web et la lecture d'une page par le navigateur interne de Jarvis (désactivées au départ), la page « Outils » ([détail](dev/basic-tools.md)) ; les serveurs MCP ajoutés par adresse ou par commande, leurs outils activés un par un ([détail](dev/mcp.md)) ; Home Assistant par MCP, où une lampe s'allume sans question et où une serrure, l'alarme ou la porte de garage exigent un clic ([détail](dev/home-assistant.md)) ; la mémoire de conversation : faits retenus sur demande ou automatiquement à partir des seuls mots de l'utilisateur, résumé des anciens échanges, page « Mémoire » pour voir et effacer ([détail](dev/conversation-memory.md)) ; le passage de main : une cible cloud choisie par l'utilisateur (aucune au départ) répond quand le modèle local lui passe la question ou quand la demande commence par « Demande à Claude », et la conversation dit qui a répondu ([détail](dev/hand-off.md)) ; le plafond de dépense : un prix par fournisseur cloud, le coût de chaque réponse compté, un compteur du mois visible, un avertissement à 80 %, et au plafond (10 $ US par mois au départ) Jarvis le dit et reste en local ([détail](dev/spend-cap.md)) | 2 | [Conception](dev/plans/design.md), [plan de la série](dev/plans/stage-2.md) |
| Éventail des tâches : un double-clic sur l'orbe en fait sortir les tâches en cours | 2 | [ADR 0010](dev/adr/0010-task-fan-and-orb-click.md) |
| Mode autonome, déclencheurs et courriel | 3 | [Conception](dev/plans/design.md) |
| Application mobile Android | 4 | [Conception](dev/plans/design.md) |
| Relais : Claude, ChatGPT et les autres agents se parlent par MCP et hooks, sous les yeux de l'humain, et leur travail est suivi en projets, sujets, tâches et sous-tâches avec un orchestrateur | v2 | [ADR 0013](dev/adr/0013-relais-readiness.md) |
| Suivi du travail dans GitHub Issues ou Azure Boards : une source réglée par projet, chaque commit et chaque PR d'agent liés à une issue (`#<id>`) ou à un work item Azure DevOps (`AB#<id>`), et, en option, une issue ou un work item obligatoire par sujet et par tâche, que Jarvis crée, modifie et synchronise | v2 | [ADR 0015](dev/adr/0015-work-tracking-source.md) |
