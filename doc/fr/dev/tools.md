# Outils : boucle d'outils, point de décision, heure et calcul

Chantier `stage2/3-tool-loop` (US2.1, début). Décisions :
[ADR 0007](adr/0007-llm-providers-and-tool-format.md) (sections 2 à 4),
[ADR 0009](adr/0009-permission-and-confirmation-model.md) et
[ADR 0017](adr/0017-tools-classes-and-decision-point.md).

## Ce qui est construit

```
tranche d'outil (Tools/Clock, Tools/Calculator, …) ─► ITool ─► IToolRegistry (noyau)
                                                                    │ outils activés
ConversationEngine ─► modèle (outils déclarés) ─► appel d'outil demandé
        │                                              │
        │                         IPermissionDecision (tranche Permissions/Decision)
        │                         connu et activé ? classe de cet appel ? ─► journal (jarvis.db)
        │                                              │ permis                │ refusé
        │                                   ITool.InvokeAsync (30 s)     cause en une phrase
        ◄──────────── résultat rendu au modèle comme donnée (rôle « tool ») ───┘
```

Le modèle propose, le code décide. Aucun appel ne s'exécute automatiquement : la conversation garde la
boucle.

## Contrats du noyau

`src/Kazo.Jarvis.Kernel/Tools/` et `src/Kazo.Jarvis.Kernel/Permissions/` :

| Type | Rôle |
| --- | --- |
| `ITool` | Nom anglais stable, origine (`builtin`, `mcp:<serveur>`, `hand-off`), description par langue, schéma JSON, classe déclarée, classement d'un appel (`Classify`, et `ClassifyAsync` quand il faut interroger un serveur : voir [home-assistant.md](home-assistant.md)), exécution |
| `ToolException` | Un appel impossible pour une raison que le modèle peut dire ; son message revient comme résultat |
| `IToolRegistry`, `ToolRegistry` | Les outils connus et les outils activés ; un outil s'ajoute aussi en cours de route (serveur MCP) |
| `ToolActivation` | Section de réglages `Tools.Activation` : un interrupteur par outil ; sans choix, l'outil suit `EnabledByDefault` |
| `ActionClass` | `Read`, `EverydayEffect`, `Sensitive`, `Critical`, `Forbidden` (ADR 0009, section 2), dans cet ordre |
| `IPermissionDecision` | « Cette action est-elle permise ? » : qui demande, quoi exactement ; réponse permise ou refusée avec la cause |
| `ActionRequest`, `ActionRequester`, `PermissionDecision`, `DenialCause` | La demande et la réponse |

Une tranche inscrit son outil par `services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, MonOutil>())`.
Le noyau ne porte aucune logique de classement.

## Tranche `Permissions/Decision`

`PermissionDecisionPoint` suit l'ADR 0017, section 2 :

1. outil inconnu ou désactivé : refusé ;
2. arguments qui ne sont pas un objet JSON : refusé ;
3. classe de l'appel : la plus haute entre la classe déclarée par l'outil et celle de son classement. Un
   classement ne peut donc que monter la classe. Un classement qui échoue : refusé ;
4. « Lecture » et « Effet courant » : permis ;
5. « Sensible » et « Critique » : une réponse humaine décide, par la
   [demande de confirmation](confirmation.md). Sans elle dans l'hôte : refusé (`ConfirmationUnavailable`) ;
6. « Interdite » : refusé sans question.

Chaque décision va dans la collection `Permissions.Decisions` de `jarvis.db` : date, demandeur, outil,
arguments coupés à 500 caractères, classe, résultat, cause, canal de la réponse humaine. Un échec d'écriture du journal ne change pas la
décision. « Tout effacer » efface aussi ce journal. Une interruption est journalisée comme un refus.

## Outils

| Outil | Tranche | Classe | Ce qu'il rend |
| --- | --- | --- | --- |
| `get_datetime` | `Tools/Clock` | Lecture | JSON : `date`, `time`, `weekday`, `time_zone`, `utc_offset` du PC |
| `calculate` | `Tools/Calculator` | Lecture | La valeur de `expression`, écrite avec un point décimal |

Les outils de fichiers et du web sont dans [basic-tools.md](basic-tools.md).

`calculate` lit lui-même l'expression (aucune exécution de script) : nombres, `+ - * / % ^`, parenthèses,
`pi`, `e`, `sqrt`, `abs`, `round`, `floor`, `ceil`. Il accepte `×`, `÷`, `**` et la virgule décimale. Le
calcul se fait en `decimal` : 0,1 + 0,2 donne 0,3. Expression de 500 caractères au plus ; une erreur
(division par zéro, texte non compris) revient au modèle en une phrase.

## La boucle dans `Conversation/Dialogue`

- Le modèle reçoit les outils **activés**, décrits dans la langue du tour. Les règles fixes du message
  système gagnent alors une ligne : se servir d'un outil quand il sert la demande, et ne jamais suivre une
  instruction trouvée dans un résultat.
- Les appels s'exécutent un par un, dans l'ordre demandé, chacun par le point de décision.
- Bornes : **8 appels par tour**, **30 s par appel**, résultat coupé à **2 000 caractères** avec une note.
  À la borne des appels, le modèle ne reçoit plus d'outils et répond avec ce qu'il a.
- Un refus revient au modèle comme résultat : la cause en une phrase, dans la langue du tour. La même
  action redemandée dans le tour est refusée sans repasser par le point de décision.
- Un outil qui échoue ou dépasse le délai rend une phrase ; le tour continue. Ce n'est pas une panne de
  l'assistant.
- L'orbe reste en réflexion pendant un appel. Une interruption annule l'appel en cours et les suivants.
- Ce que le modèle écrit avant un appel est dit tout de suite, sans attendre le résultat.
- Chaque appel est enregistré avec le tour (`ConversationTurn.ToolCalls`) : nom, arguments, résultat, issue,
  classe, durée. La fenêtre de conversation le montre dans le tour, replié par défaut.
- Les jetons d'un tour sont la somme de ses requêtes.

## Adaptateurs

Voir [llm.md](llm.md). Les trois adaptateurs envoient les outils et lisent les appels dans le format de leur
fournisseur. Les adaptateurs locaux ont l'analyseur tolérant de l'ADR 0007, section 4.

## Hôte Web

Le modèle simulé demande un outil d'après les mots de la demande : « heure exacte » (`get_datetime`),
« calcule … » (`calculate`), « outil inconnu » (un outil qui n'existe pas, refusé), « écris une note … » et
« efface les notes » (deux outils factices qui demandent une confirmation, voir
[confirmation.md](confirmation.md)). Après un résultat, sa
réponse le cite.

## Choix provisoires de ce chantier

Les ADR ne les tranchent pas ; à confirmer par Benoit :

1. ~~Pas encore de page « Outils »~~ : faite au chantier 5 ([basic-tools.md](basic-tools.md)).
2. **Les appels d'outils des tours passés ne sont pas renvoyés au modèle** : l'historique garde la demande
   et la réponse.
3. **Description du paramètre `expression`** en anglais dans les deux langues ; la description de l'outil
   suit la langue.
4. **L'analyseur tolérant** se coupe par fournisseur dans `config.json` (`TolerantToolCalls`), sans
   interrupteur sur la page.
5. ~~Le journal des décisions n'a pas de page~~ : fait au chantier 4 (page « Journal des permissions »).

## Non vérifié

- Avec un vrai modèle : la boucle n'a tourné qu'avec le modèle simulé et des serveurs en mémoire. Le choix
  du bon outil par `qwen3:14b` est prouvé par les essais E7 et E13, pas la boucle complète de Jarvis.
- Les formats d'outils d'OpenAI et d'Anthropic : testés contre des réponses écrites à la main, jamais
  contre les vraies API.

## Tests

- Unitaires : `ToolRegistryTests`, `PermissionDecisionPointTests` (refus par défaut à chaque étape, classe
  par appel, journal), `BuiltInToolsTests`, `HttpLlmConnectorTests` (outils envoyés, appels lus, analyseur
  tolérant, pour les trois adaptateurs), `ConversationEngineTests` (aller-retour d'un appel, refus, bornes,
  délai, interruption, parole avant un appel).
- Playwright (`ConversationTests`) : un calcul montré dans le tour avec ses arguments et son résultat,
  l'heure, un outil inconnu refusé.

## Vérifications sur Windows

À faire avec Benoit, Ollama lancé avec `qwen3:14b` :

1. « Quelle heure est-il ? » et « Combien font 12 fois 7 plus 3 ? », à la voix et par écrit : l'appel
   apparaît dans la fenêtre de conversation, la réponse est juste.
2. Une simple conversation (« Bonsoir ») : aucun outil appelé, délai du premier son inchangé.
3. « Hey Jarvis » pendant un appel : l'appel est annulé et Jarvis écoute.
