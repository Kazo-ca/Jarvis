# Annonces des sessions Claude Code : fin, attente, heures de silence, « où en est Claude ? »

Chantier `stage2/13-session-announcements` (US2.12, US2.13). Décisions :
[ADR 0021](adr/0021-claude-code-sessions.md), section 9 (ce qui est annoncé, réglage par dossier, heures de
silence, « je vais dormir »), et [ADR 0024](adr/0024-announcement-turn.md) (le tour d'annonce). **L'ADR 0024
est encore « Proposé »** : ce chantier est construit sur sa recommandation et attend la réponse de Benoit.

## Ce que voit et entend l'utilisateur

- Quand une session Claude Code **attend une permission**, **pose une question**, **se termine** ou **se
  bloque**, Jarvis le dit : « Claude Code attend votre permission sur kazo-bar. », « Claude Code a terminé
  sur kazo-bar. Le menu est corrigé. ».
- L'annonce s'écrit aussi dans la conversation, comme une ligne de Jarvis sans demande au-dessus.
- Elle n'interrompt jamais : elle attend que Jarvis soit au repos. Le mot d'éveil, le raccourci ou un clic la
  coupent comme une réponse.
- La page « Annonces » (réglages, ordre 55) : ce qui se passe en ce moment (annonces dites, heures de
  silence, période de silence et son heure de fin), un bouton pour se taire 8 heures ou pour mettre fin au
  silence, les heures de silence de chaque jour, et un choix par dossier : tout, seulement les attentes, rien.
- « Je vais dormir » : Jarvis propose 8 heures de silence et ne commence qu'après le oui.
- « Où en est Claude sur Kazo Bar ? » : Jarvis répond d'après l'état de la session et son dernier message.

Une session lancée par Jarvis et une session lancée à la main (vue par ses hooks,
[hook-server.md](hook-server.md)) sont annoncées de la même façon.

## Le tour d'annonce (noyau et `Conversation/Dialogue`)

```
tranche qui annonce ── AnnouncementToSay(texte, langue, à voix haute) ──► AnnouncementDesk (conversation)
                                                                             │ écrit dans le journal, tout de suite
                                                                             │ à voix haute : file d'attente
                                             au repos : AnnouncementSubmitted ▼
                              machine d'états ── TurnStarted (annonce, texte) ──► AnnouncementDesk
                                                                             │ SentenceToSpeak, AnswerSentencesEnded
                                                                             ▼
                                                              Voice/Speech dit les phrases du tour
```

| Élément | Rôle |
| --- | --- |
| `AnnouncementToSay` (`Kernel/Messages/Assistant`) | Ce qu'une tranche veut annoncer ; elle seule décide si c'est dit |
| `AnnouncementSubmitted` | Événement d'entrée de la machine d'états : nouveau tour au repos, ignoré sinon |
| `TurnInput.Announcement` | Troisième entrée d'un tour : ni micro ni modèle |
| `AssistantState.Announcing` | Le tour en cours est une annonce : sa fin ne lève pas l'erreur |
| `AnnouncementDesk` (`Conversation/Dialogue`) | Écrit l'annonce, attend le repos, la soumet, donne ses phrases à la voix |

- Une annonce s'écrit **dès qu'elle arrive**, dite ou non.
- Les annonces se disent une par une. **Cinq au plus attendent** ; une annonce qui attend depuis plus de
  **dix minutes** n'est plus dite. Dans les deux cas elle reste écrite.
- Une annonce coupée par une activation n'est pas redite.
- **Une annonce n'est jamais envoyée à un modèle** : ni dans l'historique des tours suivants, ni vers la
  mémoire de conversation, ni dans le silence qui rend un échange « ancien ». Son texte peut venir d'une
  session.
- Pendant une annonce, la fenêtre de conversation n'accepte pas de texte, comme pendant une réponse.

## Tranche `ClaudeCode/Announcements`

| Fichier | Rôle |
| --- | --- |
| `SessionAnnouncer` | Surveille le tableau des tâches, décide ce qui est une nouvelle, publie `AnnouncementToSay` |
| `ClaudeAnnouncementsOptions` | Section `ClaudeCode.Announcements` de `config.json` |
| `QuietTime` | Quand rien n'est dit : heures de silence et période de silence |
| `QuietPeriodTool` | L'outil `start_quiet_period` |
| `SessionStatusTool` | L'outil `get_claude_sessions` |
| `ClaudeAnnouncementsPage` | La page « Annonces » |

### Ce qui est une nouvelle

L'annonceur compare le statut de chaque tâche Claude Code (hors sous-tâches) à celui d'avant. Il n'écoute
aucun hook lui-même : le tableau des tâches est sa seule source, quelle que soit l'origine de la tâche.

| Changement de statut | Annonce |
| --- | --- |
| Vers `waiting_approval` | « attend votre permission » |
| Vers `waiting_answer` | « vous pose une question » |
| De `working` ou d'une attente vers `idle` ou `done` | « a terminé », avec le dernier message |
| De `working`, d'une attente, de `idle` ou de `paused` vers `blocked` | « est bloqué », avec la cause |
| Vers `abandoned` | Rien : c'est l'utilisateur qui l'a arrêtée |
| De `pending` vers `blocked` | Rien : le tour qui l'a lancée l'a déjà dit |
| De `idle` vers `done` | Rien : la fin de son tour a déjà été annoncée |
| Tâche déjà sur le tableau au démarrage de Jarvis | Rien |

- Le nom dit est le dernier segment du dossier de la session ; à défaut, le nom du projet.
- La langue est celle de l'assistant au moment de l'annonce.

### Le dernier message d'une session

Le dernier message est **une donnée** : il n'est jamais exécuté.

- 160 caractères ou moins : dit tel quel.
- Plus long, annonce dite, **modèle local déjà en mémoire** : raccourci par le modèle local, sans outil, le
  message étant un seul contenu avec la consigne de ne suivre aucune instruction qu'il contient. 20 secondes
  au plus, 300 caractères au plus.
- Sinon (modèle absent, pas chargé, en échec, trop lent) : coupé à 300 caractères, à la fin d'une phrase quand
  il y en a une.
- Annonce muette : coupé à 500 caractères, sans modèle.
- La cause d'un blocage n'est jamais reformulée, seulement coupée.

Une annonce ne fait pas charger le modèle local.

### Réglage par dossier

`Tout` (défaut), `Seulement quand elle m'attend`, `Rien`. « Rien » n'écrit rien non plus. La page liste les
dossiers autorisés, ceux des sessions vues et ceux déjà réglés.

### Silence

- **Heures de silence** : des plages `HH:mm` à `HH:mm`, heure locale, chaque jour ; une plage peut passer
  minuit. **Aucune par défaut.** Six au plus.
- **Période de silence** : commence maintenant, dure de 1 à 24 heures (8 par défaut), **ne change aucun
  réglage et se termine seule**. Elle est gardée en mémoire seulement.
- Pendant le silence, l'annonce est publiée « muette » : écrite dans la conversation, pas dite, et pas redite
  après.

### Outils du modèle local

| Outil | Classe | Rôle |
| --- | --- | --- |
| `start_quiet_period` (`hours`, 8 par défaut) | Effet courant | Commence une période de silence |
| `get_claude_sessions` (`folder`, facultatif) | Lecture | L'état des sessions et leur dernier message |

- La description de `start_quiet_period` dit au modèle de proposer 8 heures de silence quand l'utilisateur dit
  qu'il va dormir, et de n'appeler l'outil qu'après son oui. **Quand des heures de silence sont réglées, la
  description change** : le modèle ne le propose plus de lui-même.
- `start_quiet_period` est **refusé quand le tour contient déjà un contenu venu de l'extérieur** (une page,
  un fichier, le dernier message d'une session) : un texte que le modèle vient de lire ne peut pas faire taire
  Jarvis (ADR 0019, section 3).
- `get_claude_sessions` rend au plus cinq sessions, la plus récente d'abord : dossier, titre, statut, lancée
  par Jarvis ou par l'utilisateur, minutes depuis le dernier changement, dernier message (600 caractères au
  plus). Le nom donné est comparé sans égard à la casse, aux accents ni aux séparateurs. Le résultat est
  marqué comme un contenu venu de l'extérieur de la conversation (ADR 0019, section 3).

## Réglages (`ClaudeCode.Announcements`)

| Réglage | Défaut |
| --- | --- |
| `Folders` (dossier, niveau) | aucun : tout est annoncé |
| `QuietHours` (de, à) | aucune |

## Hôte Web

- `SimulatedHandSession` : trois boutons de la page de test font vivre une session lancée à la main (elle
  travaille, attend une permission, termine son tour), en déplaçant le tableau comme le font les routes des
  hooks.
- Le modèle simulé appelle `get_claude_sessions` sur « où en est Claude sur … » et `start_quiet_period` sur
  « silence pendant N heures », et rend « Résumé simulé de N caractères. » quand on lui demande de raccourcir
  un dernier message.

## Écarts par rapport à l'ADR 0021

1. **L'ADR 0024 n'est pas encore accepté.** Tout le chantier repose sur son choix recommandé.
2. **Le badge ambre de l'orbe** pendant le silence (ADR 0021, section 9) n'est pas dans ce chantier : il
   arrive avec l'éventail (chantier 14), qui affiche les tâches en attente.
3. **« Je vais dormir » passe par la description de l'outil**, pas par du code : c'est le modèle local qui
   reconnaît la phrase et propose. Aucun essai avec le vrai modèle n'a mesuré s'il le fait bien.
4. **L'annonce d'une session lancée par Jarvis est dite à la fin de son tour** (statut `idle`), pas à la fin
   du processus : c'est le moment où son dernier message est connu.

## Choix provisoires de ce chantier

À confirmer par Benoit ; chacun se change sans toucher aux autres.

1. Cinq annonces au plus en attente, dix minutes d'attente au plus.
2. Le dernier message n'est raccourci par le modèle local que s'il dépasse 160 caractères et que le modèle
   est déjà en mémoire.
3. La période de silence est perdue quand Jarvis se ferme.
4. Une session arrêtée par l'utilisateur n'est pas annoncée.
5. Chaque attente d'une permission est annoncée, même plusieurs de suite pour la même session.
6. Une annonce ne compte pas comme une activité dans le délai qui rend un échange « ancien ».

## Non vérifié

- **Rien n'a tourné sur Windows, et aucune annonce n'a été dite par une vraie voix.**
- Aucune session lancée à la main n'a été annoncée par de vrais hooks.
- La qualité du dernier message raccourci par le modèle local, et le temps que cela prend.
- Si le modèle local propose bien le silence sur « je vais dormir ».

## Tests

- Unitaires : `AssistantStateMachineTests` (ligne de l'ADR 0024), `AnnouncementDeskTests` (écrit puis dit,
  muet, attente du repos, une à la fois, coupure, attente trop longue, jamais envoyé au modèle),
  `SessionAnnouncerTests` (table des nouvelles, dossier, silence, dernier message raccourci ou coupé, les deux
  outils, réglages).
- De bout en bout : `SessionAnnouncementsTests` (4 scénarios sur l'hôte Web).

## Vérifications sur Windows

À faire avec Benoit, sur son PC :

1. Une session lancée par Jarvis demande une écriture : Jarvis dit qu'elle attend, la fenêtre de confirmation
   est là.
2. Elle se termine : Jarvis dit qu'elle a terminé, avec une ou deux phrases sur ce qu'elle a fait.
3. Une session lancée à la main, hooks installés : mêmes annonces.
4. Une annonce pendant que Jarvis répond : elle attend la fin de la réponse.
5. Le mot d'éveil pendant une annonce : elle se coupe, Jarvis écoute.
6. Heures de silence : rien n'est dit, l'annonce est dans la conversation.
7. « Je vais dormir » : Jarvis propose 8 heures de silence ; après « oui », la page « Annonces » montre la
   période et son heure de fin.
8. « Où en est Claude sur *dossier* ? » : la réponse correspond à la page « Sessions Claude ».
