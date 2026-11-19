# ADR 0005 : machine à états de l'assistant et contrat de l'orbe

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 5, 6, 15, 18 et 31 de la [conception](../plans/design.md) et sa section
  « L'orbe et ses sept états » ; récits US1.1, US1.3, US1.6, US1.12, US1.13 et US1.17 ; essai E6
  ([résultats](../../../../spikes/e6-orb-window/RESULTS.md)) ; décision n° 1 des
  [essais](../spikes.md#décisions-prises-le-18-novembre-2026) (orbe immobile en veille).
- **Complète :** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (la machine à états est dans
  le noyau) et [ADR 0003](0003-hosts-and-native-services.md) (fenêtre de l'orbe).
- **Complété par :** [ADR 0006](0006-voice-chain.md) (qui produit les événements de la voix).

## Contexte

- L'orbe a sept états, et chacun doit se reconnaître du coin de l'œil. L'état « tâche déléguée » se
  superpose aux autres : Jarvis peut écouter ou parler pendant que Claude travaille.
- Les tranches ne se référencent pas (ADR 0002). Il faut donc un seul endroit qui dit dans quel état est
  l'assistant, et une seule façon de le faire changer.
- L'essai E6 a échoué sur sa cible : aucun montage ne tient moins de 2 % d'un cœur avec un orbe animé en
  veille. Une image fixe coûte 1,3 %. À 30 images par seconde, l'orbe coûte 14 à 18 % d'un cœur.
- Une boucle `requestAnimationFrame` réveille le moteur 60 fois par seconde même sans dessiner
  ([pièges](../pitfalls.md)).
- Le même composant d'orbe sert sur Windows, dans l'hôte Web et sur mobile.

## Décision

### 1. Une phase et trois conditions

L'état de l'assistant se compose d'une **phase**, exclusive, et de **conditions** qui s'y ajoutent.

| Phase | Quand |
| --- | --- |
| Veille (`Idle`) | Attend une activation |
| Écoute (`Listening`) | Capte la demande |
| Réflexion (`Thinking`) | Transcrit, puis attend le modèle |
| Parole (`Speaking`) | Dit la réponse |

| Condition | Quand |
| --- | --- |
| Micro coupé (`MicrophoneMuted`) | Coupure volontaire (US1.13) |
| Erreur (`Fault`) | Micro, modèle ou voix inutilisable ; porte la cause et l'action corrective (US1.17) |
| Tâches déléguées (`DelegatedTasks`) | Nombre de tâches en arrière-plan (étape 2) |

### 2. Des sept états de la conception à l'affichage

L'orbe affiche un seul **aspect**, calculé par une fonction pure du noyau, la même pour tous les hôtes :

1. si la phase est Écoute, Réflexion ou Parole : l'aspect de cette phase ;
2. sinon, s'il y a une erreur : l'aspect « erreur » ;
3. sinon, si le micro est coupé : l'aspect « micro coupé » ;
4. sinon : l'aspect « veille ».

Les satellites des tâches déléguées se dessinent par-dessus l'aspect, quel qu'il soit. On retrouve les sept
états de la conception : six aspects exclusifs et une superposition.

### 3. Transitions

Chaque tour de conversation a un identifiant. Tous les messages du tour le portent.

| Phase | Événement | Nouvelle phase | Effet |
| --- | --- | --- | --- |
| Veille | Activation (mot d'éveil, raccourci, clic) | Écoute | Nouveau tour. Refusée si le micro est coupé ou absent. |
| Veille | Texte soumis dans la conversation | Réflexion | Nouveau tour, sans micro. |
| Écoute | Fin de phrase | Réflexion | |
| Écoute | Rien entendu avant le délai | Veille | Tour abandonné. |
| Écoute | Raccourci ou clic | Veille | Écoute annulée. |
| Écoute | Micro coupé | Veille | Tour annulé. |
| Réflexion | Première phrase prête à être dite | Parole | |
| Réflexion | Réponse terminée sans parole | Veille | |
| Réflexion, Parole | Activation (mot d'éveil, raccourci, clic) | Écoute | Interruption (US1.12) : le tour en cours est annulé, la parole coupée net, un nouveau tour commence. |
| Parole | Fin de la parole | Veille | |
| Écoute, Réflexion, Parole | Échec du tour | Veille | La condition « erreur » est posée avec sa cause. |

- Pendant l'écoute, le mot d'éveil est ignoré.
- Toute paire (phase, événement) absente de la table est ignorée et journalisée.
- Un message qui porte l'identifiant d'un tour annulé est ignoré.
- La condition « erreur » se lève quand sa cause disparaît ou quand un tour réussit.

### 4. Règles de la machine

- La machine est dans le noyau. C'est une fonction pure : (état, événement) donne (nouvel état, effets).
  Elle ne fait aucune entrée ni sortie et n'a pas de minuterie : un délai est un événement publié par une
  tranche.
- **Un seul écrivain.** Les événements sont traités un par un, dans l'ordre d'arrivée.
- **Personne ne fixe l'état directement.** Une tranche publie un événement ; la machine décide. Chaque
  changement est publié par le message `AssistantStateChanged`.
- Elle se teste sans micro, sans modèle et sans interface.

### 5. Contrat de l'orbe

- **Un seul composant Razor**, dans la tranche de l'orbe, pour les trois hôtes. Son dessin est en Canvas 2D,
  dans un fichier `.razor.js` à côté du composant. Le JavaScript ne fait que dessiner : aucune règle d'état
  n'y est écrite.
- **Entrées du composant :** l'aspect, le nombre de tâches déléguées, la cause de l'erreur (affichée au
  survol), le niveau sonore, l'option « mouvement réduit » et la règle d'animation donnée par l'hôte.
- **Niveau sonore :** une valeur de 0 à 1, celle du micro en écoute et celle de la voix synthétisée en
  parole. Elle arrive par un contrat dédié, pas par le bus, au plus 30 fois par seconde.
- **Sorties du composant :** clic (activation, US1.6) et début de déplacement (US1.1).
- **Page de démonstration** (US1.3) : elle pilote le composant directement par ses entrées, sans micro ni
  modèle.

### 6. Règle d'animation

| Situation | Règle |
| --- | --- |
| Veille, micro coupé, erreur sur Windows | **Image fixe.** Un seul dessin au changement d'aspect, puis aucune minuterie et aucun `requestAnimationFrame`. |
| Écoute, réflexion, parole | Animation complète, au plus 30 images par seconde. |
| Animation sous 10 images par seconde | Par minuterie, jamais par `requestAnimationFrame`. |
| Satellites des tâches déléguées, en veille sur Windows | Dessinés immobiles. Leur position avance par pas, au plus une image toutes les 5 secondes au départ ; l'intervalle est réglé par la mesure. |
| Mouvement réduit | Aucune animation : les aspects se distinguent par la teinte et la forme. |

- La règle « image fixe en veille » est un paramètre que l'hôte donne au composant. L'hôte Windows l'active.
  L'hôte Web propose les deux, pour la démonstration. Le choix du mobile se fera à l'étape 4.
- **Budget en veille sur Windows :** moins de 2 % d'un cœur pour l'affichage (processus WPF et processus
  WebView2), mesuré avec la méthode de l'essai E6 sur l'hôte réel avant de déclarer la tranche de l'orbe
  terminée. Le mot d'éveil a son propre coût, mesuré à part (ADR 0006).
- Les phases animées durent quelques secondes. Leur coût (14 à 18 % d'un cœur à 30 images par seconde) est
  accepté.

### 7. Ce que cet ADR ne décide pas

- **Les couleurs et le dessin de l'orbe.** La conception les renvoie à un ADR dédié, à écrire sur maquette
  avant le travail visuel de la tranche de l'orbe.
- **Si une réponse à une demande écrite est aussi dite à voix haute.** La table permet les deux ; le plan de
  la série de la conversation le précise.
- **Ce que fait un clic sur l'orbe au-delà de l'activation.** Le 2026-11-19, Benoit a demandé qu'un clic
  fasse sortir de l'orbe les fenêtres des tâches en cours, et qu'un second clic les y fasse rentrer. Ce
  comportement, et la façon dont il se combine avec l'activation par clic (US1.6), sont fixés par l'ADR de
  l'orbe à venir, qui remplacera au besoin les lignes « clic » de la table des transitions.
- **Plusieurs appareils** (décision n° 31). L'état est calculé sur le PC. La façon de le diffuser aux
  téléphones et de choisir l'appareil qui parle est fixée par l'ADR du mobile, à l'étape 4.

## Conséquences

- L'orbe animé en veille de la conception (« anneaux lents, respiration calme ») est remplacé sur Windows
  par une image fixe. C'est la décision prise après l'essai E6.
- Ajouter un état demande un nouvel ADR : la table des transitions et la fonction d'aspect sont le contrat
  entre toutes les tranches.
- La chorégraphie d'un tour passe par des messages : la tranche du mot d'éveil ne connaît ni la
  transcription ni le modèle.
- **Risque à vérifier à l'étape 1 :** pendant la parole, le mot d'éveil doit être reconnu par-dessus la voix
  de Jarvis qui sort des haut-parleurs. Ce n'est pas mesuré. Si la détection n'est pas fiable, l'interruption
  pendant la parole se fera par le raccourci et le clic seulement, et un nouvel ADR le dira.
- Le rendu natif par le GPU (piste n° 3 de l'essai E6) est écarté : il demanderait un deuxième composant
  d'orbe, que le mobile et l'hôte Web ne partageraient pas.
