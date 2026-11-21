# Conversation : le tour complet

Chantier `stage1/13-conversation` (US1.9, US1.12, US1.14, US1.15, ADR [0005](adr/0005-assistant-state-machine-and-orb-contract.md),
[0007](adr/0007-llm-providers-and-tool-format.md) et [0008](adr/0008-languages-and-localization.md)) : Jarvis
répond. La demande dite ou écrite va au modèle local par défaut, la réponse arrive en flux et chaque phrase
complète part tout de suite à la voix. Le mot d'éveil, le raccourci ou un clic l'interrompt. La fenêtre de
conversation garde l'historique ; la page « Personnalité » règle le message système.
English: [../../en/dev/conversation.md](../../en/dev/conversation.md).

## Ce qui est construit

```
mot d'éveil / raccourci / clic ─► ActivationRequested ─► machine à états ─► TurnStarted (voix)
                                                                            │
UtteranceListener (chantier 10) ─► UtteranceEnded ─► UtteranceTranscribed ──┤
fenêtre de conversation ─► TextSubmitted ─► machine à états ─► TurnStarted (texte)
                                                                            ▼
                                     ConversationEngine (tranche Conversation/Dialogue)
                     IAssistantModel (chantier 11) ◄─ message système + historique + demande
                                                                            │ réponse en flux
                       SentenceSplitter ─► SentenceToSpeak … AnswerSentencesEnded ─► voix (chantier 12)
                       demande écrite ─► AnswerFinishedWithoutSpeech (aucune voix)
                       ConversationJournal ─► fenêtre + collection Conversation.Turns (SQLite)
```

Aucune tranche n'en référence une autre : tout passe par les messages du noyau. Le noyau n'a pas changé.

Depuis le chantier `stage2/3-tool-loop`, le moteur donne aussi les outils activés au modèle et exécute ses
appels par le point de décision des permissions : voir [tools.md](tools.md).

### Tranche `Features/Conversation/Dialogue`

- **`ConversationEngine`** fait le tour :
  - **Demande écrite** (`TurnStarted` avec `TurnInput.Text`) : réponse écrite seulement, puis
    `AnswerFinishedWithoutSpeech`. **Jarvis ne parle que si on lui a parlé** (réponse n° 3 du plan).
  - **Demande dite** (`UtteranceTranscribed`) : chaque phrase complète est publiée en `SentenceToSpeak` dès
    qu'elle est écrite, puis `AnswerSentencesEnded`. La voix publie `FirstSentenceReady` et `SpeechEnded`.
  - **Annonce** (`TurnStarted` avec `TurnInput.Announcement`, ADR 0024) : le moteur ne fait rien, le modèle
    n'est pas appelé. `AnnouncementDesk` écrit l'annonce dans le journal dès qu'elle arrive et donne ses
    phrases à la voix quand son tour commence : voir [session-announcements.md](session-announcements.md).
    Une annonce n'entre jamais dans l'historique envoyé au modèle.
  - **Rien entendu** (texte vide de Whisper) : `AnswerFinishedWithoutSpeech`, sans appeler le modèle.
  - **Interruption** (`TurnCancelled`, US1.12) : la génération s'arrête (le jeton du flux est annulé) ; la voix
    se coupe d'elle-même. Le mot d'éveil reste actif pendant la réflexion et la parole (chantier 9).
  - **Panne du modèle** (`LlmException`) : `TurnFailed` avec la panne `ModelMissing` (modèle absent) ou
    `ModelUnreachable` (toute autre cause), et la cause précise en détail. Avant l'appel,
    `IAssistantModel.EnsureLoadedAsync` recharge le modèle si Ollama l'a libéré et l'annonce.
  - **Délai jusqu'au premier son** : de `UtteranceEnded` à `FirstSentenceReady`, écrit au journal et montré
    dans la fenêtre.
- **`SentenceSplitter`** découpe la réponse : une phrase finit par `.`, `!`, `?` ou `…` suivi d'un espace, ou
  par un saut de ligne. Une abréviation (« M. », « Dr. »), un nombre (« 3.5 »), une initiale ou un numéro de
  liste ne la finissent pas. Une phrase de plus de 250 caractères part jusqu'à sa dernière virgule. Les marques
  Markdown (`**`, `#`, accents graves, puces) ne sont pas dites.
- **`SystemPrompt`** : la personnalité de la langue (celle de l'utilisateur, sinon le majordome des
  ressources `PromptTexts`), puis les **règles fixes** : répondre dans la langue du tour, un texte fait pour
  être dit (ni Markdown, ni listes, ni emoji, ni code, des phrases courtes), la date et l'heure locales.
- **Historique envoyé au modèle** : les derniers tours répondus qui tiennent dans environ la moitié du
  contexte du fournisseur (3 caractères par jeton), 20 tours au plus et, quand la mémoire est en service,
  aucun tour d'avant un silence de 2 heures. Le bloc de la mémoire s'ajoute au message système après les
  règles fixes ([conversation-memory.md](conversation-memory.md)).
- **Passage de main** : une demande qui commence par « Demande à Claude… », ou un appel de l'outil
  `hand_off_to_claude` par le modèle local, fait répondre la cible cloud dans la même boucle ; le tour garde
  qui a répondu et pourquoi ([hand-off.md](hand-off.md)).
- **`ConversationJournal`** : les tours de la collection `Conversation.Turns` (lus une fois, au premier
  besoin) et le tour en cours. Un tour est enregistré **quand il finit** : répondu, interrompu (avec ce qui
  était écrit) ou en échec (avec la cause). Ce qui est enregistré (ADR 0007, section 7) : demande, réponse,
  langue, voix ou texte, fournisseur, modèle, jetons envoyés et reçus, durée, délai du premier son.
  Le journal technique ne contient jamais le texte (ADR 0004, section 6).
- **Fenêtre de conversation** (`ConversationWindow`, clé `Conversation.Window`), ouverte par l'entrée
  « Conversation » de la zone de notification (`ConversationWindowRequested`, active depuis ce chantier) :
  historique (le plus récent en bas, sans script), réponse qui s'écrit en direct, modèle et durée de chaque
  tour, « le modèle se charge » quand Ollama recharge, champ de saisie (Entrée envoie ; grisé tant que Jarvis
  n'est pas en veille), **« Tout effacer »** avec confirmation dans la fenêtre : conversation, mémoire et
  journal de la base ; réglages et clés gardés (ADR 0004).
- **Page « Personnalité »** des paramètres (`Conversation.Personality`, après « Modèles de langage ») : le
  message de chaque langue, la langue de l'assistant en premier, « Enregistrer » et « Réinitialiser ». Réglage
  `Conversation.Dialogue` de `config.json` : seulement les textes changés, par code de langue.

### Tranche `Features/Voice/Speech` (retouche)

`SpeechPlayer` écoute aussi `TurnFailed` : quand le modèle tombe au milieu d'une réponse, la voix se tait
tout de suite au lieu de finir les phrases déjà reçues.

### Ordre des pages des paramètres

Langue (0), Orbe (5), Modèles de langage (10), Personnalité (15), Audio (20), Mot d'éveil (25),
Transcription (30), Voix (35). Mot d'éveil, Transcription et Voix avaient toutes le rang 25.

### Hôte Web

Le bouton « Open the conversation (tray menu) » publie le même événement que la zone de notification. Avec
« Say “Hey Jarvis” » ou le raccourci, puis « Say a phrase », le tour complet tourne sur les simulations :
transcription, modèle (`SimulatedLlmConnector`, une réponse de deux phrases, mot par mot) et voix.

## Choix provisoires de ce chantier

Les ADR ne les tranchent pas ; à confirmer par Benoit :

1. **Fin de phrase** : `.`, `!`, `?`, `…` suivis d'un espace, ou un saut de ligne ; liste d'abréviations
   françaises et anglaises ; phrase coupée à la virgule au-delà de 250 caractères.
2. **Markdown retiré de ce qui est dit**, gardé dans la réponse écrite.
3. **Historique** : derniers tours répondus, 20 au plus, dans la moitié du contexte ; les tours interrompus ou
   en échec ne sont pas renvoyés au modèle ; **pas de limite d'âge** (un tour d'hier compte) jusqu'au résumé de
   l'étape 2. *(Levé par le chantier `stage2/8-memory` : silence de 2 heures, réglable.)*
4. **Un tour est enregistré à sa fin**. Une écoute annulée ou sans mots n'est pas enregistrée.
5. **Panne du modèle au milieu d'une réponse dite** : la voix se tait, l'orbe passe en erreur.
6. **Personnalité** : les deux langues sur la même page ; enregistrer le texte du majordome ou un texte vide
   revient à réinitialiser ; 4 000 caractères au plus. Textes par défaut tirés de l'essai E4 (« Tu es Jarvis,
   le majordome du film. Poli, concis, avec un humour pince-sans-rire… »).
7. **Date et heure** dans les règles fixes : heure locale au format long de la langue, sans fuseau.
8. **Demande écrite** dans la langue de l'assistant ; demande dite dans la langue de la transcription.
9. **Fenêtre** : 500 tours en mémoire (les plus anciens restent dans la base), envoi seulement en veille,
   confirmation de « Tout effacer » dans la fenêtre plutôt qu'une boîte de dialogue Windows.

## Tests

- Unitaires : `ConversationEngineTests` (demande écrite sans voix et enregistrée dans SQLite, demande dite
  phrase par phrase, message système et historique, interruption, pannes nommées, panne au milieu d'une
  réponse, rien entendu, fenêtre d'historique, tout effacer ; avec la vraie machine à états),
  `SentenceSplitterTests`, `ConversationOptionsTests`, et dans `SpeechPlayerTests` la voix coupée par une panne
  du modèle.
- Playwright (`ConversationTests`) : tour dit complet, **interruption par le mot d'éveil pendant la parole**,
  demande écrite sans voix, « Tout effacer », personnalité changée puis réinitialisée. `TranscriptionTests`
  attend maintenant la réponse.
- Captures de l'hôte Web : `stage1-13/` dans les fichiers du projet.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (**sa voix et ses oreilles**), Ollama sur le port 11435 réglé dans
« Modèles de langage » :

1. **Délai entre la fin de la phrase et le premier son**, micro et haut-parleur réels, à chaud : cible sous
   2 s (plan de l'étape 1). Trois essais ; le chiffre est dans la fenêtre (« premier son après … s ») et au
   journal. Noter les valeurs dans la mémoire.
2. **« Hey Jarvis » pendant que Jarvis parle** : la voix se coupe et Jarvis écoute. S'il n'est pas reconnu de
   façon fiable, l'interruption pendant la parole passe par le raccourci et le clic seulement, et **un nouvel
   ADR le dit** (ADR 0005).
3. Raccourci et clic sur l'orbe pendant la réflexion et pendant la parole.
4. Fenêtre « Conversation » de la zone de notification : historique gardé après un redémarrage, modèle de
   chaque tour, demande écrite répondue par écrit sans voix, « Tout effacer ».
5. Personnalité française changée (par exemple « réponds en un mot »), une question, puis « Réinitialiser ».
6. Ollama arrêté : le tour échoue avec « le serveur du modèle ne répond pas » et l'orbe en erreur ; modèle
   libéré (`ollama stop qwen3:14b`) : « le modèle se charge », puis la réponse.
