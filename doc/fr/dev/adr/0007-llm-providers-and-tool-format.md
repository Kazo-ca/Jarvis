# ADR 0007 : fournisseurs LLM et format des outils

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 13, 20 et 21 de la [conception](../plans/design.md) ; récits US1.8, US1.9,
  US1.14 et US1.15 ; essais E4, E5 et E7 ([résultats](../../../../spikes/e7-local-tool-calls/RESULTS.md)) ;
  décision n° 2 des [essais](../spikes.md#décisions-prises-le-18-novembre-2026) (`qwen3:14b` sans réflexion).
- **Complète :** [ADR 0004](0004-configuration-storage-and-secret-vault.md) (clés dans le coffre),
  [ADR 0006](0006-voice-chain.md) (la réponse part à la synthèse phrase par phrase).
- **Complété par :** [ADR 0009](0009-permission-and-confirmation-model.md) (exécution des outils).

## Contexte

- L'utilisateur configure un ou plusieurs fournisseurs : Ollama, llama.cpp ou tout serveur compatible
  OpenAI, Claude, ChatGPT. L'adresse, le port et le modèle sont libres : chez Benoit, Ollama écoute sur
  11435, pas sur 11434 (US1.8).
- Essai E7 : `qwen3:14b` avec la réflexion coupée choisit le bon outil 42 fois sur 42 par le canal officiel,
  en 0,9 s. Avec la réflexion active, il lui faut 5,5 s. `qwen2.5-coder` choisit aussi le bon outil, mais
  l'écrit en JSON dans le texte 36 fois sur 42.
- Essai E4 : le premier appel à froid prend 70 à 115 s, le temps qu'Ollama charge le modèle de 9 Go.
- Essai E5 : avec un contexte de 4096 jetons, il reste environ 600 Mio de mémoire graphique.
- L'étape 1 n'a pas d'outils. Le format est fixé maintenant pour que le contrat ne change pas à l'étape 2.
- Le SDK C# officiel de MCP expose les outils d'un serveur MCP comme des `AIFunction` de
  `Microsoft.Extensions.AI` (`McpClientTool` en hérite), d'après sa documentation consultée le 2026-11-18.

## Décision

### 1. Types de fournisseurs

| Type | Protocole | Nature | Exemples |
| --- | --- | --- | --- |
| `ollama` | API native d'Ollama (`/api/chat`) | Local | Ollama dans Docker sur le port 11435 |
| `openai-compatible` | `/v1/chat/completions` | Local | llama.cpp (`llama-server`), LM Studio |
| `openai` | `/v1/chat/completions` | Cloud | ChatGPT |
| `anthropic` | API Messages | Cloud | Claude |

- Un fournisseur configuré a un nom, un type, une adresse complète, un modèle, des options, et au besoin la
  référence de sa clé dans le coffre. Plusieurs fournisseurs du même type peuvent coexister.
- **« Tester la connexion »** appelle le serveur et nomme la cause en cas d'échec : serveur injoignable,
  modèle absent, clé refusée, délai dépassé.
- **Le modèle par défaut de l'étape 1 est un fournisseur local** (US1.9, décision n° 20). Les fournisseurs
  cloud se configurent et se testent dès l'étape 1 ; ils ne répondent qu'à partir de l'étape 2, par le
  passage de main et sous le plafond de dépense.

### 2. Un seul contrat pour tous les fournisseurs

- Le contrat du noyau est celui des abstractions de **`Microsoft.Extensions.AI`** : `IChatClient`, ses
  messages, ses options et `AIFunction` pour les outils. Le noyau ne référence que le paquet d'abstractions.
- Les adaptateurs sont dans `Adapters` et mettent en œuvre `IChatClient` :
  - `ollama` : écrit sur `HttpClient`, comme dans les essais E4 et E7, pour garder la maîtrise de `think`,
    de `keep_alive`, de la taille du contexte et de l'analyseur tolérant ;
  - `openai-compatible` et `openai` : un même adaptateur, écrit sur `HttpClient` ;
  - `anthropic` : écrit sur `HttpClient`, ou sur le SDK officiel s'il met en œuvre le même contrat. Le choix
    se fait à la construction de l'adaptateur.
- Raisons : c'est le contrat standard de .NET, et les outils MCP de l'étape 2 s'y branchent sans conversion.
- **Jarvis garde la boucle d'outils.** L'invocation automatique des fonctions n'est pas utilisée : chaque
  appel demandé par le modèle passe par le modèle de permissions (ADR 0009) et s'affiche dans la
  conversation (US2.1).
- Toute réponse arrive en flux et s'annule : une interruption (US1.12) coupe la génération.

### 3. Format des outils

- Un outil a un nom, une description et un schéma JSON de ses paramètres. Chaque adaptateur le traduit dans
  le format de son fournisseur.
- **Noms :** en anglais, ASCII, minuscules et tirets bas, stables dans le temps (`get_datetime`,
  `hand_off_to_claude`), comme dans l'essai E7.
- **Descriptions :** dans la langue de l'assistant. L'essai E7 les a prouvées en français ; l'anglais se
  vérifie à l'étape 2 avec le même protocole.
- Le résultat d'un outil revient au modèle comme une **donnée**, jamais comme une instruction (ADR 0009).
- La classe de risque de chaque outil est fixée par l'ADR des outils, à l'étape 2.

### 4. Analyseur tolérant

- Les adaptateurs locaux (`ollama`, `openai-compatible`) acceptent un appel d'outil écrit dans le texte.
- Règle stricte, celle de l'essai E7 : la réponse **entière** doit être un seul objet JSON qui nomme un
  outil déclaré. Tout le reste est du texte.
- Conséquence pour le flux : si la réponse commence par `{`, elle est retenue jusqu'à sa fin avant d'être
  dite ou interprétée. Sinon elle part vers la synthèse phrase par phrase.
- L'option se désactive par fournisseur.

### 5. Modèle local par défaut et ses réglages

| Réglage | Valeur de départ | Source |
| --- | --- | --- |
| Modèle | `qwen3:14b` | E7 |
| Réflexion | **Coupée** (`think: false`) pour tout modèle qui en a une ; réglable par fournisseur | E7 |
| Contexte | 4096 jetons ; au plus 8192 conseillé sur une carte de 12 Go | E5 |
| Température | 0,3 | E4 |
| Chargement | Préchargé au démarrage de Jarvis et gardé en mémoire tant que Jarvis tourne | E4 |

- Si le modèle doit être rechargé, par exemple parce qu'Ollama l'a libéré pour un autre, l'orbe reste en
  réflexion et la conversation dit que le modèle se charge.
- L'assistant de configuration propose en repli un modèle local plus petit (7 à 8 milliards de paramètres).
  Aucun n'a été essayé : le choix du modèle de repli se fait par une mesure à l'étape 1.

### 6. Message système et historique

- Le message système se compose de la **personnalité**, modifiable et réinitialisable (US1.15,
  [ADR 0008](0008-languages-and-localization.md)), et de **règles techniques fixes** : répondre dans la
  langue choisie, écrire un texte fait pour être dit (pas de Markdown, pas de listes, phrases courtes), la
  date et l'heure du moment.
- L'historique envoyé au modèle est une fenêtre des derniers tours qui tient dans le contexte. Le résumé
  automatique des anciens échanges arrive à l'étape 2 (US2.4).

### 7. Ce qui est enregistré à chaque réponse

- Le fournisseur, le modèle, les jetons en entrée et en sortie, la durée. La conversation montre le modèle
  qui a répondu à chaque tour (US1.14).
- Ces données serviront au plafond de dépense de l'étape 2 (US2.8).

### 8. Erreurs

- Une panne nomme sa cause et l'action corrective (US1.17) : serveur injoignable (vérifier l'adresse et le
  port), modèle absent (le télécharger), clé refusée, délai dépassé.

## Conséquences

- Ajouter un fournisseur revient à écrire un adaptateur. Aucune tranche ne change.
- Le noyau dépend d'un paquet d'abstractions de Microsoft. Il reste sans logique métier ni dépendance
  native ([ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md)).
- Trois adaptateurs écrits à la main sont à tenir à jour quand les API changent. En échange, les réglages
  propres à la voix (`think`, préchargement, analyseur tolérant) restent sous contrôle.
- Garder le modèle chargé occupe environ 9,5 Go de mémoire graphique tant que Jarvis tourne. Ollama peut le
  libérer pour charger un autre modèle ; la réponse suivante de Jarvis attend alors le rechargement.
- **Non vérifié par l'essai E7 :** le tour complet (appel, résultat, réponse finale), la justesse des
  arguments, et une longue liste d'outils comme celle d'un serveur MCP Home Assistant. Ces points se
  vérifient au début de l'étape 2.
- Benoit a choisi `Microsoft.Extensions.AI` plutôt qu'un contrat maison (question posée dans la
  conversation, réponse du 2026-11-19).
