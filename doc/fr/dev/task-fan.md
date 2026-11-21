# Éventail des tâches et double-clic sur l'orbe

Chantier `stage2/14-task-fan`. Décisions : [ADR 0010](adr/0010-task-fan-and-orb-click.md) (l'éventail, le
simple clic et le double-clic), [ADR 0013](adr/0013-relais-readiness.md), section 6 (un panneau par tâche,
ses sous-tâches dedans, ambre quand elle attend), [ADR 0011](adr/0011-orb-colors-and-drawing.md) (cyan,
ambre, badge) et l'essai E11 ([résultats](../../../spikes/e11-task-fan-window/RESULTS.md)).

## Ce que voit l'utilisateur

- **Un double-clic sur l'orbe** fait sortir de l'orbe les panneaux des tâches en cours, en arc, vers le
  centre de l'écran. Un trait pointillé relie chaque panneau à l'orbe ; le bureau derrière est assombri.
- **Un second double-clic, la touche Échap ou un clic sur le bureau assombri** les y fait rentrer.
- Un panneau montre une tâche : qui travaille, sur quel projet, qui l'a lancée (Jarvis ou l'utilisateur),
  son titre, son état, l'heure de son dernier changement, son dernier message et ses sous-tâches.
- Un panneau qui attend l'utilisateur est **ambre**, son trait aussi.
- Quand une session lancée par Jarvis demande une permission, son panneau montre la demande et les boutons
  **Autoriser**, **Refuser** et **Détails**. La fenêtre de confirmation reste ouverte : les deux font la même
  chose.
- Près de l'orbe, une ligne compte les tâches et les décisions en attente. Sans tâche, l'éventail s'ouvre
  quand même et dit « Aucune tâche en cours. ».
- **Un simple clic sur l'orbe** lance toujours l'écoute, mais après le délai de double-clic de Windows (une
  demi-seconde par défaut). Le mot d'éveil et le raccourci ne sont pas retardés.
- **Le badge ambre de l'orbe** donne le nombre de décisions en attente, éventail ouvert ou fermé. L'orbe
  prend l'aspect « tâche longue » tant qu'une tâche travaille.

L'éventail n'est pas un état de l'assistant : Jarvis peut écouter, réfléchir ou parler pendant qu'il est
ouvert.

## Messages et contrats du noyau

| Élément | Rôle |
| --- | --- |
| `TaskFanToggleRequested` (`Kernel/Messages/Desktop`) | Le double-clic sur l'orbe : la tranche de l'éventail l'ouvre ou le range |
| `IPointerTimings` (`Kernel/Desktop`) | Le délai de double-clic, donné par l'hôte ; sans lui, pas de double-clic ni de délai |
| `ITaskFanSurface`, `FanAnchor` (`Kernel/Desktop`) | La surface où l'éventail est dessiné : où est le centre de l'orbe, montrer, cacher |
| `IOpenConfirmations`, `OpenConfirmation` (`Kernel/Permissions`) | La demande de confirmation ouverte et son clic, pour qui la montre en plus de la fenêtre |
| `ConfirmationRequest.TaskId`, `SessionAction.TaskId` | La tâche qui demande, pour placer la demande dans son panneau |
| `DelegatedTaskCountChanged` (déjà dans l'ADR 0005) | Le nombre de tâches qui travaillent, maintenant publié par le tableau |

## Le clic sur l'orbe (`Assistant/Orb`)

`OrbActivation.ClickAsync` est appelé une fois par clic, par le composant (hôte Web) ou par la fenêtre de
l'orbe (hôte Windows).

- Le premier clic attend le délai de `IPointerTimings`, puis publie `ActivationRequested(Click)`.
- Un second clic dans ce délai publie `TaskFanToggleRequested` ; le premier ne publie rien. **Un double-clic
  n'est jamais aussi un simple clic.**
- Sans `IPointerTimings` (mobile), le clic agit tout de suite.
- `OrbView` calcule le badge : les tâches qui attendent l'utilisateur, plus la demande de confirmation ouverte
  quand elle ne vient pas d'une de ces tâches.

## Tranche `Tasks/Fan`

| Fichier | Rôle |
| --- | --- |
| `TaskFan` | Ouvert, en fermeture ou fermé ; reçoit le double-clic ; montre et cache la surface |
| `FanContent` | Ce que l'éventail montre, d'après le tableau des tâches et la demande ouverte |
| `FanLayout` | Où vont les panneaux : côté du centre de l'écran, angles par nombre de panneaux |
| `TaskFanView` | Le dessin, dans une seule surface |

- **Contenu.** Un panneau par tâche qui n'est pas finie (hors sous-tâches), celles qui attendent d'abord,
  puis la plus récemment changée. **Cinq panneaux au plus** ; les autres sont comptées dans la ligne près de
  l'orbe. Deux sous-tâches au plus par panneau, puis « et N de plus ».
- **Demande de confirmation.** Elle s'affiche dans le panneau de la tâche qui demande (`TaskId`). Une demande
  qui ne vient d'aucune tâche (un outil du modèle local) a son propre panneau ambre. La tâche qui demande a
  toujours son panneau, même au-delà de cinq.
- **Disposition.** Les longueurs sont en `em` ; la taille du texte suit le petit côté de la surface (16 px au
  plus), donc le même arc tient sur un petit écran. Hors d'un coin, l'éventail prend la disposition du coin
  le plus proche.
- **Dessin.** Un SVG dont l'origine est le centre de l'orbe : traits, panneaux (`foreignObject`) et ligne de
  compte sont dans un seul groupe. L'ouverture et la fermeture mettent ce groupe à l'échelle autour de
  l'orbe.
- **Animation** (ADR 0010, section 6) : 400 ms à l'ouverture en 12 pas, 300 ms à la fermeture en 9 pas, donc
  30 images par seconde au plus. Ouvert, rien ne bouge. Mouvement réduit (Windows ou navigateur) : aucune
  animation.
- **Une tâche qui change** redessine l'éventail au plus une fois par seconde. Une demande de confirmation qui
  s'ouvre ou se ferme est dessinée tout de suite.
- **Fermé**, le composant ne dessine rien, ne lit pas le tableau et ne tient aucune minuterie.

## Tranche `Tasks/Board`

`TaskCount` publie `DelegatedTaskCountChanged` : le nombre de tâches `pending` ou `working`, hors
sous-tâches. Une tâche qui attend est montrée par le badge ; une tâche dont le tour est fini ne travaille plus.

## Hôte Windows

| Fichier | Rôle |
| --- | --- |
| `Fan/FanWindow` | Fenêtre transparente, sans bordure, toujours au premier plan, hors de la barre des tâches |
| `Fan/FanWindowService` | `ITaskFanSurface` : place la fenêtre sur la zone de travail de l'écran de l'orbe |
| `Fan/FanShell.razor`, `wwwroot/fan.html` | La racine de la vue web de l'éventail, fond transparent |
| `Desktop/SystemPointerTimings` | Le délai de double-clic réglé dans Windows (`GetDoubleClickTime`) |

- La fenêtre est **créée une fois, après l'orbe, et gardée cachée** (essai E11). Pour que sa vue web démarre,
  elle est affichée une fois, vide, puis cachée dès que la vue web existe.
- À l'ouverture, la fenêtre prend le focus du clavier (pour Échap), puis l'orbe est remis au-dessus. À chaque
  activation de la fenêtre de l'éventail, l'orbe est remis au-dessus (piège de l'essai E11).
- Échap est attrapé par la fenêtre WPF, quel que soit le focus dans la page.
- La fenêtre de l'orbe n'est pas agrandie.
- Si le bureau est perdu (session verrouillée), la fenêtre de l'éventail est fermée avec celle de l'orbe et
  recréée ensuite.

## Hôte Web

- L'orbe est maintenant dans un coin de la page, au-dessus de tout, comme sur le bureau. Le bouton
  « Move the orb to the next corner » le change de coin.
- `SimulatedTaskFanSurface` : surface et délai de double-clic simulés (300 ms).
- `TaskFanView` est dessiné par-dessus la page. `#task-fan` dit si la surface est montrée et dans quel coin
  est l'orbe.
- « Make it start a sub-agent » ajoute une sous-tâche à la session lancée à la main.

## Choix provisoires de ce chantier

L'ADR 0010 (section 9) laisse ces points ouverts. À confirmer par Benoit ; chacun se change sans toucher aux
autres.

1. Cinq panneaux au plus ; les autres tâches sont comptées dans une ligne.
2. Pas de ✕ sur les panneaux : une tâche s'arrête depuis la page « Tâches Claude Code ».
3. Une tâche dont le tour est fini (`idle`) ou bloquée garde son panneau jusqu'à ce qu'elle soit finie.
4. L'orbe compte comme « tâche longue » les tâches `pending` et `working` seulement.
5. L'éventail s'ouvre sur la zone de travail de l'écran où est l'orbe ; rien sur les autres écrans.
6. Un triple clic ouvre l'éventail puis compte comme un simple clic.

## Non vérifié

- **Rien n'a tourné sur Windows.** La fenêtre de l'éventail, son démarrage cachée, le focus, l'orbe remis
  au-dessus et le passage des clics n'ont été vérifiés que par la compilation.
- Les mesures de l'ADR 0010 (section 7) ne sont pas refaites sur l'hôte réel : l'essai E11 mesurait une page
  HTML, pas les vues Razor.
- La fluidité de l'animation et le confort du simple clic retardé, à juger par Benoit.

## Tests

- Unitaires : `OrbDoubleClickTests` (simple clic retardé, double-clic, deux clics espacés), `TaskFanTests`
  (ouverture et fermeture, réouverture pendant la fermeture, coins, panneaux qui ne se chevauchent pas,
  contenu, demande dans le panneau de sa tâche, demande à part, plus de cinq tâches, compte des tâches qui
  travaillent).
- De bout en bout : `TaskFanTests` (4 scénarios sur l'hôte Web).

## Vérifications sur Windows

À faire avec Benoit, sur son PC :

1. Double-clic sur l'orbe : l'éventail sort sans retard visible, le bureau s'assombrit, Jarvis n'écoute pas.
2. Second double-clic, Échap, clic sur le bureau assombri : l'éventail rentre ; l'application dessous ne
   reçoit pas le clic.
3. Éventail fermé : les clics atteignent l'application dessous, partout sur l'écran.
4. Clic sur un panneau, puis double-clic sur l'orbe : l'orbe est resté au-dessus.
5. Une session lancée par Jarvis demande une écriture : « Autoriser » dans son panneau la laisse continuer.
6. L'orbe dans chacun des quatre coins, puis sur un autre écran : l'éventail s'ouvre vers le centre.
7. Simple clic : l'écoute commence après le délai de double-clic.
8. Coût (méthode de l'essai E6) : éventail fermé, ouvert et immobile, pendant l'ouverture et la fermeture.
9. Au démarrage, la fenêtre de l'éventail préparée ne se voit pas et ne prend aucun clic.
