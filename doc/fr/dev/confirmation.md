# Demande de confirmation

Chantier `stage2/4-confirmation` (socle de US2.10). Décisions :
[ADR 0009](adr/0009-permission-and-confirmation-model.md) (principe 2),
[ADR 0017](adr/0017-tools-classes-and-decision-point.md) (section 2) et
[ADR 0023](adr/0023-confirmation-request-in-a-turn.md) (**proposé**, en attente de la réponse de Benoit : ce
chantier est construit sur ses recommandations).

## Ce qui est construit

```
ConversationEngine ─► IPermissionDecision (Permissions/Decision)
                          │ classe « Sensible » ou « Critique »
                          ▼
                      IConfirmation (noyau) ◄── ConfirmationDesk (Permissions/Confirmation)
                                                   │ ConfirmationOpened / ConfirmationClosed ─► état (condition, aspect de l'orbe)
                                                   │ fenêtre de confirmation (Approuver, Refuser, temps restant)
                                                   │ tour parlé : ConfirmationQuestionToSay ─► Voice/Speech dit la question
                                                   │                ConfirmationQuestionSaid ─► Voice/Transcription, une écoute
                                                   ◄─ ConfirmationAnswerHeard (texte entendu) ── liste fermée de mots
```

Seule une réponse humaine approuve. Le modèle ne voit jamais la question ni la réponse : il reçoit le
résultat de l'outil, ou une phrase qui dit pourquoi l'appel est refusé.

## Contrats du noyau

| Type | Rôle |
| --- | --- |
| `IConfirmation` | « Demande à l'utilisateur d'approuver cette action » : rend approuvé, refusé, délai dépassé ou interrompu, avec le canal |
| `ConfirmationRequest`, `ConfirmationOutcome`, `ConfirmationAnswer`, `ConfirmationChannels` | La demande et sa réponse ; canaux `voice` et `click` |
| `ActionContext` | Le tour où l'action est demandée, et s'il était parlé ; porté par `ActionRequest.Context` |
| `PendingConfirmation` | La condition « confirmation en attente » de `AssistantState.Confirmation` : identifiant et classe |
| `ConfirmationOpened`, `ConfirmationClosed` | Les deux entrées de la machine d'états : posent et lèvent la condition, sans toucher à la phase ni au tour |
| `ConfirmationQuestionToSay`, `ConfirmationQuestionSaid`, `ConfirmationAnswerHeard` | Les messages entre tranches : question à dire, question dite, réponse entendue |
| `AssistantLook.Confirmation` | Le huitième aspect ; il passe avant l'aspect de la phase et avant toute autre condition |
| `ITool.Summarize` | La phrase qui dit ce que ferait l'appel, écrite par l'outil ; sans elle, une phrase générale qui nomme l'outil |

## Tranche `Permissions/Confirmation`

`ConfirmationDesk` tient les demandes, **une à la fois** ; les suivantes attendent.

1. Il pose la condition, ouvre la fenêtre de confirmation et, pour un tour **parlé**, publie la question à
   dire : le résumé, puis « Confirmez-vous ? ».
2. Il attend un clic, une réponse parlée reconnue, le délai ou une interruption.
3. Il ferme la fenêtre et lève la condition, quelle que soit l'issue.

| Cas | Ce qui se passe |
| --- | --- |
| Tour écrit | La fenêtre seule. Jarvis ne parle pas. |
| Tour parlé, « Sensible », micro non coupé | Question dite, une écoute, réponse à la voix ou au clic. |
| Tour parlé, « Sensible », micro coupé | Question dite (« Approuvez ou refusez dans la fenêtre »), clic seul. |
| Tour parlé, « Critique » | La phrase dit que la voix ne suffit pas ; clic seul. |
| Fenêtre fermée par l'utilisateur | Refus. |
| Délai dépassé | Refus, cause `TimedOut`. |
| Mot d'éveil, raccourci ou clic sur l'orbe | Le tour est annulé (ADR 0005), la demande se ferme, cause `Interrupted`. |

- **Délai** : 30 s par défaut, réglable de 10 à 120 (section `Permissions.Confirmation`, page
  « Confirmations » des paramètres). Il part à l'ouverture de la demande.
- **Mots reconnus** (`ConfirmationWords`) : la transcription entière, sans ponctuation ni majuscules, doit
  être une entrée de la liste de la langue de l'assistant. Français : « oui », « oui Jarvis »,
  « d'accord », « confirme », « je confirme », « approuve », « vas-y ». Anglais : « yes », « yes Jarvis »,
  « confirm », « I confirm », « approve », « go ahead ». **Toute autre réponse est un refus**, y compris
  « oui, enfin non ». Rien d'entendu : la demande reste ouverte jusqu'au délai.
- Le texte entendu est comparé à la liste puis oublié : ni journal, ni modèle.

## Ce que font les autres tranches

- **`Permissions/Decision`** appelle `IConfirmation` à l'étape 4. Il revérifie le canal : « Critique » ne
  s'approuve que par un clic, quoi que réponde la tranche de confirmation. Un accord sans canal n'est pas
  un accord. Le journal porte le canal (`voice`, `click`, ou rien). Sans `IConfirmation` dans l'hôte :
  refusé (`ConfirmationUnavailable`). La page **« Journal des permissions »** des paramètres montre les
  200 décisions les plus récentes des 90 derniers jours.
- **`Voice/Speech`** dit la question dans la file du tour, donc avec la voix du tour et après ce que le
  modèle a déjà dit. Ce n'est pas une phrase de la réponse : pas de `FirstSentenceReady`, la phase ne
  change pas. Une demande fermée pendant la question coupe la question, pas le tour.
- **`Voice/Transcription`** ouvre **une seule écoute** après la question (même fin de parole, même
  Whisper). Elle ne publie aucun message de tour ; ce qu'elle entend part seulement en
  `ConfirmationAnswerHeard`.
- **`Assistant/Orb`** : aspect « Confirmation », ambre, rotation lente et pulsation (0,6 en haute qualité,
  0,7 en version légère).
- **`Conversation/Dialogue`** donne le tour et son mode (parlé ou écrit) au point de décision. Le délai de
  30 s d'un appel ne compte pas l'attente de la confirmation.

## Hôte Web

Deux outils factices, pour montrer la demande avant qu'un vrai outil en ait besoin : `write_note`
(« Sensible », « écris une note … ») et `erase_notes` (« Critique », « efface les notes »). Ils gardent leurs
notes en mémoire. Le champ « Phrase to say » et le bouton « Say this phrase » font « entendre » un texte
choisi : une demande, ou la réponse à la question.

## Choix provisoires de ce chantier

L'ADR ne les tranche pas ; à confirmer par Benoit :

1. **Fermer la fenêtre de confirmation vaut un refus**, sans attendre le délai.
2. **Le délai part à l'ouverture de la demande**, pas à la fin de la question dite.
3. **La liste de mots suit la langue de l'assistant** : « yes » n'approuve pas quand Jarvis parle français.
4. **La place de la fenêtre** est celle des autres fenêtres de l'hôte ; « près de l'orbe » reste à faire
   dans l'hôte Windows.
5. **Le journal n'a ni filtre ni recherche** : les 200 décisions les plus récentes.

## Non vérifié

- Sur Windows : la fenêtre de confirmation, la question dite par Piper, l'écoute de la réponse par Whisper.
- La reconnaissance de « oui » par Whisper dans du bruit, et l'écho de la question dans le micro.
- Aucun vrai outil « Sensible » ou « Critique » n'existe encore : les premiers arrivent avec les chantiers
  suivants (fichiers, domotique).

## Tests

- Unitaires : `AssistantStateMachineTests` et `AssistantLooksTests` (condition, aspect),
  `ConfirmationWordsTests` (liste fermée), `ConfirmationDeskTests` (fenêtre, question, canaux, délai,
  interruption, une demande à la fois), `PermissionDecisionPointTests` (canal revérifié, journal),
  `SpeechPlayerTests` (question dite sans phase « Parole », question coupée), `UtteranceListenerTests`
  (une seule écoute, aucun message de tour).
- Playwright (`ConfirmationTests`) : demande écrite approuvée par clic, action critique refusée, « oui »
  parlé, réponse parlée hors liste, action critique en tour parlé, délai dépassé, interruption ; le journal
  est vérifié à chaque fois.

## Vérifications sur Windows

À faire avec Benoit, quand un premier outil « Sensible » existe :

1. Demande parlée : Jarvis dit le résumé et la question, l'orbe passe à l'ambre, « oui » approuve.
2. « Non », puis une phrase hors liste : refus dans les deux cas.
3. Silence : la fenêtre se ferme après le délai, l'action n'a pas lieu.
4. La fenêtre de confirmation ne prend pas le focus d'un jeu ou d'une application en plein écran.
