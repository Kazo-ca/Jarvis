# Projets : un nom, un dossier, une mémoire, et l'agent « Projet »

Chantier `stage2/19-projects`. Décision : [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 6, 10,
11 et 13 (point 2). Les agents sont décrits dans [agents.md](agents.md), la mémoire de conversation dans
[conversation-memory.md](conversation-memory.md).

Ce chantier construit les projets, leur page, leur mémoire lue dans le dossier, l'agent « Projet » et le projet
d'un tour. **Restent à construire :** la demande d'assigner à un projet un dossier qui n'en a pas (quand une
session Claude Code y travaille), le lancement d'une session Claude Code dans le dossier du projet, l'écriture
de la mémoire d'un projet par Jarvis, puis la vue des projets (ADR 0026).

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
lectures. Jarvis ne l'écrit pas encore.

- Lu par `ILocalFiles`, sans passer par les dossiers autorisés : c'est l'utilisateur qui a donné ce dossier au
  projet, et seul ce fichier est lu. Les outils `list_directory` et `read_file` gardent leur règle : l'agent ne
  lit le reste du dossier que si le dossier est autorisé.
- Sa part : un seizième du contexte (`ProjectCatalog.Budget`). Un fichier plus long est coupé, avec « … ».
- Un crochet du fichier devient une parenthèse : il ne peut pas fermer le bloc.
- Jamais envoyé au cloud par ce chemin : le bloc va au modèle local, avec la mémoire.

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

## Choix provisoires de ce chantier

À confirmer par Benoit :

1. Le fichier de mémoire s'appelle `JARVIS.md`, à la racine du dossier. Claude Code ne le lit pas de lui-même :
   il faudra le lui dire au lancement d'une session, ou le nommer depuis `CLAUDE.md`.
2. Jarvis lit la mémoire d'un projet et ne l'écrit pas.
3. Le fichier de mémoire est lu même quand le dossier n'est pas dans les dossiers autorisés.
4. L'agent Jarvis ne reçoit pas le bloc du projet.
5. Le projet choisi dans la fenêtre passe avant celui que la phrase nomme, et s'oublie à l'arrêt de Jarvis.
6. Le projet « Domotique » de l'ADR (section 6) n'est pas créé d'office.
7. La mémoire d'un projet prend un seizième du contexte, en plus des deux autres mémoires.

## Vérifications sur Windows

Rien de ce chantier n'a tourné sur Windows.

1. Créer un projet sur un vrai dossier (`C:\...`), avec et sans `JARVIS.md` ; un chemin relatif est refusé.
2. « Autoriser ce dossier », puis lire un fichier du projet par l'agent « Projet ».
3. Le modèle local nomme-t-il le projet en passant la main ? « Dans Kazo Os, il nous reste quoi à faire ? »
4. Taille de la demande de l'agent « Projet » avec un `JARVIS.md` long.
