# Plafond de dépense : prix, compteur du mois, plafond

Chantier `stage2/10-spend-cap` (US2.8). Décision :
[ADR 0020](adr/0020-hand-off-targets-and-spend-cap.md), sections 6 et 7. Le passage de main, seul chemin par
lequel un fournisseur cloud répond, est décrit dans [hand-off.md](hand-off.md).

## Ce qui est construit

```
page « Dépense » ─► réglages Llm.Spend (plafond, prix par fournisseur)
                          │
Conversation/Dialogue ─► ProviderAnswered (noyau) ─► SpendLedger ─► réglages Llm.SpendCount (compte du mois)
   après chaque requête de la cible                     │      └─► collection Llm.SpendEntries (détail)
                                                        ▼
Llm/HandOff ─► ISpendCap (noyau) : Check(fournisseur) · Status · TakeNotice() · Changed
   avant chaque passage, et avant chaque requête suivante de la cible
```

Les tranches `Llm/Spend`, `Llm/HandOff` et `Conversation/Dialogue` ne se connaissent pas : un message et un
contrat du noyau les relient (ADR 0020, section 7).

## Réglages (`Llm.Spend`)

| Réglage | Défaut | Rôle |
| --- | --- | --- |
| `MonthlyCapUsd` | 10 | Le plafond du mois, en dollars américains. Zéro : aucun appel au cloud |
| `Prices` | vide | Par identifiant de fournisseur : dollars par million de jetons envoyés et écrits |

- **Un fournisseur cloud sans prix n'est jamais appelé** : son coût ne pourrait pas être compté. La page
  « Passage de main » le grise dans la liste des cibles.
- `KnownPrices` propose les prix connus à la date de publication (grille d'Anthropic du 3 novembre 2026, par
  début d'identifiant de modèle). **Rien n'est cherché sur le réseau.** Aucun prix n'est proposé pour OpenAI :
  l'utilisateur l'entre.
- Un prix est ses deux montants : rien n'est enregistré tant qu'il en manque un.

## Compter (`SpendLedger`)

- La conversation publie `ProviderAnswered` (fournisseur, modèle, jetons, heure) **après chaque requête de la
  cible**, pas seulement à la fin du tour : une cible qui appelle des outils fait plusieurs requêtes. Rien de
  ce qui a été dit n'est dans ce message. Le modèle local n'est jamais annoncé.
- Coût = jetons envoyés × prix d'entrée + jetons écrits × prix de sortie, au prix du moment.
- Le compte est celui du **mois civil à l'heure locale**. Il est gardé dans les réglages (`Llm.SpendCount` :
  mois, montant) ; le détail de chaque réponse (heure, fournisseur, modèle, jetons, coût) dans la collection
  `Llm.SpendEntries`.
- Une réponse sans nombre de jetons, ou d'un fournisseur sans prix, n'est pas comptée ; le journal le dit.
- Le premier jour du mois, le compte repart à zéro. Le mois est regardé toutes les dix minutes, pour que
  l'outil revienne sans redémarrer.

## Plafonner

- **Avant chaque passage**, `HandOffTargets.Open` demande `ISpendCap.Check` : sans prix ou au plafond, l'appel
  n'a pas lieu. La conversation redemande **avant chaque requête suivante de la cible** : le dépassement
  possible est borné par une seule réponse.
- **Au plafond ou sans prix, l'outil `hand_off_to_claude` quitte le registre** : le modèle local ne se voit
  pas offrir ce qui serait refusé. Il revient quand le plafond est relevé, le prix entré, ou le mois changé.
  Le choix de la cible est gardé.
- « Demande à Claude… » au plafond : Jarvis dit la cause et ne répond pas à la place de la cible.
- Une cible arrêtée entre deux requêtes : ce qu'elle a écrit reste, la cause est dite, ce qui a été dépensé
  est compté.

| Cause (`HandOffRefusal`) | Ce que Jarvis dit |
| --- | --- |
| `NoPrice` | « Le passage de main n'a pas eu lieu : la cible n'a pas de prix… » |
| `CapReached` | « Le passage de main n'a pas eu lieu : le plafond de dépense cloud de ce mois est atteint… » |

## Prévenir

Après la réponse de la cible, une phrase de Jarvis, écrite dans la réponse et dite quand le tour est parlé,
**une fois** :

- quand la réponse fait passer 80 % du plafond : « Au fait, plus de 80 % du plafond de dépense cloud de ce
  mois est utilisé. »
- quand la réponse atteint le plafond : « Le plafond de dépense cloud de ce mois est maintenant atteint : je
  reste en local… »

Une réponse qui saute de moins de 80 % au plafond ne dit que le plafond.

## Où le compteur se voit

- Page « Dépense » (après « Passage de main ») : dépensé sur plafond, mois, les 20 dernières réponses comptées,
  le plafond, les prix, « Remettre le compte à zéro » avec confirmation, et ce que le compte n'est pas.
- Page « Passage de main » : la ligne « Cloud ce mois-ci ».
- Fenêtre de conversation : la même ligne, dès que quelque chose a été dépensé.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `ISpendCap` (`Kernel/Llm`) | Cet appel est-il permis ? Le compte et le plafond. Ce qu'il faut dire une fois |
| `SpendVerdict`, `SpendNotice`, `SpendStatus` | Les valeurs de ce contrat |
| `ProviderAnswered` (`Kernel/Messages/Llm`) | Une réponse d'un fournisseur cloud s'est terminée : fournisseur, modèle, jetons |
| `HandOffRefusal.NoPrice`, `CapReached` ; `IHandOff.Notice` | Les deux causes de refus, et la phrase à dire après un passage |

## Écarts avec l'ADR 0020

- **La collection s'appelle `Llm.SpendEntries`**, pas `Llm.Spend.Entries` : un nom de collection a un seul
  point ([storage.md](storage.md)).
- **Le réglage « Claude Code est payé par abonnement »** (coché par défaut) et le plafond de chaque session
  (`--max-budget-usd`) sont arrivés avec les sessions Claude Code : voir
  [claude-code-tasks.md](claude-code-tasks.md#coût-llmspend).

## Choix provisoires de ce chantier

L'ADR ne les tranche pas ; à confirmer par Benoit :

1. **« Tout effacer » de la fenêtre de conversation ne remet pas le compte à zéro** : le compte est dans les
   réglages. Seul le détail des réponses comptées part avec les documents.
2. **Un bouton « Remettre le compte à zéro »** existe sur la page, avec confirmation.
3. **L'avertissement est dit après la réponse de la cible**, dans le même tour, pas à part.
4. **Le compteur n'apparaît dans la fenêtre de conversation qu'une fois quelque chose dépensé.**
5. **Un avertissement en attente est perdu si Jarvis redémarre** avant le passage suivant ; le refus au plafond,
   lui, dit toujours la cause.
6. **Le prix d'un fournisseur retiré reste dans les réglages** : un fournisseur rajouté avec le même
   identifiant le retrouve.

## Limites connues

- **Le compte est une estimation.** Les jetons mis en cache, les rabais et les taxes ne sont pas connus. La
  facture du fournisseur fait foi. La page le dit.
- Les voix cloud ne sont pas comptées (ADR 0020, section 6). La page le dit.
- Une réponse interrompue, ou qui échoue en cours de route, n'est pas comptée : le fournisseur n'a pas donné
  ses jetons. Elle peut pourtant être facturée.
- Les appels de la mémoire automatique vont au modèle local : rien à compter.

## Non vérifié

- **Aucun appel n'est parti vers l'API d'Anthropic ni d'OpenAI** : les jetons que rendent les vrais
  adaptateurs en flux n'ont pas été comparés à une facture.
- **Rien n'a tourné sur Windows.**
- Les prix proposés viennent d'une grille datée ; ils n'ont pas été relus sur le site du fournisseur.

## Hôte Web

Le fournisseur cloud simulé rend 42 jetons envoyés et 18 écrits par réponse. Les tests atteignent le plafond en
entrant un prix d'un million de dollars par million de jetons : 60 $ par réponse.

## Tests

- Unitaires : `SpendTests` (plafond par défaut, prix requis, coût, 80 %, plafond, mois suivant, redémarrage,
  remise à zéro, bornes, prix connus), `HandOffTests` (outil retiré sans prix et au plafond, phrase à dire),
  `ConversationEngineHandOffTests` (une annonce par requête, refus avant et entre deux requêtes, phrase après
  la réponse).
- Playwright : `SpendTests` (page ; fournisseur sans prix ; compte et remise à zéro ; 80 %, plafond, relèvement ;
  plafond hors bornes).

## Vérifications sur Windows

À faire avec Benoit, avec une vraie clé :

1. Entrer le prix proposé pour Claude, poser une question par « Demande à Claude » : le compte monte, et le
   montant ressemble à celui de la console du fournisseur.
2. Une question où la cible lit un fichier : deux réponses comptées.
3. Plafond à un cent : la réponse suivante dit que le plafond est atteint, puis « Demande à Claude » est refusé
   à la voix, et l'outil a quitté la page « Outils ».
4. Relever le plafond : l'outil revient sans redémarrer.
5. Redémarrer Jarvis : le compte est toujours là.
