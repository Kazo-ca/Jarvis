# Projets : un nom, un dossier, une mémoire, et l'agent « Projet »

Chantiers `stage2/19-projects`, `stage2/19-project-sessions` et `stage2/21-project-memory-writing`. Décision : [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 6, 10,
11 et 13 (point 2). Les agents sont décrits dans [agents.md](agents.md), la mémoire de conversation dans
[conversation-memory.md](conversation-memory.md).

Le premier chantier construit les projets, leur page, leur mémoire lue dans le dossier, l'agent « Projet » et le
projet d'un tour. Le second ajoute la demande d'assigner à un projet un dossier qui n'en a pas (quand une
session Claude Code y travaille) et le lancement d'une session Claude Code dans le dossier du projet.
Le troisième ajoute l'écriture de la mémoire d'un projet par Jarvis, sur demande et après confirmation.
**Reste à construire :** la vue des projets (ADR 0026).

## Ce qui est construit

```
page « Projets » ──► ProjectCatalog (tranche Projects/Catalog) ──► réglages : section Projects.List
                          │   lit JARVIS.md par ILocalFiles (noyau)
                          ▼
Conversation/Dialogue ─► IProjectCatalog (noyau) : Projects · Find(nom) · NamedIn(demande) · RecallAsync(projet)
   │
   ├─ projet du tour : choisi dans la fenêtre (demande écrite), sinon nommé dans la phrase,
   │                   sinon nommé par l'agent Jarvis dans hand_to_agent(agent, project)
   └─ l'agent qui répond (sauf l'agent Jarvis) reçoit le bloc « [Projet] » : nom, dossier, mémoire
```

Les tranches `Projects/Catalog`, `Agents/Catalog` et `Conversation/Dialogue` ne se connaissent pas : le contrat
`IProjectCatalog` du noyau les relie. Sans cette tranche, aucun tour ne porte sur un projet.

## Un projet (`ProjectCatalog`)

- **Un nom et un dossier local**, gardés dans les réglages (`Projects.List`). 50 projets au plus, un nom de
  60 caractères au plus, avec au moins une lettre ou un chiffre.
- **Deux projets ne portent pas le même nom**, sans tenir compte de la casse ni des accents.
- L'identité vient du premier nom (`Kazo OS` donne `kazo-os`) et ne change plus.
- Retirer un projet laisse son dossier et son fichier de mémoire comme ils sont.
- Créer un projet n'écrit aucun agent (section 13, point 2) et n'autorise aucun dossier (section 6).

## La mémoire d'un projet

**Le fichier `JARVIS.md` à la racine du dossier du projet.** C'est du Markdown lisible, que l'utilisateur ou une
session Claude Code écrit ; Jarvis le **lit** à chaque tour qui porte sur le projet, sans rien garder entre deux
lectures. Jarvis y **ajoute une ligne** quand l'utilisateur le demande (plus bas).

- Lu par `ILocalFiles`, sans passer par les dossiers autorisés : c'est l'utilisateur qui a donné ce dossier au
  projet, et seul ce fichier est lu. Les outils `list_directory` et `read_file` gardent leur règle : l'agent ne
  lit le reste du dossier que si le dossier est autorisé.
- Sa part : un seizième du contexte (`ProjectCatalog.Budget`). Un fichier plus long est coupé, avec « … ».
- Un crochet du fichier devient une parenthèse : il ne peut pas fermer le bloc.
- Jamais envoyé au cloud par ce chemin : le bloc va au modèle local, avec la mémoire.

## Écrire dans la mémoire d'un projet (`remember_project_fact`)

« Retiens pour le projet Kazo OS que la cible est ARM64. » Décision de Benoit du 2026-11-23 : Jarvis peut écrire
dans `JARVIS.md`, sur demande, avec confirmation.

- L'outil `remember_project_fact(project, text)` appartient à l'agent « Projet ». Sa classe est **« Sensible »** :
  Jarvis demande une confirmation à chaque fois (ADR 0023), et la demande dit le projet et la phrase.
- Il **ajoute une ligne** `- <fait>` à la fin du fichier, sans rien changer de ce qui s'y trouve. Sans fichier, il
  le crée avec le nom du projet en titre. Une ligne déjà présente n'est pas écrite deux fois.
- Un fait tient en une ligne de 300 caractères au plus. **Un texte qui ressemble à un mot de passe, une clé ou un
  numéro de carte n'est jamais écrit** (`SecretShapes`, passé au noyau) : une session lit ce fichier, et un dépôt
  peut le garder.
- Refusé dès que le tour contient un contenu lu à l'extérieur (page web, fichier) : le texte vient du modèle.
- C'est la seule écriture de Jarvis dans un dossier de l'utilisateur : `ILocalFiles.AppendTextAsync`. Les outils
  de fichiers du modèle restent en lecture seule (ADR 0009). L'écriture ne passe pas par les dossiers
  autorisés : l'utilisateur a donné ce dossier au projet, et il confirme chaque ligne.
- Jarvis n'efface et ne corrige rien dans ce fichier : l'utilisateur le fait à la main.

## Le projet d'un tour (`ConversationEngine`)

Dans l'ordre :

1. le projet **choisi dans la fenêtre de conversation** (liste « Projet », demande écrite seulement) ;
2. sinon le projet **nommé dans la phrase** (« Dans Kazo Os, … »), par mots entiers, le nom le plus long d'abord ;
3. l'agent Jarvis peut en nommer un autre **en passant la main** : `hand_to_agent` a un argument `project`, dont
   les valeurs sont les noms des projets. Un nom qu'aucun projet ne porte ne change rien.

Quand un tour a un projet, **l'agent qui répond reçoit le bloc « [Projet] »** après sa propre mémoire : le nom, le
dossier, puis le texte de `JARVIS.md` ou la phrase qui dit qu'il n'y en a pas. L'agent Jarvis ne le reçoit pas :
sa demande reste courte, et il passe la main. Sans projet reconnu, seule la mémoire de l'agent est envoyée
(section 10). Le tour garde le nom du projet (`ConversationTurn.Project`), affiché sous la réponse.

## L'agent « Projet »

Un septième agent de départ, `project` : outils `list_directory`, `read_file` et `get_claude_sessions`. Ses
consignes disent de répondre d'après le bloc et les fichiers du dossier, et de demander quel projet quand aucun
n'est donné. La description de `hand_to_agent` ajoute une ligne avec les noms des projets.

## La page « Projets »

Après la page « Agents ». Chaque projet montre son nom, son dossier, le début de sa mémoire (ou comment en
écrire une), et si Jarvis peut lire le dossier. **« Autoriser ce dossier »** demande une confirmation, puis ajoute
le dossier aux dossiers autorisés (`IAllowedFolders.AllowAsync`) : créer un projet ne le fait jamais seul.

## Un dossier sans projet (`ProjectCatalog.Unassigned`)

Quand une session Claude Code suivie par Jarvis (une tâche du tableau, `ITaskBoard`, qui n'est pas terminée)
travaille dans un dossier qui n'appartient à aucun projet, la page « Projets » montre une demande en tête :
**« Une session Claude travaille dans X, qui n'appartient à aucun projet. »** L'utilisateur répond de deux façons :

- **assigner le dossier à un projet qui existe** : le dossier s'ajoute aux autres dossiers du projet
  (`ProjectEntry.MoreFolders`, 20 au plus). La mémoire reste dans le dossier principal du projet ;
- **créer un projet sur ce dossier**, en lui donnant un nom.

Jarvis ne choisit jamais, et la demande n'a pas de délai : elle reste tant qu'une session travaille dans le
dossier. Un dossier appartient à un projet quand c'est son dossier, un de ses autres dossiers, ou un
sous-dossier de l'un d'eux, sans tenir compte de la casse ni du sens des barres. Un dossier repris à un projet
(« Retirer ce dossier ») redemande.

## Une session Claude Code dans le dossier du projet

Quand un tour porte sur un projet et qu'un travail part vers Claude Code (« Demande à Claude Code de corriger
le pilote réseau de Kazo OS », ou l'outil de passage de main), la session est lancée **dans le dossier du
projet** : `ClaudeTaskRequest.Folder`. La question « sur quel dossier ? » n'est alors pas posée. La règle des
dossiers autorisés ne change pas (ADR 0021) : si le dossier du projet n'est pas autorisé, rien ne démarre et
Jarvis le dit, avec le nom du dossier et la page où l'autoriser.

## Choix provisoires de ces chantiers

À confirmer par Benoit :

1. Le fichier de mémoire s'appelle `JARVIS.md`, à la racine du dossier. Claude Code ne le lit pas de lui-même :
   il faudra le lui dire au lancement d'une session, ou le nommer depuis `CLAUDE.md`.
2. Tranché par Benoit le 2026-11-23 : le fichier s'appelle `JARVIS.md` (point 1), et Jarvis y écrit sur demande,
   avec confirmation. Jarvis n'y efface rien.
3. Le fichier de mémoire est lu même quand le dossier n'est pas dans les dossiers autorisés.
4. L'agent Jarvis ne reçoit pas le bloc du projet.
5. Le projet choisi dans la fenêtre passe avant celui que la phrase nomme, et s'oublie à l'arrêt de Jarvis.
6. Le projet « Domotique » de l'ADR (section 6) n'est pas créé d'office.
7. La mémoire d'un projet prend un seizième du contexte, en plus des deux autres mémoires.
8. La demande pour un dossier sans projet se voit **seulement à la page « Projets »** : ni l'orbe, ni l'éventail,
   ni la voix ne la signalent encore.
9. « Assigner à un projet existant » ajoute le dossier aux autres dossiers du projet ; le dossier principal et
   la mémoire ne bougent pas.
10. La demande disparaît quand les sessions du dossier sont terminées, et il n'y a pas de bouton pour l'écarter.
11. Le projet du tour passe avant un dossier nommé dans la phrase pour choisir où lancer la session.
12. La session lancée dans le dossier d'un projet ne reçoit pas encore la consigne de lire `JARVIS.md`.

## Vérifications sur Windows

Rien de ce chantier n'a tourné sur Windows.

1. Créer un projet sur un vrai dossier (`C:\...`), avec et sans `JARVIS.md` ; un chemin relatif est refusé.
2. « Autoriser ce dossier », puis lire un fichier du projet par l'agent « Projet ».
3. Le modèle local nomme-t-il le projet en passant la main ? « Dans Kazo Os, il nous reste quoi à faire ? »
4. Taille de la demande de l'agent « Projet » avec un `JARVIS.md` long.
5. Lancer Claude Code à la main dans un dossier sans projet : la demande apparaît-elle à la page « Projets »,
   avec le chemin tel que le hook l'écrit ?
6. « Demande à Claude Code de … dans Kazo OS » : la session démarre-t-elle dans le dossier du projet ?
