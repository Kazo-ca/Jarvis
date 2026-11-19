# Kazo.Jarvis : document de conception

2026-11-18 · Benoit Robin

## But et périmètre

Kazo.Jarvis est un assistant vocal pour Windows, écrit en C# sur .NET 10, qui fonctionne sans Python. Il prend la forme d'un orbe animé inspiré du film, répond d'abord avec un modèle local, et passe la main à Claude quand la tâche le dépasse. Une application Android et un poste téléphonique 3CX permettent de lui parler ailleurs que devant le PC.

La référence fonctionnelle est OpenJarvis. On en reprend le cœur utile et on y ajoute le mot d'éveil, qu'il n'a pas.

| Repris d'OpenJarvis | Ajouté | Exclu de la première version |
| --- | --- | --- |
| Boucle vocale (reconnaissance, synthèse) | Mot d'éveil « hey jarvis » | Les 35 canaux de messagerie |
| Moteurs LLM : Ollama, llama.cpp, API cloud | Orbe animé à sept états | Bancs d'essai et évaluations |
| Outils : fichiers, web, calcul, client MCP | Suivi des sessions Claude | Apprentissage et optimisation |
| Mémoire de conversation | Application mobile satellite | Minage |
| Planificateur de tâches | Téléphonie 3CX | Langues autres que le français et l'anglais (v2) |
| Connecteurs Gmail et Agenda, résumé du matin | Déclencheurs par événement, webhook et scripts | Connecteur Microsoft (interface prête, pas livré) |
| Agent Claude Code | Plafond de dépense cloud | iOS |

Le code vit dans un nouveau dépôt, `Kazo-ca/jarvis`. Le fork `Kazo-ca/OpenJarvis` reste intact et sert de référence de comportement en lecture seule.

## Décisions prises

Benoit a répondu à 40 questions le 18 novembre 2026. Les numéros renvoient aux questions de la discussion, et chaque ligne deviendra un ADR ou une partie d'ADR.

| N° | Sujet | Décision |
| --- | --- | --- |
| 1 | Portée | Nouvelle application C# autonome, cœur utile seulement, aucun Python à l'exécution |
| 2 | Référence | OpenJarvis, avec un mot d'éveil ajouté |
| 3, 34 | Dépôt et nom | `Kazo.Jarvis` dans `Kazo-ca/jarvis` |
| 4 | Forme sur Windows | Orbe flottant transparent, toujours au premier plan, icône de notification, fenêtres séparées pour les paramètres et la conversation |
| 5 | États de l'orbe | Sept états (voir la section sur l'orbe) |
| 6 | Animation | Canvas ou WebGL dans la bibliothèque Razor partagée, réactive au volume |
| 7, 8 | Mobile | Satellite vocal avec mode texte et notifications, MAUI Blazor Hybrid, Android d'abord, réseau local et VPN |
| 9, 10 | Téléphonie | 3CX gratuit à installer, Jarvis s'y enregistre comme poste SIP |
| 11 | Appels sortants | Seulement pour une tâche bloquée sur une décision ou une alerte urgente, avec heures de silence |
| 12 | Appelants | Postes internes et numéros autorisés, NIP vocal pour les actions sensibles |
| 13 | Blocs repris | Voix, moteurs, outils, mémoire, planificateur, Gmail, Agenda, résumé du matin |
| 14 | Home Assistant | Par le client MCP générique |
| 15 | Déclenchement | Mot d'éveil, raccourci clavier global, clic sur l'orbe |
| 16 | Reconnaissance | Whisper local via Whisper.net, modèle et appareil réglables |
| 17 | Synthèse | Kokoro et Piper en local, voix cloud optionnelles, page de test |
| 18 | Interruption | Par mot d'éveil, raccourci ou clic seulement |
| 19, 26 | Langue | Français et anglais obligatoires, choix au premier lancement, modifiable ensuite |
| 20 | Passage de main | Le modèle local répond d'abord et appelle un outil « passer la main », forçage vocal possible, plafond mensuel |
| 21 | Cible | Claude Code pour les fichiers, le code et les tâches à étapes, API pour les questions difficiles |
| 22, 27 | Suivi des sessions | Toutes les sessions Claude Code du PC et les sessions de code de Claude Desktop |
| 23 | Permissions | Dossiers autorisés, lecture seule par défaut, confirmation avant écriture ou commande |
| 24, 29 | Mode autonome | Tâches planifiées, tâches déléguées et six déclencheurs : Home Assistant, courriel, fin de session Claude, fin de build GitHub Actions, webhook, script |
| 25 | Notifications mobiles | Connexion maintenue par un service Android, sans service externe |
| 28 | Comptes | Google d'abord, interface prête pour Microsoft |
| 30 | Écoute sur téléphone | Appui pour parler par défaut, écoute permanente en option |
| 31 | Plusieurs appareils | Seul l'appareil qui a capté la demande répond |
| 32 | Personnalité | Majordome du film, poli, concis, humour sec, vouvoiement et « Monsieur », texte modifiable |
| 33 | Ordre de livraison | Cinq étapes (voir plus bas) |
| 35 | Forme du plan | Ce document, dans le style de `doc/Features.md` de Kazo Bar |
| 36 | Architecture | Hôtes de Kazo Bar, code en tranches verticales, petit noyau partagé |
| 37 | Méthode | Celle de Kazo Os, au complet, doc en français puis en anglais |
| 38 | Webhook | Entrant, protégé par jeton, réseau local et VPN |
| 39 | Scripts | Déclencheur ou action, en PowerShell, Python ou cmd |
| 40 | Scripts générés | Approbation obligatoire avant la première exécution et après chaque modification |

## Architecture

On garde de Kazo Bar les hôtes et les services natifs derrière des interfaces. Tout le reste est rangé par fonctionnalité, selon l'architecture en tranches verticales (VSA).

*(diagramme d'architecture : voir le document original)*

Les hôtes ne contiennent que le démarrage et le code propre à leur plateforme. Le téléphone affiche les mêmes tranches que le PC, mais leurs demandes voyagent jusqu'au serveur intégré à l'hôte Windows, où se fait tout le traitement.

| Projet | Contenu | Repris de Kazo Bar |
| --- | --- | --- |
| `Kazo.Jarvis.Kernel` | Machine à états de l'assistant, messages entre tranches, contrats des services natifs, configuration, stockage, coffre | `Core`, réduit au strict commun |
| `Kazo.Jarvis.Features` | Bibliothèque Razor qui contient toutes les tranches | Remplace `UI`, `Plugins` et la logique d'`Infrastructure` |
| `Kazo.Jarvis.Adapters` | Mises en œuvre des technologies externes, chargées par l'hôte Windows seulement | Les services réels d'`Infrastructure` |
| `Kazo.Jarvis.Windows` | Hôte WPF avec `BlazorWebView`, fenêtre transparente, zone de notification, audio, raccourci global, serveur intégré | `Windows` |
| `Kazo.Jarvis.Web` | Hôte de test avec services simulés, cible des tests Playwright | `Web` |
| `Kazo.Jarvis.Mobile` | Hôte MAUI Blazor Hybrid pour Android | L'ancien hôte `Maui`, repris pour Android seulement |
| `Kazo.Jarvis.Updater` | Programme de mise à jour | `Updater` et `Updates` |

Une tranche est un dossier qui contient tout ce qu'il faut pour une fonctionnalité :

```
Features/
  Voice/
    WakeWord/
      WakeWordSettings.razor        (+ .razor.css, .razor.js)
      DetectWakeWord.cs             demande, traitement, résultat
      WakeWordOptions.cs            modèle de configuration de la tranche
      WakeWordTests.cs              dans le projet de tests, même chemin
    TestVoice/
      TestVoicePage.razor
      PlaySample.cs
  Handoff/
    EscalateToClaude/
    SpendCap/
```

Règles de dépendance, à fixer dans l'ADR 0002 :

- Une tranche ne référence jamais une autre tranche. Elles se parlent par des messages définis dans le noyau.
- Une tranche dépend du noyau et de contrats, jamais d'un adaptateur concret.
- On préfère dupliquer un peu de code entre deux tranches plutôt que créer trop tôt une abstraction partagée. Ce qui sert à trois tranches ou plus monte dans le noyau.
- Le noyau ne contient aucune logique métier, aucun composant d'interface et aucune dépendance native.
- Le CSS et le JavaScript d'un composant vivent dans des fichiers à côté de lui, jamais dans le balisage, comme dans Kazo Os.

## L'orbe et ses sept états

L'orbe a sept états, et chacun doit se reconnaître du coin de l'œil sans lire de texte. La lumière réagit au volume réel : celui du micro en écoute, celui de la voix synthétisée en parole.

| État | Quand | Aspect |
| --- | --- | --- |
| Veille | Attend le mot d'éveil | Cyan, immobile, faible luminosité |
| Écoute | Mot d'éveil capté, raccourci ou clic | Cyan, rotation douce ; la voix fait monter la lumière |
| Réflexion | Transcription puis attente du modèle | Violet, rotation rapide |
| Parole | Synthèse en cours | Cyan ; la lumière suit la voix, ou les anneaux pulsent au hasard |
| Tâche longue | Claude ou une tâche longue travaille en arrière-plan | Vert, anneau de progression |
| Micro coupé | Coupure volontaire | Gris, immobile, mention « micro coupé » |
| Erreur | Micro, modèle ou réseau injoignable | Rouge, un éclair puis immobile, cause affichée au survol |

Règles communes :

- L'anneau de progression d'une tâche longue reste visible par-dessus les autres états. Jarvis peut écouter ou parler pendant que Claude travaille.
- Les couleurs et le dessin sont fixés par l'[ADR 0011](../adr/0011-orb-colors-and-drawing.md), à partir du [catalogue des designs](../ui/catalog.md). L'orbe existe en deux versions : haute qualité et légère.
- Une option « mouvement réduit » retire les rotations et les pulsations.
- Le même composant sert sur Windows, dans l'hôte Web de test et sur mobile.

## Étape 1 : orbe, voix, modèle local et paramètres

À la fin de cette étape, on dit « hey jarvis » devant le PC, on pose une question, et le modèle local répond à voix haute en français ou en anglais. Rien ne sort de la machine.

| Récit | En tant qu'utilisateur, je veux | Critère d'acceptation |
| --- | --- | --- |
| US1.1 | voir un orbe flottant, transparent et toujours au premier plan | Les clics autour de l'orbe atteignent l'application dessous. L'orbe se déplace et sa position est mémorisée. |
| US1.2 | une icône dans la zone de notification | Clic gauche : afficher ou cacher l'orbe. Clic droit : Paramètres, Conversation, Couper le micro, Quitter. |
| US1.3 | que l'orbe montre les sept états | Chaque état se déclenche depuis une page de démonstration de l'hôte Web, sans micro ni modèle. |
| US1.4 | choisir ma langue au premier lancement | L'assistant de configuration propose français ou anglais, puis le micro, la sortie audio et le modèle. Le choix s'applique à l'interface, à la reconnaissance et à la voix. |
| US1.5 | réveiller Jarvis par « hey jarvis » | Détection locale en continu. La sensibilité se règle. Un indicateur montre le score de détection pour faciliter le réglage. |
| US1.6 | l'activer aussi par un raccourci global ou un clic | Le raccourci est configurable et fonctionne quand une autre application a le focus. |
| US1.7 | que ma phrase soit transcrite localement | Fin de phrase détectée par le silence. Modèle Whisper et appareil (processeur ou carte graphique) réglables. La transcription s'affiche dans la conversation. |
| US1.8 | configurer un ou plusieurs fournisseurs LLM | Types : Ollama, llama.cpp ou tout serveur compatible OpenAI, Claude, ChatGPT. Adresse, port et modèle libres (Ollama sur 11435 doit fonctionner). Bouton « Tester la connexion ». |
| US1.9 | désigner le modèle local par défaut | La réponse arrive en flux et la synthèse commence dès la première phrase complète. |
| US1.10 | entendre la réponse avec une voix choisie | Kokoro et Piper en local. Voix cloud listées seulement si une clé est fournie. |
| US1.11 | tester les voix | La page liste les voix par moteur et par langue, joue une phrase d'essai modifiable, règle la vitesse et définit la voix par défaut de chaque langue. |
| US1.12 | interrompre Jarvis | Le mot d'éveil, le raccourci ou un clic coupe la parole et repasse en écoute. |
| US1.13 | couper le micro | L'état « micro coupé » est visible et aucun son n'est capté. |
| US1.14 | relire la conversation | Fenêtre séparée avec l'historique, une zone de saisie texte, et le modèle qui a répondu à chaque tour. |
| US1.15 | régler la personnalité | Le message système par défaut est le majordome, en français et en anglais. Il est modifiable et réinitialisable. |
| US1.16 | que mes clés soient protégées | Les clés d'API vont dans le Gestionnaire d'identification Windows ou un fichier chiffré, jamais dans le fichier de configuration. |
| US1.17 | comprendre une panne | L'état « erreur » nomme la cause (micro absent, modèle injoignable, voix manquante) et propose l'action corrective. |
| US1.18 | recevoir les mises à jour | Même mécanisme que Kazo Bar : vérification des versions GitHub et programme de mise à jour séparé. |

Hors de cette étape : outils, MCP, Claude, mobile, téléphonie.

## Étape 2 : outils, MCP, passage de main et sessions Claude

À la fin de cette étape, Jarvis agit : il contrôle la maison par Home Assistant, confie les tâches lourdes à Claude et annonce où en sont les sessions Claude du PC.

| Récit | En tant qu'utilisateur, je veux | Critère d'acceptation |
| --- | --- | --- |
| US2.1 | que le modèle local utilise des outils | Outils de base : lecture de fichiers, recherche web, requête HTTP, calcul, date et heure. Chaque appel d'outil apparaît dans la conversation. |
| US2.2 | brancher des serveurs MCP | Ajout par adresse ou par commande locale, liste des outils exposés, activation outil par outil. |
| US2.3 | contrôler Home Assistant à la voix | Le serveur MCP de Home Assistant se configure comme les autres. « Allume le salon » fonctionne en français et en anglais. |
| US2.4 | que Jarvis se souvienne | Mémoire de conversation persistante, avec résumé automatique des anciens échanges. Une page permet de voir et d'effacer ce qu'il retient. |
| US2.5 | que le modèle local passe la main seul | Il dispose d'un outil « passer la main » avec un motif. L'orbe et la conversation indiquent qui répond. |
| US2.6 | forcer le passage à la voix | « Demande à Claude » ou « ask Claude » envoie la demande directement à la cible configurée. |
| US2.7 | choisir les cibles | Une cible pour les tâches avec fichiers ou étapes (Claude Code) et une pour les questions difficiles (API Claude ou ChatGPT). |
| US2.8 | plafonner la dépense cloud | Montant mensuel réglable. Le compteur est visible. Une fois le plafond atteint, Jarvis le dit et reste en local. |
| US2.9 | lancer une tâche Claude Code à la voix | Jarvis choisit ou demande le dossier parmi la liste autorisée, lance la session et passe à l'état « tâche déléguée ». |
| US2.10 | garder le contrôle des écritures | Lecture seule par défaut. Toute écriture ou commande demande une confirmation, à la voix ou sur l'orbe, avec un résumé de l'action. |
| US2.11 | voir toutes les sessions Claude Code du PC | Liste des sessions avec dossier, état (en cours, en attente, terminée) et dernière activité, y compris celles lancées à la main. |
| US2.12 | être prévenu à la voix | Annonce quand une session se termine ou attend une permission. Les annonces se règlent par dossier et respectent les heures de silence. |
| US2.13 | demander un résumé | « Où en est Claude sur Kazo Bar? » produit un résumé fait par le modèle local à partir de la session. |
| US2.14 | suivre aussi Claude Desktop | Ses sessions de code apparaissent dans la même liste. Dépend de l'essai technique E3. |
| US2.15 | répondre à une demande de permission | Depuis l'orbe ou à la voix, pour les sessions lancées par Jarvis. Pour les autres, Jarvis annonce seulement. |

Hors de cette étape : tâches planifiées, déclencheurs, courriel.

## Étape 3 : mode autonome, déclencheurs et courriel

À la fin de cette étape, Jarvis travaille sans qu'on lui parle : à heure fixe, sur événement, ou sur une tâche longue qu'on lui a confiée avant de partir.

Une tâche autonome est décrite en langage naturel. Elle a un déclencheur, une consigne, une cible (modèle local ou Claude) et une règle de compte rendu.

| Récit | En tant qu'utilisateur, je veux | Critère d'acceptation |
| --- | --- | --- |
| US3.1 | créer une tâche planifiée | À la voix ou dans une page dédiée. Horaire simple (chaque jour à 7 h) ou expression avancée. Les tâches manquées pendant que le PC était éteint sont signalées, pas rejouées en rafale. |
| US3.2 | confier une tâche longue et partir | La tâche continue en arrière-plan, son état est visible, et elle peut être annulée. |
| US3.3 | déclencher sur un événement Home Assistant | Choix d'une entité et d'une condition. Dépend de l'essai technique E9. |
| US3.4 | déclencher sur un courriel | Règle sur l'expéditeur, le sujet ou le libellé. |
| US3.5 | déclencher à la fin d'une session Claude | Par dossier ou pour toutes les sessions. |
| US3.6 | déclencher à la fin d'un build GitHub Actions | Par dépôt, avec filtre sur le résultat (succès, échec). |
| US3.7 | déclencher par webhook entrant | Chaque webhook a sa propre adresse et son jeton. Le corps reçu est transmis à la tâche comme donnée. Écoute sur le réseau local et le VPN seulement. |
| US3.8 | utiliser un script comme déclencheur | Jarvis l'exécute à intervalle réglé. Le code de sortie décide s'il y a un événement, et la sortie standard est transmise à la tâche. |
| US3.9 | utiliser un script comme action | PowerShell, Python ou cmd, avec la sortie affichée en direct et un bouton d'arrêt. |
| US3.10 | faire écrire un script par Jarvis ou Claude | Le script généré reste « en attente » jusqu'à mon approbation, à l'écran ou à la voix après lecture d'un résumé. Toute modification remet le script en attente. Mes propres scripts sont approuvés d'office. |
| US3.11 | connecter mon compte Google | Connexion OAuth, jetons dans le coffre. Gmail en lecture et Agenda en lecture pour commencer. |
| US3.12 | recevoir le résumé du matin | Tâche planifiée fournie d'office : courriels importants, agenda du jour, météo, lus à voix haute. Le contenu se règle. |
| US3.13 | être prévenu selon l'urgence | Chaque tâche a un niveau : silencieux, notification, annonce vocale, appel (étape 5). Les heures de silence s'appliquent à tout sauf au niveau urgent. |
| US3.14 | consulter le journal | Chaque exécution garde son déclencheur, sa durée, son résultat, son coût cloud et les actions faites. |
| US3.15 | que les tâches autonomes restent dans leurs limites | Une tâche autonome ne peut jamais approuver elle-même une écriture ou un script. Si elle en a besoin, elle se met en attente et me prévient. |

L'envoi de courriels et la modification de l'agenda sont exclus de cette étape.

## Étape 4 : application mobile Android

À la fin de cette étape, le téléphone est un satellite de l'installation du PC : on y parle, on y écrit et on y reçoit les notifications. Tout le traitement reste sur le PC.

| Récit | En tant qu'utilisateur, je veux | Critère d'acceptation |
| --- | --- | --- |
| US4.1 | activer le serveur du PC | Un réglage ouvre l'écoute sur le réseau local. Il est fermé par défaut. |
| US4.2 | jumeler mon téléphone | Le PC affiche un code QR. Le téléphone le lit et reçoit un jeton propre à cet appareil. |
| US4.3 | gérer les appareils jumelés | Liste sur le PC avec la dernière connexion, et révocation en un clic. |
| US4.4 | voir l'orbe sur le téléphone | Même composant et mêmes états que sur Windows, synchronisés en temps réel. |
| US4.5 | parler à Jarvis depuis le téléphone | Appui sur l'orbe, l'audio part vers le PC, la réponse revient et joue sur le téléphone. |
| US4.6 | écrire au lieu de parler | Mode texte avec la même conversation que sur le PC. |
| US4.7 | recevoir les notifications | Par une connexion maintenue par un service Android. Une notification permanente indique que le service tourne. |
| US4.8 | répondre depuis une notification | Approuver ou refuser une demande de permission, ou ouvrir la conversation. |
| US4.9 | activer l'écoute permanente | Option désactivée par défaut, avec un choix « seulement en charge ». Dépend de l'essai technique E10. |
| US4.10 | que seul le bon appareil réponde | L'appareil qui a capté la demande parle. Les autres montrent l'état en silence. |
| US4.11 | l'utiliser hors de la maison | Par un VPN comme Tailscale. L'application accepte une adresse de VPN et n'expose rien sur Internet. |
| US4.12 | savoir que le PC est injoignable | État « erreur » clair, nouvelle tentative automatique, aucune perte du texte en cours de saisie. |

Les paramètres complets restent sur le PC. Le téléphone ne règle que ce qui le concerne : serveur, micro, écoute permanente, notifications.

## Étape 5 : téléphonie 3CX

> **Abandonnée le 18 novembre 2026** par Benoit : pas de 3CX, on parle à Jarvis seulement par l'application MAUI Blazor Hybrid. Le texte ci-dessous est gardé pour mémoire ; l'essai E1 n'est plus nécessaire.

À la fin de cette étape, on compose un numéro de poste pour converser avec Jarvis, et Jarvis appelle quand une tâche autonome a besoin d'une décision.

Cette étape dépend de l'essai technique E1. L'édition gratuite de 3CX annoncée le 8 janvier 2026 est auto-hébergée et limitée à 4 appels simultanés, ce qui suffit. Elle est présentée pour les applications de 3CX, et rien d'officiel ne confirme qu'un client SIP générique peut s'y enregistrer ([](https://www.3cx.com/blog/news/free-edition/)).

| Récit | En tant qu'utilisateur, je veux | Critère d'acceptation |
| --- | --- | --- |
| US5.1 | enregistrer Jarvis comme poste | Serveur, numéro de poste et mot de passe dans les paramètres. L'état de l'enregistrement est visible. |
| US5.2 | appeler Jarvis | Il décroche, salue, et la conversation se déroule comme devant le PC, sans mot d'éveil. |
| US5.3 | une conversation fluide au téléphone | Détection de fin de phrase adaptée à l'audio téléphonique. Une touche du clavier coupe la parole de Jarvis. |
| US5.4 | limiter qui peut appeler | Seuls les postes internes et les numéros autorisés sont acceptés. Les autres appels sont refusés et journalisés. |
| US5.5 | protéger les actions sensibles | Un NIP, dit ou tapé au clavier, est demandé avant la domotique de sécurité, les écritures et l'exécution de code. Trois échecs mettent fin à l'appel. |
| US5.6 | que Jarvis m'appelle | Seulement pour une tâche bloquée sur une décision ou une alerte de niveau urgent. Il appelle le poste ou le numéro configuré, expose la situation et attend la réponse. |
| US5.7 | des heures de silence | Aucun appel pendant la plage réglée, sauf niveau urgent. La demande devient une notification. |
| US5.8 | un repli si je ne réponds pas | Nombre de tentatives réglable, puis notification. La tâche reste en attente. |
| US5.9 | voir l'appel sur l'orbe | L'orbe du PC et du téléphone montre qu'un appel est en cours. La transcription s'ajoute à la conversation. |
| US5.10 | consulter le journal des appels | Date, sens, appelant, durée, actions demandées. |

Si l'essai E1 échoue, le repli est un pont SIP ou une autre centrale téléphonique. Le choix sera posé à ce moment-là.

## Sécurité et vie privée

Jarvis entend en permanence, contrôle la maison et peut lancer du code. Ces neuf règles s'appliquent à toutes les étapes.

1. **** Le mot d'éveil, la reconnaissance et la synthèse tournent sur le PC. L'audio ne quitte la machine que si une voix ou un modèle cloud est choisi explicitement.
2. **** Le tampon d'écoute reste en mémoire et n'est jamais écrit sur disque.
3. **** Clés d'API, jetons OAuth, mot de passe SIP et jetons des appareils vont dans le coffre, comme dans Kazo Bar.
4. **** Un courriel, le corps d'un webhook, une page web ou la sortie d'un script ne peut pas donner d'instructions à Jarvis. C'est la principale défense contre l'injection d'instructions.
5. **** Écriture de fichiers, commandes, scripts générés et domotique de sécurité demandent un accord, et une tâche autonome ne peut pas se l'accorder.
6. **** Claude Code et les outils de fichiers ne voient que les dossiers de la liste.
7. **** Le serveur pour le mobile et les webhooks n'écoute que si on l'active, sur le réseau local et le VPN, avec un jeton par appareil ou par webhook.
8. **** Le plafond mensuel bloque le cloud une fois atteint.
9. **** Appels d'outils, passages de main, exécutions autonomes et appels téléphoniques sont consultables et effaçables.

Une confirmation vocale peut être captée par erreur ou prononcée par quelqu'un d'autre. Les actions les plus risquées exigent donc un clic sur l'orbe, le téléphone jumelé ou le NIP.

## Essais techniques à faire avant de coder

Dix points reposent sur des suppositions que je n'ai pas pu vérifier. Chacun se règle par un petit essai jetable, rangé dans `spikes/` comme dans Kazo Bar, avec un résultat écrit.

| Essai | Bloque | Ce qu'il doit prouver |
| --- | --- | --- |
| E1 | Étape 5 | Un client SIP en C# (SIPSorcery) s'enregistre comme poste sur le 3CX gratuit, reçoit un appel, en émet un et échange l'audio dans les deux sens. |
| E2 | Étape 2 | On peut lister les sessions Claude Code du PC et connaître leur état (en cours, en attente de permission, terminée) à partir de leurs fichiers et des hooks, sans les perturber. |
| E3 | US2.14 | Les sessions de code de Claude Desktop sont lisibles de la même façon. Sinon, on se limite à « occupé ou en attente ». |
| E4 | Étape 1 | La chaîne vocale tient en C# : modèle ONNX « hey jarvis », Whisper.net, Kokoro et Piper. Mesurer le délai entre la fin de la phrase et le premier son, avec une cible sous 2 secondes. |
| E5 | Étape 1 | Whisper et le modèle local cohabitent sur la RTX 3060 de 12 Go, ou Whisper passe sur le processeur avec un délai acceptable. |
| E6 | Étape 1 | L'animation Canvas ou WebGL tourne dans la fenêtre WPF transparente sans coût processeur notable en veille. Cible : moins de 2 % d'un cœur. |
| E7 | Étape 2 | Le modèle local (qwen2.5-coder 14B) appelle les outils de façon fiable en français, dont « passer la main ». Sinon, choisir un autre modèle local par défaut. |
| E8 | Étape 2 | La meilleure façon de piloter Claude Code depuis C# : ligne de commande en mode non interactif, ou pont vers le Claude Agent SDK comme le fait OpenJarvis. |
| E9 | US3.3 | Les événements Home Assistant arrivent par MCP. Sinon, passer par son API d'événements pour les déclencheurs seulement. |
| E10 | Étape 4 | Transport audio entre téléphone et PC (WebRTC ou WebSocket) et coût en batterie de l'écoute permanente sur Android. |

E4, E5 et E6 passent en premier, parce que l'étape 1 en dépend. E1 peut se faire tôt en parallèle : s'il échoue, mieux vaut le savoir avant d'installer 3CX pour de bon.

## Méthode de travail et premiers ADR

Le dépôt `Kazo-ca/jarvis` reprend la méthode de Kazo Os au complet. Voici ce qui doit exister avant la première ligne de code.

| Pièce | Rôle |
| --- | --- |
| `AGENTS.md`, importé par `CLAUDE.md` | Règles pour tout agent : français avec Benoit, questions à choix avec recommandation, VSA, fichiers CSS et JS à côté de leur composant, noms de fichiers en anglais |
| `doc/fr/` et `doc/en/` en miroir | Français d'abord, anglais traduit dans le même changement, un `README.md` d'index par dossier |
| `doc/fr/dev/adr/` | Un ADR numéroté par décision, écrit et fusionné avant le code qu'il décrit. Un ADR accepté ne se modifie pas, un nouveau le remplace. |
| `doc/fr/memory/` | Point de reprise : état des séries, prochain numéro d'ADR libre, décisions prises mais pas construites, échecs de tests connus |
| `doc/fr/features.md` | Vue d'ensemble du produit, mise à jour par chaque PR vers `main` |
| `doc/fr/dev/plans/` | Ce document, puis un plan par série |
| `doc/fr/dev/pitfalls.md` | Pièges rencontrés et leur solution |
| Tickets `TODO` | Tout ce qui est « à ajouter à la liste », avec une section « Dépendances » |
| Branches | `<série>/base` depuis `main`, une branche `<série>/<n>-<sujet>` par chantier. Seul Benoit fusionne dans `main`. |
| Tests | Tests unitaires par tranche, tests Playwright contre l'hôte Web. Aucun test désactivé pour obtenir le vert. |

Premiers ADR à écrire, dans cet ordre :

1. [ADR 0001](../adr/0001-target-dotnet-10-and-projects.md) : cible .NET 10 et liste des projets.
2. [ADR 0002](../adr/0002-vertical-slices-kernel-and-dependency-rules.md) : tranches verticales, noyau partagé et règles de dépendance.
3. [ADR 0003](../adr/0003-hosts-and-native-services.md) : hôtes (Windows WPF, Web de test, Android) et services natifs.
4. [ADR 0004](../adr/0004-configuration-storage-and-secret-vault.md) : configuration, stockage et coffre de secrets.
5. [ADR 0005](../adr/0005-assistant-state-machine-and-orb-contract.md) : machine à états de l'assistant et contrat de l'orbe.
6. [ADR 0006](../adr/0006-voice-chain.md) : chaîne vocale (mot d'éveil, reconnaissance, synthèse), après E4 et E5.
7. [ADR 0007](../adr/0007-llm-providers-and-tool-format.md) : fournisseurs LLM et format des outils.
8. [ADR 0008](../adr/0008-languages-and-localization.md) : langues et localisation.
9. [ADR 0009](../adr/0009-permission-and-confirmation-model.md) : modèle de permissions et de confirmation.

Les ADR des étapes 2 à 5 s'écrivent au début de chaque série, une fois les essais techniques correspondants terminés.

## Hypothèses à confirmer

J'ai pris neuf décisions de détail sans te les demander. Coche celles qui te conviennent, et commente celles que tu veux changer.

- [x] Un seul utilisateur par installation. Pas de comptes ni de reconnaissance de la voix de chaque personne.
- [x] L'historique des conversations et la mémoire sont stockés en local dans `%USERPROFILE%\.kazo\jarvis`, sans limite de durée, avec un bouton pour tout effacer. (Emplacement corrigé par Benoit le 19 novembre 2026 : le dossier de l'utilisateur, pas `%APPDATA%`. Voir l'[ADR 0004](../adr/0004-configuration-storage-and-secret-vault.md).)
- [x] Jarvis démarre avec Windows par défaut, et l'option se désactive dans les paramètres.
- [ ] Il n'y a pas de module Jarvis dans Kazo Bar pour l'instant (tu as choisi 4A, pas 4C).
- [ ] Gmail et Agenda sont en lecture seule dans la première version. Envoyer un courriel ou créer un événement viendra plus tard.
- [ ] Le téléphone ne règle pas les paramètres du PC. Il ne règle que les siens.
- [x] Le dépôt est public et sous la même licence que Kazo Bar.
- [ ] `jarvis.py` continue de tourner tel quel jusqu'à la fin de l'étape 2, puis Kazo.Jarvis le remplace.
- [x] Le mot d'éveil reste « hey jarvis » dans les deux langues. Un mot d'éveil personnalisé demanderait d'entraîner un modèle et attend la v2.

Prochaine action : créer le dépôt `Kazo-ca/jarvis`, y déposer ce plan avec `AGENTS.md`, puis lancer les essais E4, E5 et E6.
