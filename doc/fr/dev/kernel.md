# Le noyau : bus, machine à états, niveau sonore

2026-11-19 · Chantier `stage1/2-kernel`. Construit d'après l'[ADR 0002](adr/0002-vertical-slices-kernel-and-dependency-rules.md)
(section 5, messages entre tranches), l'[ADR 0005](adr/0005-assistant-state-machine-and-orb-contract.md)
(machine à états) et l'[ADR 0011](adr/0011-orb-colors-and-drawing.md) (section 4, fonction d'aspect).

Ce document dit comment une tranche se sert du noyau. Les règles elles-mêmes sont dans les ADR.

## Contenu

```
src/Kazo.Jarvis.Kernel/
  KernelServiceCollectionExtensions.cs   AddJarvisKernel(), appelé par AddJarvisFeatures()
  Messaging/                             IEvent, IRequest<T>, IMessageBus, gestionnaires, bus en mémoire
  Assistant/                             état, phase, conditions, tour, fonction d'aspect, machine, rédacteur unique
  Messages/Assistant/                    messages d'entrée de la machine et messages qu'elle publie
  Audio/                                 niveau sonore et flux du micro (hors du bus), contrats de capture et de lecture (voir audio.md)
  Messages/Audio/                        MicrophoneMuteRequested
  Speech/                                contrats des moteurs de voix, choix de la voix, clés des voix (voir speech.md)
  Messages/Speech/                       SentenceToSpeak, AnswerSentencesEnded
  Settings/                              contrats des réglages (voir storage.md), pages de la fenêtre des paramètres
  Localization/                          langue de l'assistant, lecteur de la langue, langue de Windows (voir shared-ui.md)
  Messages/Localization/                 AssistantLanguageChanged
  Storage/                               contrat de la base de documents (voir storage.md)
  Secrets/                               contrat du coffre de secrets (voir storage.md)
  Tools/                                 outil, registre des outils, interrupteurs (voir tools.md)
  Conversation/                          ce que la mémoire rappelle au modèle (voir conversation-memory.md), passage de main (voir hand-off.md)
  Messages/Conversation/                 TurnRecorded, ExchangesLeftHistory
  Tools/Servers/                         serveur d'outils : connexion, outils, appel (voir mcp.md), classement par type de serveur (voir home-assistant.md)
  Files/                                 fichiers locaux en lecture (voir basic-tools.md)
  Tasks/                                 forme d'une tâche, clés de projet et de sujet, tableau des tâches (voir tasks-and-sessions.md)
  ClaudeCode/                            registre des sessions Claude Code en cours (voir tasks-and-sessions.md)
  Web/                                   navigateur de lecture, recherche par API, adresses permises (voir basic-tools.md)
  Permissions/                           classes d'actions, point de décision des permissions (voir tools.md)
```

Un hôte n'appelle pas `AddJarvisKernel()` lui-même : `AddJarvisFeatures()` le fait. L'appel peut se répéter
sans effet.

## Le bus

- **Événement** (`IEvent`) : un fait passé, zéro, un ou plusieurs abonnés. **Requête** (`IRequest<TResult>`) :
  exactement un gestionnaire, sinon `SendAsync` lève `InvalidOperationException`.
- Deux façons de s'abonner :
  - un `IEventHandler<TEvent>` inscrit dans le conteneur, en singleton, par la méthode d'inscription de la
    tranche (le cas normal) ;
  - `IMessageBus.Subscribe<TEvent>(…)`, pour un abonnement qui vit avec un composant Razor ; on le libère
    dans `Dispose`.
- Un abonné reçoit aussi les événements des types dérivés : s'abonner à `AssistantInput` reçoit toutes les
  entrées de la machine, s'abonner à `IEvent` reçoit tout.
- **Ordre :** les gestionnaires du conteneur, puis les abonnements, dans l'ordre où ils ont été faits, l'un
  après l'autre. `PublishAsync` se termine quand tous ont fini.
- **Un abonné qui échoue** est journalisé (niveau `Error`) et n'empêche pas les autres de recevoir
  l'événement. L'exception d'un gestionnaire de requête, elle, revient à l'appelant.
- Le bus marche aussi dans un hôte sans journalisation (l'hôte Windows n'en a pas encore) : il prend alors un
  journal muet.
- Les messages d'une tranche se rangent par domaine : `Kernel/Messages/<Domaine>/`.

## La machine à états

- `AssistantState` : la phase (`Idle`, `Listening`, `Thinking`, `Speaking`), le tour en cours, le dernier
  tour, et les conditions (micro coupé, faute, nombre de tâches déléguées). Immuable.
- `AssistantStateMachine.Apply(état, entrée)` : la table de transitions de l'ADR 0005, fonction pure. Elle
  rend le nouvel état, les effets à publier, ou la raison pour laquelle l'entrée est ignorée.
- `AssistantStateKeeper` : le **rédacteur unique**. Il reçoit chaque `AssistantInput` du bus, les applique un
  par un dans l'ordre d'arrivée, puis publie `AssistantStateChanged` et les effets (`TurnStarted`,
  `TurnCancelled`, `ActivationRefused`). Une entrée ignorée est journalisée (niveau `Debug`) et ne publie
  rien.
- `IAssistantStateReader.Current` : l'état actuel, pour un composant qui démarre après le dernier
  changement.
- **Personne ne fixe l'état.** Une tranche publie une entrée et attend `AssistantStateChanged`.

| Entrée publiée par une tranche | Quand |
| --- | --- |
| `ActivationRequested(source)` | Mot d'éveil, raccourci ou clic |
| `TextSubmitted(texte)` | Texte envoyé depuis la fenêtre de conversation |
| `UtteranceEnded(tour)` / `NothingHeard(tour)` | Fin de la phrase / rien entendu avant le délai |
| `FirstSentenceReady(tour)` / `AnswerFinishedWithoutSpeech(tour)` / `SpeechEnded(tour)` | Étapes de la réponse |
| `TurnFailed(tour, faute)` | Le tour ne peut pas finir |
| `MicrophoneMuteChanged(coupé)` | Micro coupé ou rétabli |
| `FaultRaised(faute)` / `FaultResolved(cause)` | Faute trouvée hors d'un tour / cause disparue |
| `DelegatedTaskCountChanged(nombre)` | Étape 2 |

- **Tours.** Chaque tour a un numéro (`TurnId`) qui suit le précédent. Les messages d'un tour le portent ; un
  message d'un tour annulé ou fini est ignoré.
- **Pas de réentrée.** Une entrée publiée par un abonné pendant qu'une autre est traitée (une tranche qui
  réagit à `TurnStarted`, par exemple) attend son tour : elle est traitée après les effets de la première.
  `PublishAsync` d'une entrée peut donc rendre la main avant que l'entrée soit appliquée.
- **Fautes.** Le noyau ne nomme que la cause (`FaultCause`) ; le texte, l'action corrective et le lien vers
  le bon réglage viennent des tranches (chantier `stage1/15-faults`). `MicrophoneMissing` refuse les
  activations, comme le micro coupé.
- **Aspect.** `AssistantLooks.Of(état)` donne l'un des huit aspects (ADR 0011, section 4 ; « interaction requise » vient de l'ADR 0023 et passe avant tous les autres) ;
  `AssistantLooks.ShowsProgressRing(état)` dit si l'anneau de progression est dessiné par-dessus.

### Cas que l'ADR 0005 ne couvre pas

Une interruption (mot d'éveil, raccourci ou clic pendant que Jarvis réfléchit ou parle) alors que le micro
est coupé ou absent : le tour est quand même annulé et la voix s'arrête (on l'a demandé), mais l'écoute ne
démarre pas. La machine publie `TurnCancelled` puis `ActivationRefused`, et revient au repos.

## Le niveau sonore

Hors du bus (ADR 0002, section 5), dans `Kernel/Audio/` :

- `ISoundLevelSink.Report(origine, valeur)` : appelé par les tranches audio (micro au chantier 8, voix au
  chantier 12). La valeur est ramenée entre 0 et 1.
- `ISoundLevelSource.Subscribe(…)` : lu par l'orbe. Au plus 30 valeurs par seconde **pour chaque origine** ;
  les valeurs en trop sont jetées, sauf le silence (0), qui passe toujours pour que l'orbe ne reste pas
  allumée.
- L'orbe choisit l'origine selon l'aspect : le micro pendant l'écoute, la voix pendant la parole.
