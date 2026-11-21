# Passage de main : cibles, outil `hand_off_to_claude`, « demande à Claude »

Chantier `stage2/9-hand-off` (US2.5, US2.6, US2.7). Décision :
[ADR 0020](adr/0020-hand-off-targets-and-spend-cap.md), sections 1 à 5 et 7. Le plafond de dépense (section 6)
est le chantier `stage2/10-spend-cap`. Le tour complet est décrit dans [conversation.md](conversation.md), la
boucle d'outils dans [tools.md](tools.md), les fournisseurs dans [llm.md](llm.md).

## Ce qui est construit

```
page « Passage de main » ─► réglages Llm.HandOff (cible des questions, mémoire)
                                   │
Llm/Providers ─► ILlmProviderList (noyau) ─► HandOffTargets ─► ILlmConnector : client de la cible
                                   │              │
                                   │              └─► registre des outils : hand_off_to_claude,
                                   │                  présent seulement quand une cible est réglée
                                   ▼
Conversation/Dialogue ─► IHandOff (noyau) : Forced(demande) · Read(arguments) · Open(genre)
   │
   ├─ « Demande à Claude… » ─────────────────────────────► cible, sans le modèle local
   └─ modèle local ─► hand_off_to_claude(kind, reason) ──► cible, qui répond à l'utilisateur
```

Les tranches `Llm/HandOff`, `Llm/Providers` et `Conversation/Dialogue` ne se connaissent pas : deux contrats
du noyau les relient.

## Cibles (`HandOffTargets`, section `Llm.HandOff`)

| Réglage | Défaut | Rôle |
| --- | --- | --- |
| `QuestionsProviderId` | vide | Le fournisseur cloud (`anthropic` ou `openai`) qui répond aux questions passées |
| `WithMemory` | non | Envoyer aussi les faits retenus et le résumé ([conversation-memory.md](conversation-memory.md)) |

- **Aucune cible au départ : rien ne part vers un fournisseur cloud**, et le modèle local ne reçoit pas
  l'outil.
- Seul un fournisseur cloud peut être une cible. Un identifiant qui désigne un fournisseur local ou retiré
  vaut « aucune cible ».
- Le client de la cible suit les réglages : changer de cible, ou changer le modèle du fournisseur choisi,
  ferme l'ancien client et en ouvre un nouveau.
- Le nom dit à l'utilisateur vient du genre du fournisseur : « Claude » pour `anthropic`, « ChatGPT » pour
  `openai`.
- **La cible des tâches (Claude Code) n'existe pas encore** : elle arrive avec les sessions Claude Code
  (ADR 0021, chantier `stage2/12`). Un passage de genre `task` est refusé avec sa cause.

## L'outil `hand_off_to_claude` (`HandOffTool`)

- Nom fixe quelle que soit la cible ; sa description nomme la cible. Origine `hand-off`, classe « Lecture » :
  choisir la cible vaut accord, aucune confirmation à chaque passage. Activé dès qu'une cible est réglée ;
  l'interrupteur de la page « Outils » le coupe.
- Paramètres : `kind` (`question` seulement pour l'instant) et `reason`, 200 caractères au plus.
- **Outil terminal.** La conversation le reconnaît par son nom : après l'accord du point de décision, elle
  fait répondre la cible, et rien ne retourne au modèle local.

## Forcer le passage (`HandOffPhrases`)

Au début de la demande, dite ou écrite, reconnu par du code :

| Formule | Vise |
| --- | --- |
| « Demande à Claude … », « Demandez à Claude … », "Ask Claude …" | la cible des questions |
| « Demande à Claude Code … », "Ask Claude Code …" | la cible des tâches |

- Tolère « Claud », « Clode », « demande a », un « Jarvis, » devant, la casse et la ponctuation.
- Les formules des deux langues sont essayées quelle que soit la langue détectée.
- Ce qui suit la formule est la demande envoyée à la cible. Une formule sans suite n'est pas un passage forcé.
- Le modèle local n'est ni chargé ni interrogé. Si la cible manque ou échoue, Jarvis dit la cause et **ne
  répond pas à sa place**.

## Ce que fait la conversation (`ConversationEngine.HandOff.cs`)

1. `IHandOff.Open(genre)` rend la cible, ou la cause de son absence.
2. Pour une demande dite : ce que le modèle local avait écrit, puis la phrase fixe « Je demande à Claude. »
3. La cible répond **dans la même boucle** que le modèle local (`RunAsync`) : son texte est montré et dit
   phrase par phrase, ses appels d'outils passent par le même point de décision.
4. Le tour enregistre le fournisseur, le modèle et les jetons **de la cible** ; l'entrée `HandOff` du tour
   garde le genre, le motif, s'il était forcé, le nom de la cible, le fournisseur, le modèle et les jetons du
   modèle local, le délai avant le premier texte de la cible et, s'il y a lieu, la cause de l'échec.

### Ce qui part vers la cible

| Part | Ne part pas |
| --- | --- |
| La personnalité et les règles fixes | Les faits retenus et le résumé, sauf si `WithMemory` est coché |
| La même fenêtre d'historique que le modèle local | L'appel d'outil du modèle local et son motif |
| La demande (pour un passage forcé : ce qui suit la formule) | Un secret, le contenu du coffre, un son |
| Les outils activés de classe déclarée « Lecture », sans `hand_off_to_claude` | Tout autre outil |

Une cible qui appelle un outil qu'elle n'a pas reçu est refusée (`ToolDisabled`). Le journal des permissions
nomme la cible comme demandeur.

### Quand la cible échoue

| Cas | Ce qui se passe |
| --- | --- |
| Par l'outil | Le résultat de l'outil est la cause nommée (« Claude a refusé la clé. ») ; le modèle local le dit et répond de son mieux |
| Passage forcé | Jarvis dit la cause ; personne ne répond à la place de la cible |

Causes : aucune cible, cible des tâches absente, et les huit causes d'un fournisseur ([llm.md](llm.md)). Une
cible qui n'écrit rien vaut « erreur du serveur ». **Jamais une panne de l'assistant** : aucun `TurnFailed`.

## Qui répond

La fenêtre de conversation montre le fournisseur et le modèle du tour, et une ligne « Passage de main » :
« Passé à Claude : *motif* », « Demandé directement à Claude, à votre demande », ou « Le passage de main n'a
pas eu lieu (*cause*) ». **Rien ne change sur l'orbe** (ADR 0020, réponse n° 2).

## Page « Passage de main »

Après « Modèles de langage ». Elle dit d'abord ce qui quitte l'ordinateur, puis offre : la cible des questions
(liste des fournisseurs cloud configurés), la case de la mémoire, la cible des tâches (pas encore disponible),
les formules de forçage. Un changement est enregistré aussitôt.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `IHandOff` (`Kernel/Conversation`) | Une demande force-t-elle le passage ? Que dit un appel de l'outil ? Qui répond ? |
| `HandOffKind`, `HandOffRefusal`, `ForcedHandOff`, `HandOffCall`, `HandOffTarget`, `HandOffOpening` | Les valeurs de ce contrat |
| `ILlmProviderList` (`Kernel/Llm`) | Les fournisseurs configurés, pour une tranche qui en laisse choisir un |

## Hôte Web

Le modèle local simulé appelle `hand_off_to_claude` sur « question difficile » quand l'outil est offert. Un
fournisseur cloud simulé répond « Ici *modèle*, dans le cloud. Demande reçue : … Historique : N message(s).
Mémoire : oui|non. » : les tests voient ce que la cible a reçu. Une clé `refused` est refusée.

## Choix provisoires de ce chantier

L'ADR ne les tranche pas ; à confirmer par Benoit :

1. **Le motif (`reason`) est montré dans la fenêtre, pas dit** : seule la phrase fixe est dite avant la cible.
2. **« Je demande à Claude » n'est pas écrit dans la réponse** du tour : dit seulement, la ligne « Passage de
   main » le montre.
3. **Le nom dit vient du genre du fournisseur** (« Claude », « ChatGPT »), pas du nom tapé dans les réglages.
4. **Les formules ne visent que « Claude »**, même quand la cible est ChatGPT : pas de « demande à ChatGPT ».
5. **Une formule sans suite** (« Demande à Claude ») va au modèle local.
6. **« de », « si », "to" après la formule restent dans la demande** envoyée (« de résumer Kant »).
7. **La cible a ses propres huit appels d'outils** au plus, en plus de ceux du modèle local.
8. **Après un passage par l'outil qui échoue, le modèle local peut le redemander** dans le même tour, dans la
   limite des huit appels.
9. **`kind` ne propose que `question`** tant que la cible des tâches n'existe pas ; « Demande à Claude Code »
   est reconnu et refusé avec sa cause.

## Limites connues

- **Aucun plafond de dépense ne borne encore les appels** : c'est le chantier suivant. La page le dit. Un
  fournisseur sans prix peut être choisi comme cible tant que les prix n'existent pas.
- L'historique envoyé à la cible est taillé sur le contexte du modèle local, pas sur celui de la cible.

## Non vérifié

- **Aucun appel n'est parti vers l'API d'Anthropic ni d'OpenAI depuis Jarvis.** Les adaptateurs cloud datent
  du chantier `stage1/11-llm-providers` et n'ont jamais répondu à un tour.
- **Rien n'a tourné sur Windows.** Le délai avant le premier son de la cible n'est pas mesuré.
- `qwen3:14b` réel n'a pas choisi l'outil dans Jarvis (l'essai E7 l'a montré à part).
- Les formules n'ont pas été essayées avec Whisper.

## Tests

- Unitaires : `HandOffTests` (cibles, client, registre, formules, outil) et
  `ConversationEngineHandOffTests` (passage par l'outil, passage forcé, mémoire, outils de la cible, échecs,
  interruption).
- Playwright : `HandOffTests` (rien sans cible et page ; cible choisie, outil et « Ask Claude » ; case de la
  mémoire ; clé refusée).

## Vérifications sur Windows

À faire avec Benoit, avec une vraie clé et un petit montant :

1. Choisir la cible : l'outil apparaît dans la page « Outils ».
2. « Demande à Claude … » à la voix : noter le délai entre la fin de la phrase, « Je demande à Claude » et le
   premier son de la réponse (journal : `first text of the target`).
3. Une question difficile sans formule, dans les deux langues : `qwen3:14b` choisit-il l'outil, et seulement
   quand il le faut ?
4. Whisper écrit-il « Claude », « Claud » ou « Clode » ? Ajouter les variantes entendues.
5. Clé fausse, puis réseau coupé : la cause est dite, l'orbe ne passe pas en panne.
6. Interrompre la réponse de la cible par le mot d'éveil.
7. Avec un dossier autorisé : la cible lit un fichier, et la fenêtre montre l'appel.
