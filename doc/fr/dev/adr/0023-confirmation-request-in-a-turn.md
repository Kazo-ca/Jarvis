# ADR 0023 : la demande de confirmation dans un tour (complète 0005)

## Statut

Accepté par Benoit le 2026-11-21 (chantier `stage2/confirmation-adr`), avec ses trois réponses, reprises
dans la section « Réponses de Benoit ». Elles diffèrent des recommandations : l'ADR décrit ses choix.

- **Complète :** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) : une condition et un aspect
  de plus. Aucune phase et aucune transition de sa table ne change.
- **Applique :** [ADR 0009](0009-permission-and-confirmation-model.md), section 3 (ce que porte une demande,
  la réponse vocale reconnue par du code, un accord pour une action), et
  [ADR 0017](0017-tools-classes-and-decision-point.md), section 2 (étape 4 du point de décision).
- **Récits :** US2.10 (socle) du [document de conception](../plans/design.md).
- **Ne décide pas :** les demandes de permission d'une session Claude Code
  ([ADR 0021](0021-claude-code-sessions.md)), ni le téléphone jumelé comme canal fort (étape 4).

## Contexte

- Depuis le chantier `stage2/3-tool-loop`, le point de décision refuse tout appel « Sensible » ou
  « Critique » : personne ne peut être interrogé.
- L'ADR 0009 dit ce qu'est une demande de confirmation. Il ne dit pas comment elle s'insère dans un tour :
  la machine à états de l'ADR 0005 n'a que quatre phases, et la fin de la parole y termine le tour.
- La tranche de transcription n'écoute qu'au début d'un tour dit. Une réponse « oui » ou « non » arrive au
  milieu d'un tour, pendant que la boucle d'outils attend.
- Une demande de confirmation viendra aussi d'ailleurs que d'un tour : une session Claude Code (chantier 12),
  plus tard une tâche autonome.

## Décision

### 1. Une condition de plus, aucune transition changée

- L'état de l'assistant gagne une quatrième condition : **interaction requise** (`PendingInteraction`).
  Elle porte l'identifiant de ce qui attend et son genre : question, approbation, confirmation ou action
  manuelle. La demande de confirmation est le premier genre construit ; les autres s'en serviront sans
  nouvel ADR.
- Deux événements d'entrée : « demande ouverte » pose la condition, « demande fermée » la lève. La machine
  reste une fonction pure. Une seule demande est ouverte à la fois ; les autres attendent leur tour.
- **La phase ne change pas pendant une demande.** Une demande née dans un tour garde la phase où
  le tour se trouve : « Réflexion » le plus souvent, « Parole » si le modèle a déjà commencé à parler avant
  d'appeler l'outil. La boucle d'outils attend. La question dite et la réponse écoutée ne font entrer ni
  en « Parole » ni en « Écoute ».
- Une activation pendant une demande (mot d'éveil, raccourci, clic sur l'orbe) reste l'interruption de
  l'ADR 0005 : le tour est annulé, la demande est fermée par un refus, journalisé « interrompu ».
- **Aspect** (réponse 1) : tant que la condition est posée, l'orbe montre un aspect « interaction
  requise », le même pour tous les genres. Il passe avant l'aspect de la phase. L'animation suit la règle
  des phases actives (ADR 0005, section 6).

### 2. Ce que voit et entend l'utilisateur

- Une **fenêtre de confirmation** s'ouvre près de l'orbe : qui demande, le résumé dans la langue de
  l'assistant, le détail exact (nom de l'outil et arguments, sans rien couper), la classe, et les boutons
  **Approuver** et **Refuser**. Elle se ferme avec la demande.
- Pour une demande née d'un tour **dit**, Jarvis dit le résumé à voix haute, suivi de la question
  (« Confirmez-vous ? »). Pour la classe « Critique », la phrase dit que la voix ne suffit pas : il faut
  cliquer.
- Pour une demande née d'un tour **écrit** (réponse 3) : Jarvis dit seulement qu'une interaction est
  requise, sans le résumé ni la question, et n'écoute pas de réponse. La fenêtre dit le reste. La forme
  voulue par Benoit est « la tâche X du projet Y a besoin de votre interaction » ; dans une simple
  conversation, sans tâche ni projet, la phrase est « Votre demande a besoin de votre interaction ».
- Le résumé est écrit par du code à partir de l'outil et de ses arguments, jamais par le modèle.

### 3. La réponse vocale

- Elle n'existe que pour la classe « Sensible », dans un tour dit, micro non coupé.
- Juste après sa question, Jarvis ouvre **une seule écoute**. Elle se termine à la fin de la phrase, comme
  une demande (même détection de fin de phrase, même Whisper).
- **Du code reconnaît la réponse**, pas le modèle. La transcription entière, sans ponctuation ni majuscules,
  doit être l'une des entrées d'une liste fermée par langue :
  - oui : « oui », « oui Jarvis », « d'accord », « confirme », « je confirme », « approuve », « vas-y »,
    « yes », « yes Jarvis », « confirm », « I confirm », « approve », « go ahead » ;
  - non : « non », « non Jarvis », « annule », « refuse », « arrête », « no », « no Jarvis », « cancel »,
    « deny », « stop ».
- **Toute autre réponse vaut refus** (ADR 0009). Si rien n'est entendu, la demande reste ouverte : le clic
  reste possible.
- Le texte transcrit pendant cette écoute ne va jamais au modèle.

### 4. Aucun délai, et les canaux

- **Aucun délai** (réponse 2) : Jarvis ne décide jamais à la place de l'utilisateur. Une demande reste
  ouverte jusqu'à sa réponse. Elle ne se ferme autrement que par une interruption, qui est aussi un geste
  de l'utilisateur. Le tour attend avec elle.
- Canaux acceptés : « Sensible », la voix ou le clic ; « Critique », le clic seulement. Un accord arrivé par
  un canal que la classe n'accepte pas est rejeté et journalisé.
- Un accord vaut pour **une** action : celle de la demande, avec ses arguments exacts.

### 5. Journal

- La décision journalisée (ADR 0017) porte maintenant le canal de la réponse : `voice`, `click`, ou aucun
  pour une interruption.
- Une page « Journal des permissions » des paramètres montre les décisions, les plus récentes d'abord.

### 6. Tranches et contrats

- Noyau : la condition, les deux événements, l'aspect ; un contrat « demander une confirmation » que le
  point de décision appelle, et les messages entre tranches (demande ouverte, question dite, réponse
  entendue, demande fermée).
- `Permissions/Confirmation` : la demande, les mots reconnus, la fenêtre.
- `Permissions/Decision` : appelle le contrat à l'étape 4, journalise le canal, porte la page du journal.
- `Voice/Speech` dit la question ou l'annonce ; `Voice/Transcription` ouvre l'écoute de la réponse. Aucune tranche n'en
  référence une autre.

## Réponses de Benoit

Questions posées dans la conversation le 2026-11-21 ; ses réponses, le même jour :

1. **Que montre l'orbe pendant une demande de confirmation ?** « Mode "interaction requise" : question,
   approbation, confirmation ou action manuelle de l'utilisateur. » La recommandation était un aspect
   propre à la confirmation.
2. **Combien de temps Jarvis attend-il une réponse ?** « Aucun. Il ne décide jamais à la place de
   l'utilisateur. » La recommandation était 30 secondes, réglable.
3. **Une demande écrite a besoin d'une confirmation : Jarvis parle-t-il ?** « Jarvis fait juste dire "la
   tâche X du projet Y a besoin de votre interaction". » La recommandation était la fenêtre seule.

## Options écartées

- **Passer par les phases « Parole » puis « Écoute » pour la question et la réponse.** Il faudrait changer
  la table de l'ADR 0005 : aujourd'hui la fin de la parole termine le tour, et « rien entendu » l'annule.
- **Laisser le modèle comprendre la réponse.** Contraire à l'ADR 0009 : seul un humain approuve, et du code
  reconnaît son accord.
- **Chercher « oui » n'importe où dans la phrase.** « Oui, enfin non, attends » contient « oui ».
- **Plusieurs écoutes tant que la demande est ouverte.** Une seule écoute, celle qui suit la question
  (ADR 0009) : un bruit de fond dix secondes plus tard ne doit pas compter.
- **Un délai qui refuse sans réponse** (30 secondes, réglable). Écarté par Benoit : ce serait décider à la
  place de l'utilisateur.

## Conséquences

- La table des transitions de l'ADR 0005 ne change pas ; ses tests restent valables.
- Sans délai, un tour peut rester en « Réflexion » aussi longtemps que l'utilisateur ne répond pas, et
  aucun autre tour ne commence sans l'interrompre. C'est voulu : rien n'est décidé sans lui.
- Les noms de tâche et de projet de l'annonce arriveront avec les sessions et les tâches (chantier 12 et
  suivants) ; une demande née hors d'un tour ne dit rien tant qu'ils n'existent pas.
- Une réponse hésitante (« euh, oui ») est un refus : l'utilisateur redemande ou clique. C'est le prix d'une
  liste fermée.
- Pendant une demande, l'orbe ne montre pas le niveau du micro comme en écoute : c'est la fenêtre qui dit
  que Jarvis attend.
- **Non vérifié :** que Whisper transcrit bien un « oui » ou un « non » isolé avec le micro de Benoit, et
  que le haut-parleur ne se fait pas entendre par le micro juste après la question. À vérifier sur son PC ;
  si un « oui » seul est mal reconnu, la liste se complète sans nouvel ADR.
- La fenêtre « près de l'orbe » demande à l'hôte Windows de placer une fenêtre : à vérifier sur Windows.
