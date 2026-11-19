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
| [0010](0010-task-fan-and-orb-click.md) | Éventail des tâches et clic sur l'orbe | Proposé |

Les neuf premiers ADR sont ceux de l'étape 1, listés dans le
[document de conception](../plans/design.md#méthode-de-travail-et-premiers-adr).
Les ADR des étapes suivantes s'écrivent au début de chaque série.

Les images `0010-task-fan-*.png` sont les maquettes de l'ADR 0010. Elles ne sont rangées qu'ici ; la
version anglaise de l'ADR y renvoie.
