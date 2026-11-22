# Décisions d'architecture (ADR)

Un ADR par décision, numéroté, écrit et fusionné avant le code qu'il décrit.
Un ADR accepté ne se modifie pas : un nouveau le remplace. Le prochain numéro
libre est indiqué dans la [mémoire du projet](../../memory/README.md).

| ADR | Titre | Statut |
| --- | --- | --- |
| [0001](0001-target-dotnet-10-and-projects.md) | Cible .NET 10 et liste des projets | Accepté |
| [0002](0002-vertical-slices-kernel-and-dependency-rules.md) | Tranches verticales, noyau partagé et règles de dépendance | Accepté |
| [0003](0003-hosts-and-native-services.md) | Hôtes (Windows WPF, Web de test, Android) et services natifs | Accepté |
| [0004](0004-configuration-storage-and-secret-vault.md) | Configuration, stockage et coffre de secrets | Accepté |
| [0005](0005-assistant-state-machine-and-orb-contract.md) | Machine à états de l'assistant et contrat de l'orbe | Accepté |
| [0006](0006-voice-chain.md) | Chaîne vocale (mot d'éveil, reconnaissance, synthèse) | Accepté |
| [0007](0007-llm-providers-and-tool-format.md) | Fournisseurs LLM et format des outils | Accepté |
| [0008](0008-languages-and-localization.md) | Langues et localisation | Accepté |
| [0009](0009-permission-and-confirmation-model.md) | Modèle de permissions et de confirmation | Accepté |
| [0010](0010-task-fan-and-orb-click.md) | Éventail des tâches et clic sur l'orbe | Accepté |
| [0011](0011-orb-colors-and-drawing.md) | Couleurs et dessin de l'orbe | Accepté |
| [0012](0012-scripts-allowed-in-markup.md) | Le script est permis dans le balisage (remplace en partie 0002) | Accepté |
| [0013](0013-relais-readiness.md) | Préparer le module Relais (v2) : projets, sujets, tâches et orchestrateur | Accepté |
| [0014](0014-license-kazo-bar-rule-and-jarvis-py.md) | Licence Apache 2.0, règle sur Kazo Bar (copie permise, aucune référence) et retrait de `jarvis.py` | Accepté |
| [0015](0015-work-tracking-source.md) | Source de suivi du travail (GitHub Issues ou Azure Boards) et lien obligatoire des commits et des PR | Accepté |
| [0016](0016-onnx-runtime-managed-on-sherpa-native.md) | Un seul ONNX Runtime natif, celui de sherpa-onnx (remplace en partie 0001) | Accepté |
| [0017](0017-tools-classes-and-decision-point.md) | Outils de base, classe de chaque outil et point de décision des permissions (navigateur de la section 3 remplacé par 0022) | Accepté |
| [0018](0018-mcp-client-and-home-assistant.md) | Client MCP, Home Assistant et domotique de sécurité (précise 0009 pour les scripts Home Assistant) | Accepté |
| [0019](0019-conversation-memory.md) | Mémoire de conversation : faits retenus, résumé des anciens échanges, page « Mémoire » | Accepté |
| [0020](0020-hand-off-targets-and-spend-cap.md) | Passage de main, cibles et plafond de dépense | Accepté |
| [0021](0021-claude-code-sessions.md) | Sessions Claude Code : lancement, permissions, hooks, annonces | Accepté |
| [0022](0022-in-app-browser-for-web-search.md) | Recherche web dans un navigateur interne à Jarvis (remplace en partie 0017) | Accepté |
| [0023](0023-confirmation-request-in-a-turn.md) | La demande de confirmation dans un tour : condition et aspect « interaction requise », réponse vocale par liste fermée, aucun délai (complète 0005) | Accepté |
| [0024](0024-announcement-turn.md) | Le tour d'annonce : une ligne de plus dans la table des transitions, pour dire à voix haute ce que personne n'a demandé (complète 0005) | Accepté |
| [0025](0025-task-agents-and-their-memories.md) | Un agent par genre de tâche : fichiers Markdown, appel de tri, mémoire par agent et par projet (complète 0017 et 0019) | Proposé |
| [0026](0026-projects-subjects-and-their-threads.md) | La vue des projets, des sujets et de leurs fils : rail des projets, fil du sujet et fil de la tâche, réponse « T3> », projet Général (complète 0013 et 0025) | Proposé |

Les neuf premiers ADR sont ceux de l'étape 1, listés dans le
[document de conception](../plans/design.md#méthode-de-travail-et-premiers-adr).
Les ADR des étapes suivantes s'écrivent au début de chaque série.

Les images `0010-task-fan-*.png` sont les maquettes de l'ADR 0010. Elles ne sont rangées qu'ici ; la
version anglaise de l'ADR y renvoie.
