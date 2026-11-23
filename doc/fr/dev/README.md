# Développement

| Document | Contenu |
| --- | --- |
| [plans/](plans/README.md) | Document de conception et plans de série |
| [adr/](adr/README.md) | Décisions d'architecture |
| [ui/](ui/README.md) | Catalogue des designs de l'interface (orbe, éventail, plein écran) |
| [build.md](build.md) | Construire, tester, intégration continue, tests d'architecture |
| [kernel.md](kernel.md) | Le noyau : bus de messages, machine à états, niveau sonore |
| [storage.md](storage.md) | Réglages (`config.json`), base SQLite, coffre de secrets |
| [hosts.md](hosts.md) | Hôtes Windows et Web : démarrage, orbe, zone de notification, raccourci, dialogues |
| [orb.md](orb.md) | L'orbe, versions haute qualité et légère : composant, réglage de qualité, aspects, mouvement, coût, page de démonstration |
| [shared-ui.md](shared-ui.md) | Style commun, polices, traductions, réglage de langue, fenêtre des paramètres, champ de secret |
| [audio.md](audio.md) | Périphériques audio : micro, sortie, coupure du micro, tampon d'écoute |
| [wake-word.md](wake-word.md) | Mot d'éveil : modèles openWakeWord, seuil, score en direct, page des paramètres |
| [transcription.md](transcription.md) | Transcription : fin de phrase (Silero, seuil d'énergie), Whisper, page « Transcription », modèles |
| [speech.md](speech.md) | Synthèse vocale : moteurs Piper, Kokoro et dans le nuage, voix de chaque langue, page « Voix » |
| [conversation.md](conversation.md) | Conversation : le tour complet, l'interruption, la fenêtre de conversation, la personnalité |
| [confirmation.md](confirmation.md) | Demande de confirmation : condition et aspect « interaction requise » de l'orbe, fenêtre, question dite, réponse à la voix ou au clic, aucun délai, annonce pour une demande écrite, journal des permissions |
| [basic-tools.md](basic-tools.md) | Outils de base : dossiers autorisés, `list_directory`, `read_file`, `web_search`, `fetch_url`, navigateur de Jarvis (WebView2), page « Outils » |
| [home-assistant.md](home-assistant.md) | Home Assistant par MCP : classement d'un appel d'après les appareils visés, domotique de sécurité « Critique », liste « sécurité », page « Home Assistant » |
| [mcp.md](mcp.md) | Client MCP : contrat « serveur d'outils », adaptateur, connexion et nouveaux essais, outils désactivés à la découverte, empreinte d'un outil, classes, page « Serveurs MCP » |
| [tools.md](tools.md) | Outils : contrats du noyau, point de décision des permissions et son journal, boucle d'outils, heure et calcul |
| [llm.md](llm.md) | Modèles de langage : fournisseurs, adaptateurs, modèle par défaut, test de la connexion |
| [first-run.md](first-run.md) | Premier lancement : assistant de configuration, page « Modèles vocaux » (téléchargement vérifié), recherche d'Ollama |
| [conversation-memory.md](conversation-memory.md) | Mémoire de conversation : faits et résumé, extraction par le modèle local à partir des seuls mots de l'utilisateur, bloc envoyé au modèle, outils `remember_fact`, `forget_fact`, `search_memory`, page « Mémoire » |
| [hand-off.md](hand-off.md) | Passage de main : cible des questions parmi les fournisseurs cloud, outil terminal `hand_off_to_claude`, « demande à Claude » reconnu par du code, ce qui part vers la cible, qui répond, page « Passage de main » |
| [agents.md](agents.md) | Agents : un fichier Markdown par agent, agents de départ, outils vus par chaque agent, tri par l'agent Jarvis, outils `hand_to_agent` et `return_to_jarvis`, choix de l'agent dans la conversation, page « Agents » |
| [projects.md](projects.md) | Projets : un nom et un dossier, mémoire lue dans `JARVIS.md`, projet d'un tour (choisi, nommé dans la phrase ou au passage de main), agent « Projet », page « Projets » |
| [spend-cap.md](spend-cap.md) | Plafond de dépense : prix par fournisseur cloud, coût compté à chaque réponse de la cible, compteur du mois, avertissement à 80 %, plafond qui retire l'outil et garde Jarvis en local, page « Dépense » |
| [tasks-and-sessions.md](tasks-and-sessions.md) | Tâches et sessions Claude Code : forme d'une tâche et ses neuf statuts (Relais), clés de projet et de sujet, tableau des tâches, lecture du registre des sessions sans jamais ouvrir un fichier de clés, page « Sessions Claude » |
| [hook-server.md](hook-server.md) | Serveur local et hooks Claude Code : serveur sur `127.0.0.1` seulement, aucune route sans jeton, les cinq routes `/hook/…` qui font bouger les tâches, installation et retrait des hooks dans les réglages Claude Code de l'utilisateur sur son clic, avec aperçu et copie datée |
| [claude-code-launch.md](claude-code-launch.md) | Lancer Claude Code (première partie du chantier 12) : demandes d'une session au point de décision, outil `approve` sur `/mcp`, ligne de commande de `claude -p`, fichiers de Jarvis sans secret, événements du flux |
| [claude-code-tasks.md](claude-code-tasks.md) | Tâches Claude Code (seconde partie du chantier 12) : « Demande à Claude Code de… », choix du dossier autorisé, tâche créée avant le lancement, classement des demandes d'une session, arrêt d'une session qui écrit sans demander, fins d'une session, coût et « Claude Code est payé par abonnement », page « Tâches Claude Code » |
| [task-fan.md](task-fan.md) | Éventail des tâches (chantier 14) : clic droit sur l'orbe, double-clic réservé à la vue des projets, panneaux par tâche, sous-tâches, panneau ambre et boutons Autoriser et Refuser, disposition en arc, animation, fenêtre de l'éventail gardée cachée dans l'hôte Windows, badge ambre et compte des tâches de l'orbe |
| [session-announcements.md](session-announcements.md) | Annonces des sessions Claude Code (chantier 13) : le tour d'annonce (ADR 0024), ce qui est une nouvelle, dernier message d'une session raccourci ou coupé, réglage par dossier, heures et période de silence, outils `start_quiet_period` et `get_claude_sessions`, page « Annonces » |
| [faults.md](faults.md) | Pannes : causes nommées, action corrective, lien vers le bon réglage, fenêtre « Problème de Jarvis » |
| [updates.md](updates.md) | Mises à jour : vérification, téléchargement, programme de mise à jour, publication |
| [pitfalls.md](pitfalls.md) | Pièges rencontrés et leur solution |
| [spikes.md](spikes.md) | Synthèse des essais techniques (E1 à E15) |
