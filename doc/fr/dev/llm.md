# Modèles de langage

Chantier `stage1/11-llm-providers` du [plan de l'étape 1](plans/stage-1.md). Décision :
[ADR 0007](adr/0007-llm-providers-and-tool-format.md) (fournisseurs, contrat `Microsoft.Extensions.AI`,
réglages du modèle local). Récits US1.8 (configurer les fournisseurs) et US1.9 (modèle local par défaut).

## Le contrat du noyau

`src/Kazo.Jarvis.Kernel/Llm/` ne dépend que du paquet `Microsoft.Extensions.AI.Abstractions` (10.11.1) :

| Type | Rôle |
| --- | --- |
| `LlmProvider`, `LlmTuning` | Un fournisseur tel qu'un adaptateur le reçoit : type, adresse, modèle, réglages, référence de la clé |
| `LlmProviderKind` | `Ollama`, `OpenAiCompatible`, `OpenAi`, `Anthropic` ; `IsLocal()` et `RequiresKey()` |
| `ILlmConnector` | Donne un `IChatClient` par fournisseur, teste la connexion, dit si le modèle est en mémoire, le précharge |
| `LlmFailure`, `LlmException`, `LlmConnectionResult` | La cause d'une panne (ADR 0007, section 8) |
| `IAssistantModel` | Le modèle par défaut : son client, l'état de son chargement, `EnsureLoadedAsync` avant un tour |
| `AssistantModelLoadChanged` | Événement publié quand le chargement du modèle change (« le modèle se charge ») |

La conversation (chantier 13) lit `IAssistantModel` sans connaître la tranche. Les adaptateurs n'utilisent
jamais l'invocation automatique des fonctions : Jarvis garde la boucle d'outils (ADR 0007, section 2).

## Les adaptateurs

`src/Kazo.Jarvis.Adapters/Llm/`, écrits à la main sur un seul `HttpClient` (délai de 5 minutes jusqu'au premier
octet, pour couvrir le chargement d'un modèle de 9 Go) :

| Type | Protocole | Points notables |
| --- | --- | --- |
| `ollama` | `/api/chat` en flux NDJSON | `think: false` toujours envoyé, renvoyé sans `think` si le modèle le refuse (le modèle est retenu) ; `keep_alive: -1` ; `num_ctx`, `temperature`, `num_predict` |
| `openai-compatible`, `openai` | `/v1/chat/completions` en SSE | `v1/` ajouté à une adresse sans chemin ; `max_tokens` (local) ou `max_completion_tokens` (OpenAI) ; jetons comptés avec `stream_options.include_usage` |
| `anthropic` | `/v1/messages` en SSE | `anthropic-version: 2026-10-21`, clé dans `x-api-key`, pas de `temperature` (refusée par cette version), message système dans son propre champ |

- **Tester la connexion** liste les modèles du serveur (`/api/tags`, `/v1/models`) et vérifie que le modèle y
  est. Ollama ajoute `:latest` à un nom sans étiquette. Un serveur `openai-compatible` qui ne liste aucun
  modèle est cru sur parole : `llama-server` sert celui avec lequel il a démarré.
- **Causes nommées :** adresse invalide, serveur injoignable, modèle absent, clé absente, clé refusée (401,
  403), délai dépassé, erreur du serveur. Le détail technique va au journal ; il ne contient jamais la clé.
- **Préchargement** (Ollama seulement) : un `/api/chat` sans message, avec le même `num_ctx` que les
  conversations, sinon Ollama recharge le modèle à la première question. `/api/ps` dit si le modèle est en
  mémoire ; les autres types ne savent pas le dire.
- **Clés :** lues dans le coffre à chaque requête, sous la référence `llm.<id>.apikey`. Un fournisseur cloud
  sans clé échoue avant tout appel réseau.
- **Outils** (chantier `stage2/3-tool-loop`, voir [tools.md](tools.md)) : chaque adaptateur envoie les outils
  déclarés dans `ChatOptions.Tools` et rend un appel comme `FunctionCallContent`, sans jamais l'exécuter.
  Ollama : `tools` et `message.tool_calls`, arguments en objet, résultat en rôle `tool` avec `tool_name`.
  `/v1/chat/completions` : appel reçu en morceaux par `index`, arguments en texte JSON, résultat avec
  `tool_call_id`. Anthropic : `input_schema`, blocs `tool_use` et `tool_result`, les résultats d'un même tour
  de boucle dans un seul message `user`. Des arguments illisibles marquent l'appel
  (`FunctionCallContent.Exception`) : le point de décision le refuse.
- **Analyseur tolérant** (`ollama`, `openai-compatible` ; ADR 0007, section 4) : une réponse entière qui
  est un seul objet JSON nommant un outil déclaré est un appel. Une réponse qui commence par `{` ou par une
  clôture de code est retenue jusqu'à sa fin ; toute autre part morceau par morceau. Réglage
  `TolerantToolCalls` du fournisseur, activé par défaut.

## La tranche `Llm/Providers`

`src/Kazo.Jarvis.Features/Llm/Providers/` :

| Fichier | Rôle |
| --- | --- |
| `LlmProvidersOptions.cs`, `LlmProviderSettings.cs` | Section `Llm.Providers` de `config.json` : la liste, l'identifiant du fournisseur par défaut, la validation |
| `AssistantModelKeeper.cs` | Met en œuvre `IAssistantModel` ; service d'arrière-plan qui précharge le modèle au démarrage et après chaque changement de réglage |
| `ProviderEditor.cs` | La logique de la page : brouillon, ajout, retrait, test avant enregistrement, enregistrement |
| `LlmProvidersSettingsPage.razor` (+ `.css`, `.resx`) | La page « Modèles de langage » de la fenêtre des paramètres |
| `LlmProvidersSlice.cs` | Enregistrement |

- **Valeurs par défaut :** un fournisseur `local`, Ollama sur `http://127.0.0.1:11434` avec `qwen3:14b`,
  réflexion coupée, contexte 4096, température 0,3, préchargé et gardé en mémoire (ADR 0007, section 5).
- **Le fournisseur par défaut doit être local à l'étape 1** (règle de validation) ; un fournisseur cloud se
  configure et se teste, son bouton « par défaut » est grisé. Il ne répond que par le passage de main, une
  fois choisi comme cible ([hand-off.md](hand-off.md)) ; `ILlmProviderList` donne la liste des fournisseurs
  aux autres tranches.
- **Préchargement :** au démarrage, puis après chaque changement du fournisseur par défaut. En cas d'échec
  (Docker qui démarre Ollama après l'ouverture de session, par exemple), nouvel essai après 15 s, 30 s, 1 min,
  2 min et 5 min, puis attente d'un tour ou d'un changement de réglage.
- **Avant un tour**, `EnsureLoadedAsync` demande à Ollama si le modèle est en mémoire. S'il a été libéré,
  l'événement `AssistantModelLoadChanged(Loading)` part (la conversation dira « le modèle se charge »), puis
  le modèle est rechargé.
- **La page** teste les valeurs du brouillon, avant l'enregistrement, en 20 s au plus. La liste des modèles du
  serveur alimente la saisie du modèle. Changer le type d'un fournisseur reprend l'adresse et le modèle
  habituels de ce type.

L'hôte Web utilise `SimulatedLlmConnector` : aucune requête réseau. Une adresse en `.invalid` est injoignable,
une adresse en `.slow` ne répond jamais, la clé `refused` est refusée, seuls `qwen3:14b`, `qwen2.5:14b`,
`default`, `claude-opus-5-5` et `gpt-5` existent.

## Choix provisoires de ce chantier

L'ADR 0007 ne les tranche pas ; ils se changent en une ligne :

1. **Port par défaut 11434** (celui d'Ollama), pas 11435 : chez Benoit, l'adresse se règle une fois dans la
   page. Depuis le chantier 14, la page cherche Ollama sur 11434 puis 11435 tant que les fournisseurs sont ceux par défaut ([first-run.md](first-run.md#ollama-sur-11434-puis-11435-tranche-llmproviders)).
2. **Modèle par défaut d'un nouveau fournisseur Anthropic :** `claude-opus-5-5`. OpenAI n'a pas de modèle
   proposé : le test liste ceux du compte.
3. **Adaptateur Anthropic écrit sur `HttpClient`** plutôt que sur le SDK officiel : même code que les deux
   autres, aucune dépendance de plus. L'ADR 0007 laissait le choix.
4. **Réponse la plus longue :** 16 000 jetons par défaut, réglage `MaxOutputTokens` (obligatoire chez Anthropic).
5. **Délai du test de connexion :** 20 s ; délai jusqu'au premier octet d'une réponse : 5 min.
6. **Clé d'un fournisseur :** toujours `llm.<id>.apikey`, l'identifiant ne change jamais une fois créé.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (l'hôte Web et les serveurs simulés ne les prouvent pas) :

1. Régler l'adresse du fournisseur `local` sur `http://127.0.0.1:11435` (Ollama dans Docker), « Tester la
   connexion » : réussite, liste des modèles (`local-ia-active`, `qwen3:14b`, `qwen2.5:14b`).
2. Au démarrage de Jarvis, le journal montre « Default model qwen3:14b of provider local loaded in … ms » et
   `ollama ps` montre le modèle gardé (`Forever`). Noter la durée à froid.
3. Un fournisseur `openai-compatible` sur `http://127.0.0.1:8080` (`llama-server`) : test réussi, le nom du
   modèle servi apparaît dans la liste.
4. Les causes : Ollama arrêté (injoignable), modèle `llama9` (absent, avec la liste), clé Anthropic fausse
   (refusée). Aucune clé dans le journal ni dans `config.json`.
5. Libérer le modèle (`ollama stop qwen3:14b`) : au tour suivant (chantier 13), « le modèle se charge ».
6. **Mesure d'un modèle de repli** de 7 à 8 milliards de paramètres (ADR 0007, section 5) : temps jusqu'à la
   première phrase et mémoire graphique avec Whisper chargé, à proposer par l'assistant du premier lancement.
