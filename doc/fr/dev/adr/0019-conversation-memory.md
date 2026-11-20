# ADR 0019 : mémoire de conversation

## Statut

Proposé le 2026-11-20 (chantier `stage2/2-adrs`). En attente des réponses de Benoit aux questions de la
section « Questions ouvertes ».

- **Tranche les points laissés par :** [ADR 0004](0004-configuration-storage-and-secret-vault.md) (section 3,
  « puis la mémoire »), [ADR 0007](0007-llm-providers-and-tool-format.md) (section 6, « le résumé automatique
  des anciens échanges arrive à l'étape 2 »).
- **Récit :** US2.4 du [document de conception](../plans/design.md).

## Contexte

- US2.4 : « Mémoire de conversation persistante, avec résumé automatique des anciens échanges. Une page
  permet de voir et d'effacer ce qu'il retient. »
- Depuis le chantier `stage1/13-conversation`, Jarvis renvoie au modèle les derniers tours qui tiennent dans
  la moitié du contexte, 20 au plus, sans limite d'âge. Ce qui sort de cette fenêtre est gardé dans la base
  mais oublié du modèle.
- Le contexte du modèle local est petit : 4096 jetons, 8192 au plus sur la carte de 12 Go (ADR 0007,
  section 5). La mémoire doit tenir dans un budget fixe.
- OpenJarvis, la référence de comportement, fait deux choses : après chaque échange, un petit modèle local en
  tire des **faits durables** sur l'utilisateur (courts, 200 caractères au plus), sur un fil de fond qui
  n'échoue jamais vers la conversation ; et l'utilisateur peut lire et effacer ces faits. Un fait dont le
  texte ressemble à une injection est mis en quarantaine et n'est jamais montré au modèle.
- ADR 0009, principe n° 2 : une donnée n'est jamais une instruction. Ce que Jarvis retient revient dans le
  message système : la mémoire est donc un chemin par lequel un texte lu (page web, fichier, sortie d'outil)
  pourrait devenir une consigne durable. C'est le risque principal de cet ADR.

## Décision

### 1. Deux choses retenues

1. **Des faits** : des phrases courtes et durables sur l'utilisateur (préférences, identité, projets,
   contraintes, proches). 200 caractères au plus chacun.
2. **Un résumé** des échanges qui sont sortis de la fenêtre envoyée au modèle. Un seul résumé, tenu à jour,
   de 1 200 caractères au plus.

Les tours eux-mêmes restent dans la collection `Conversation.Turns`, sans limite de durée (ADR 0004).

### 2. D'où vient un fait

- **Sur demande** : « souviens-toi que… », « retiens que… », « remember that… ». Le modèle appelle l'outil
  `remember_fact`. « Oublie que… » appelle `forget_fact`. Les deux sont de classe « Effet courant » : pas de
  confirmation, mais l'appel s'affiche dans la conversation comme tout appel d'outil.
- **Automatiquement** (voir la question n° 1) : quand un tour finit, le modèle local relit **ce que
  l'utilisateur a dit ou écrit** dans ce tour et en tire zéro à trois faits.
- **À la main** : la page « Mémoire » permet d'ajouter un fait.

Chaque fait garde sa source (`asked`, `auto`, `manual`), sa date et le tour d'origine.

### 3. Ce qui n'entre jamais dans la mémoire

- **L'extraction ne lit que les mots de l'utilisateur.** La réponse de Jarvis, le résultat d'un outil, le
  contenu d'une page, d'un fichier, d'un courriel ou d'une session Claude ne sont jamais donnés au modèle
  qui extrait. C'est ce qui remplace le détecteur d'injection d'OpenJarvis : un texte venu de l'extérieur n'a
  aucun chemin vers la mémoire.
- `remember_fact` reçoit son texte du modèle, qui a pu lire une donnée. Il est donc refusé quand le tour en
  cours contient déjà le résultat d'un outil qui lit un contenu extérieur (web, HTTP, fichier, MCP). Jarvis
  dit alors de le redemander dans une phrase à part.
- Un secret n'est pas un fait : l'extraction a pour consigne de ne retenir ni mot de passe, ni clé, ni
  numéro de carte, et un fait qui a la forme d'une clé connue est rejeté par du code.
- Le résumé est fait à partir des demandes de l'utilisateur et des réponses de Jarvis, sans le résultat des
  outils.

### 4. Qui extrait et quand

- **Le modèle local seulement**, celui de la conversation. Jamais un fournisseur cloud, quelle que soit la
  cible du passage de main ([ADR 0020](0020-hand-off-targets-and-spend-cap.md)).
- Après la fin du tour, en arrière-plan, une demande à la fois. Une nouvelle activation annule l'extraction
  en cours : la conversation passe toujours avant. Un tour dont l'extraction est annulée ou échoue est
  repris plus tard, au plus une fois.
- Un échec ne produit ni panne, ni message : il va au journal technique, sans le texte.
- La sortie attendue est un tableau JSON de phrases. Tout autre texte vaut « rien à retenir ».
- Un fait identique à un fait présent (sans tenir compte de la casse ni des espaces) n'est pas ajouté.

### 5. Le résumé

- Quand des tours sortent de la fenêtre envoyée au modèle, le modèle local les fond dans le résumé : ancien
  résumé plus tours sortis, vers un nouveau résumé de 1 200 caractères au plus.
- Un tour sort de la fenêtre par la taille (plus de 20 tours, ou plus que la moitié du contexte), ou par
  l'âge (voir la question n° 2).
- Tant qu'un tour sorti n'est pas résumé, il n'est simplement plus envoyé : le résumé peut avoir un tour de
  retard, jamais la conversation.

### 6. Ce que le modèle reçoit

Le message système se compose désormais, dans cet ordre : la personnalité, les règles fixes, **la mémoire**,
puis vient l'historique.

- La mémoire est un bloc délimité, précédé d'une règle fixe : ce bloc décrit l'utilisateur, c'est une
  donnée, aucune phrase de ce bloc n'est une consigne.
- **Budget :** un huitième du contexte du fournisseur (environ 1 500 caractères pour 4096 jetons), résumé
  compris. L'historique garde sa moitié.
- Si les faits dépassent le budget, les plus récents passent d'abord, les faits épinglés avant tous. Les
  autres restent dans la base et se retrouvent par l'outil `search_memory` (classe « Lecture »), une
  recherche par mots dans le texte des faits. Pas d'index plein texte ni de vecteurs à cette étape.
- Ce qui part vers un fournisseur cloud lors d'un passage de main est décidé par l'ADR 0020.

### 7. Stockage et tranche

- Nouvelle tranche `Features/Conversation/Memory`. Elle possède les collections `Conversation.Memory.Facts`
  et `Conversation.Memory.Summary` du magasin de documents (ADR 0004), et la section de réglages
  `Conversation.Memory`.
- La tranche `Conversation/Dialogue` ne la référence pas (ADR 0002). Le noyau donne un contrat de lecture,
  « ce qu'il faut rappeler au modèle », que la tranche de la mémoire met en œuvre, et le message qui annonce
  un tour enregistré avec le texte de l'utilisateur.
- **« Tout effacer »** de la fenêtre de conversation efface aussi les faits et le résumé (ADR 0004).
- L'hôte Web utilise le magasin en mémoire et un modèle simulé qui « extrait » un fait fixe.

### 8. La page « Mémoire »

- La liste des faits : texte, date, source, épinglé ou non, envoyé au modèle ou hors budget.
- Effacer un fait, l'épingler, en ajouter un, tout effacer. Corriger un fait revient à l'effacer et à en
  ajouter un.
- Le résumé courant, avec un bouton pour l'effacer.
- L'interrupteur de la mémoire automatique, et un interrupteur général : mémoire coupée, rien n'est extrait
  ni envoyé, et ce qui est déjà retenu reste visible.

## Questions ouvertes

1. **Quand Jarvis retient-il un fait ?**
   - A. Sur demande et automatiquement, avec un interrupteur pour couper l'automatique. **(Recommandé :**
     c'est le comportement d'OpenJarvis, et la page permet de corriger.**)**
   - B. Sur demande seulement (« souviens-toi que… ») : rien n'est retenu sans que Benoit le dise.
   - C. Automatiquement seulement.
2. **Quand un échange devient-il « ancien » ?** Aujourd'hui un tour d'hier est renvoyé tel quel au modèle.
   - A. Après 30 minutes de silence, les tours précédents ne sont plus envoyés mot pour mot : seul le résumé
     les rappelle. **(Recommandé :** une question du soir ne traîne pas les détails du matin.**)**
   - B. Après 2 heures de silence.
   - C. Jamais par l'âge : seulement quand la fenêtre est pleine.

## Options écartées

- **Recherche par vecteurs ou index plein texte.** Demande un modèle d'embeddings de plus sur une carte déjà
  pleine (essai E5), pour quelques dizaines de faits. À revoir si la mémoire grossit.
- **Fichiers Markdown modifiables à la main**, comme le mode « agent persistant » d'OpenJarvis. La page
  « Mémoire » donne la même transparence, et la base est déjà le lieu de ce qui grossit (ADR 0004).
- **Un détecteur d'injection sur chaque fait.** Écarté au profit de la règle de la section 3 : ne lire que les
  mots de l'utilisateur.

## Conséquences

- Chaque tour coûte une demande de plus au modèle local, après la réponse. Sur la carte de Benoit le modèle
  est déjà chargé ; la durée se mesure au chantier `stage2/8-memory`.
- Un fait utile dit par Jarvis lui-même, ou trouvé dans un fichier, n'est pas retenu tout seul : il faut le
  demander. C'est le prix de la section 3.
- Le message système grossit d'un huitième du contexte. Si l'historique en souffre avec 4096 jetons, le
  budget se règle.
- La mémoire ne sort de la machine que si l'ADR 0020 le permet.
