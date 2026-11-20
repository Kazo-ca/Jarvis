# ADR 0020 : passage de main, cibles et plafond de dépense

## Statut

Proposé le 2026-11-20 (chantier `stage2/2-adrs`). En attente des réponses de Benoit aux questions de la
section « Questions ouvertes ».

- **Tranche les points laissés par :** [ADR 0007](0007-llm-providers-and-tool-format.md) (sections 1 et 7 :
  les fournisseurs cloud « ne répondent qu'à partir de l'étape 2, par le passage de main et sous le plafond
  de dépense »), [ADR 0009](0009-permission-and-confirmation-model.md) (section 9 : « le plafond de dépense
  est décrit par les ADR des étapes 2 à 4 »).
- **Récits :** US2.5 à US2.8 du [document de conception](../plans/design.md), décision n° 20.
- **Ne décide pas :** comment une session Claude Code est lancée et suivie. C'est l'ADR 0021.

## Contexte

- Décision n° 20 : le modèle local répond d'abord et appelle un outil « passer la main » ; l'utilisateur
  peut forcer le passage à la voix ; un plafond mensuel borne la dépense.
- Essai E7 : `qwen3:14b` sans réflexion choisit `hand_off_to_claude` de façon fiable en français.
- Depuis le chantier `stage1/11-llm-providers`, les fournisseurs `anthropic` et `openai` se configurent et se
  testent, mais ne répondent à rien. Chaque réponse enregistre déjà son fournisseur, son modèle, ses jetons
  envoyés et reçus et sa durée (ADR 0007, section 7).
- ADR 0009, section 9 : un texte ne quitte la machine que par un choix explicite, et l'écran où se fait ce
  choix le dit.
- L'orbe a sept aspects fixés par l'ADR 0011. Aucun ne dit « c'est Claude qui répond ».

## Décision

### 1. Deux cibles

| Cible | Sert à | Ce que c'est |
| --- | --- | --- |
| **Questions** | Une question que le modèle local ne sait pas traiter : raisonnement long, connaissance pointue, rédaction soignée | Un fournisseur cloud déjà configuré (`anthropic` ou `openai`), choisi par son nom |
| **Tâches** | Un travail sur des fichiers ou en plusieurs étapes | Claude Code, lancé par Jarvis (ADR 0021) |

- Section de réglages `Llm.HandOff`, page « Passage de main » des paramètres. Chaque cible peut rester vide.
- **Aucune cible n'est réglée par défaut.** Tant que la cible des questions est vide, rien ne part vers un
  fournisseur cloud. L'écran où on la choisit dit ce qui sortira de la machine (section 4).
- Une cible vide retire son usage de l'outil : le modèle local ne se voit pas proposer ce qu'il ne peut pas
  faire.

### 2. L'outil `hand_off_to_claude`

- Nom fixé par l'ADR 0007 et l'essai E7. Il sert aux deux cibles, même quand la cible des questions est
  ChatGPT : le nom d'un outil est stable, sa description dit à qui il passe la main.
- Paramètres : `kind` (`question` ou `task`) et `reason`, une phrase courte dite à l'utilisateur.
- Classe « Lecture » pour `question` : choisir la cible vaut accord, il n'y a pas de confirmation à chaque
  passage. La classe de `task` est celle que fixe l'ADR 0021.
- **C'est un outil terminal.** Pour `question`, la réponse de la cible est dite directement à
  l'utilisateur, phrase par phrase : elle ne repasse pas par le modèle local.
- Avant le premier son de la cible, Jarvis dit une phrase fixe, traduite, qui nomme la cible (« Je demande à
  Claude. »). Le délai avant le premier son se mesure au chantier `stage2/9-hand-off`.
- **Échec de la cible** (clé refusée, serveur injoignable, délai dépassé, plafond atteint) : le résultat de
  l'outil est la cause nommée, rendue au modèle local comme donnée. Il le dit à l'utilisateur et répond de
  son mieux. Ce n'est pas une panne de l'assistant.

### 3. Forcer le passage à la voix

- « Demande à Claude… », « ask Claude… » en début de demande envoient le reste de la phrase à la cible des
  questions, sans passer par le modèle local (US2.6). « Demande à Claude Code… », « ask Claude Code… »
  visent la cible des tâches.
- **Reconnu par du code**, pas par le modèle : une liste fermée de formules par langue, comparée au début de
  la transcription, tolérante aux variantes de Whisper sur le nom (« Claude », « Claud », « Clode »).
- Une demande écrite dans la fenêtre de conversation suit la même règle.
- Si la cible est vide ou le plafond atteint, Jarvis le dit et ne répond pas à la place.

### 4. Ce qui part vers la cible des questions

- Un message système propre au cloud : la personnalité et les règles fixes (langue, texte fait pour être
  dit).
- La demande, et la même fenêtre d'historique que le modèle local, pour que la question se comprenne.
- **Les faits retenus et le résumé de la mémoire** ([ADR 0019](0019-conversation-memory.md)) : selon la
  question n° 1.
- Les outils de classe « Lecture » activés, par la même boucle et le même point de décision que le modèle
  local (ADR 0007, section 2). `hand_off_to_claude` n'est pas donné à la cible.
- Jamais : un secret, le contenu du coffre, un son.

### 5. Qui répond

- La conversation montre déjà le fournisseur et le modèle de chaque tour (US1.14). Un tour passé à une cible
  montre en plus le motif du passage.
- Sur l'orbe : selon la question n° 2. Les sept aspects de l'ADR 0011 ne changent pas.

### 6. Plafond de dépense

- **Un plafond mensuel, en dollars américains**, la monnaie de facturation des API. Le mois est le mois
  civil, à l'heure locale.
- **Ce qui est compté :** chaque réponse d'un fournisseur cloud, à partir des jetons déjà enregistrés,
  multipliés par le prix du modèle. Les sessions Claude Code : selon la question n° 4.
- **Prix :** par fournisseur configuré, deux nombres réglables (entrée et sortie, par million de jetons).
  L'application propose les prix connus à sa date de publication ; elle ne va jamais les chercher sur le
  réseau. Un fournisseur cloud sans prix ne peut pas être choisi comme cible.
- **Avant chaque appel**, du code compare la dépense du mois au plafond. Au plafond, l'appel n'a pas lieu.
  Le dépassement possible est donc borné par une seule réponse (16 000 jetons au plus, [llm.md](../llm.md)).
- **À 80 % du plafond**, Jarvis le dit une fois dans le mois.
- **Au plafond**, Jarvis le dit et reste en local (US2.8) : l'outil n'est plus proposé pour `question`
  jusqu'au mois suivant ou jusqu'à ce que l'utilisateur relève le plafond.
- Le compteur est visible sur la page « Passage de main » et dans la fenêtre de conversation.
- Les voix cloud (ElevenLabs, OpenAI) ne sont pas comptées à cette étape : elles se facturent au caractère
  et se règlent à part. La page le dit.
- Montant par défaut : selon la question n° 3.

### 7. Tranches

- `Features/Llm/HandOff` : les cibles, leur page, l'outil, les formules de forçage.
- `Features/Llm/Spend` : les prix, le plafond, le compteur, la collection `Llm.Spend.Entries`.
- Elles ne se référencent pas (ADR 0002) : le noyau porte le message « réponse d'un fournisseur enregistrée »
  (fournisseur, modèle, jetons) et le contrat « cet appel est-il permis par le plafond ? ».

## Questions ouvertes

1. **La mémoire part-elle vers la cible cloud avec la question ?**
   - A. Non : la demande et la conversation récente seulement. Un réglage permet de l'ajouter.
     **(Recommandé :** ce que Jarvis sait de toi reste sur le PC tant que tu ne l'as pas choisi.**)**
   - B. Oui, toujours : Claude répond mieux en te connaissant.
   - C. Rien que la demande, sans la conversation récente. Les questions qui s'appuient sur l'échange en
     cours seront mal comprises.
2. **Comment l'orbe dit-il que Claude répond ?**
   - A. Un petit nom sous l'orbe (« Claude ») pendant la réflexion et la parole de ce tour.
     **(Recommandé :** lisible du coin de l'œil, sans toucher au dessin validé.**)**
   - B. Rien sur l'orbe : la fenêtre de conversation et la phrase « Je demande à Claude » suffisent.
   - C. Une couleur propre à la cible. Demande un nouvel ADR sur l'orbe et une maquette.
3. **Plafond mensuel par défaut.**
   - A. 10 $ US. **(Recommandé.)**
   - B. 25 $ US.
   - C. 5 $ US.
   - D. Aucun montant par défaut : le cloud reste bloqué tant que tu n'as pas saisi un plafond.
4. **Les sessions Claude Code comptent-elles dans le plafond ?** Claude Code annonce un coût à la fin de
   chaque session, même quand il tourne sur un abonnement où ce coût n'est pas facturé.
   - A. Un réglage « Claude Code est payé par abonnement » : coché, le coût annoncé est affiché à part et ne
     compte pas ; décoché, il compte. Coché par défaut. **(Recommandé si tu utilises Claude Code avec ton
     abonnement.)**
   - B. Toujours compté.
   - C. Jamais compté.

## Options écartées

- **Faire reformuler la réponse du cloud par le modèle local.** Double le délai et abîme la réponse.
- **Chercher les prix sur le réseau.** Une connexion sortante de plus, et un format qui change.
- **Une confirmation à chaque passage de main.** Le plafond et le choix explicite de la cible suffisent ; une
  question de plus à chaque fois rendrait le passage inutilisable à la voix.

## Conséquences

- Avec la configuration par défaut, rien ne sort : pas de cible, pas de passage de main.
- Le coût affiché est une estimation à partir des jetons et de prix saisis. La facture du fournisseur fait
  foi.
- Les prix proposés vieillissent avec l'application. Une mise à jour les rafraîchit ; l'utilisateur peut les
  corriger.
- Une question passée au cloud emporte la conversation récente : l'écran de la cible le dit en toutes
  lettres.
