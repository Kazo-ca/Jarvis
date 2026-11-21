# ADR 0024 : le tour d'annonce (complète 0005)

## Statut

Accepté par Benoit le 2026-11-21 (proposé le même jour, chantier `stage2/13-announcement-turn-adr`).

- **Complète :** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) : une ligne de plus dans la
  table des transitions. Aucune phase et aucun aspect ne s'ajoute ; aucune ligne existante ne change.
- **Applique :** [ADR 0021](0021-claude-code-sessions.md), section 9 (Jarvis annonce à voix haute, sans
  interrompre l'utilisateur ni une réponse en cours ; pendant les heures de silence l'annonce reste dans la
  conversation) et section 8 (à la fin d'une session, Jarvis dit son résumé).
- **Récits :** US2.12 du [document de conception](../plans/design.md).
- **Ne décide pas :** ce qui est annoncé, le réglage par dossier ni les heures de silence (ADR 0021,
  section 9) ; les annonces d'autres origines (rappels, minuteries), qui pourront passer par le même tour.

## Contexte

Jusqu'ici, Jarvis ne parle que pour répondre : chaque phrase dite appartient à un tour que l'utilisateur a
ouvert (ADR 0005), et la question d'une confirmation se dit dans le tour qui l'a fait naître (ADR 0023).
L'ADR 0021 demande une parole que personne n'a demandée : « Claude Code a terminé sur Kazo Bar. » Elle doit
attendre que l'assistant soit au repos, se voir sur l'orbe, et se couper comme une réponse quand
l'utilisateur parle.

La table des transitions de l'ADR 0005 n'a aucune ligne pour cela. L'ADR 0005 dit qu'une paire absente de la
table est ignorée, et que la table est le contrat : il faut un ADR pour y ajouter une ligne.

## Décision

### 1. Une ligne de plus dans la table

| Phase | Événement | Nouvelle phase | Effet |
| --- | --- | --- | --- |
| Veille | Annonce soumise | Réflexion | Nouveau tour, d'entrée « annonce », sans micro et sans modèle. |

- Dans toute autre phase, l'annonce soumise est **ignorée** : c'est à celui qui la soumet d'attendre le
  repos et de la soumettre de nouveau (section 3).
- Le tour d'annonce suit ensuite les lignes existantes : « première phrase prête » le fait passer à
  « Parole », « fin de la parole » le ramène à « Veille ». L'orbe montre donc l'aspect « parole », sans
  aspect nouveau.
- **Interruption** : une activation pendant une annonce la coupe net et ouvre un tour d'écoute, comme pour
  une réponse (US1.12). L'annonce coupée n'est pas redite.
- **La condition « erreur » n'est pas levée** par la fin d'un tour d'annonce. Un tour qui réussit lève
  l'erreur parce qu'il prouve que le micro et le modèle marchent ; une annonce ne prouve ni l'un ni l'autre.
- Une annonce se dit aussi quand la condition « interaction requise » est posée (ADR 0023) : « Claude Code
  attend votre permission » arrive justement à ce moment. Elle se dit aussi micro coupé : la coupure du
  micro n'est pas le silence.
- Si la voix échoue, le tour échoue comme un autre (condition « erreur », cause de la voix).

### 2. Ce que voit et entend l'utilisateur

- Une annonce s'écrit dans la conversation **dès qu'elle arrive**, dite ou non, comme une ligne de Jarvis
  sans demande au-dessus. C'est ce qui reste d'une annonce muette (heures de silence) ou coupée.
- Une annonce n'est **pas envoyée au modèle** dans l'historique des tours suivants, ni gardée par la mémoire
  de conversation (ADR 0019) : son texte peut venir d'une session, donc d'une donnée (ADR 0021, section 9).
- Les annonces se disent une par une, dans l'ordre d'arrivée. Une annonce qui attend le repos depuis plus de
  dix minutes n'est plus dite : elle reste écrite. Au plus cinq attendent ; au-delà, les plus anciennes ne
  sont plus dites.

### 3. Tranches et contrats

- Le noyau porte deux messages : `AnnouncementToSay` (le texte, la langue, à voix haute ou non), que publie
  toute tranche qui a quelque chose à annoncer, et l'événement d'entrée `AnnouncementSubmitted` de la
  machine d'états.
- La tranche de la conversation écrit l'annonce dans son journal, attend le repos, soumet l'annonce, puis
  donne sa phrase à la voix quand le tour commence. Elle ne décide pas **si** une annonce se dit : celui qui
  la publie le décide (réglage par dossier, heures de silence).
- La tranche de la voix ne change pas : elle dit les phrases d'un tour.

## Réponse de Benoit

Question posée dans la conversation le 2026-11-21 : comment Jarvis dit une annonce que personne n'a demandée.
Trois choix : un tour d'annonce (recommandé, cet ADR), une parole hors de tout tour, ou rien à voix haute.
Benoit a répondu « Ok » le même jour : il prend la recommandation, le tour d'annonce.

## Options écartées

- **Dire l'annonce hors de tout tour**, comme la question d'une confirmation (ADR 0023). La question d'une
  confirmation vit dans un tour qui existe déjà ; une annonce n'en a pas. Sans tour, l'orbe ne montre pas que
  Jarvis parle, une activation pendant l'annonce ouvre le micro sur la voix de Jarvis, et rien ne la coupe.
- **Un tour de texte ordinaire** (« Texte soumis »). Le texte irait au modèle local, qui répondrait à
  l'annonce au lieu de la dire.
- **Une phase « Annonce »**. Une phase de plus demande un aspect de plus et touche tous ceux qui lisent la
  phase, pour un comportement identique à « Parole ».
- **Interrompre une réponse pour annoncer.** L'ADR 0021 l'exclut.

## Conséquences

- La machine d'états garde ses quatre phases ; son état retient en plus que le tour en cours est une annonce,
  pour ne pas lever l'erreur à sa fin.
- Les tranches qui réagissent à « nouveau tour » voient une troisième entrée, « annonce » : l'écoute ne
  s'ouvre pas, le modèle n'est pas appelé.
- Le chantier 13 (annonces des sessions) est le premier à publier `AnnouncementToSay`. Le résumé de fin d'une
  session lancée par Jarvis, laissé de côté au chantier 12, passe par là.
- À vérifier sur Windows : une annonce dite pendant que rien ne se passe, puis coupée par le mot d'éveil.
