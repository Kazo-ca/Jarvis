# Outils de base : fichiers, recherche web, lecture d'une page, page « Outils »

Chantier `stage2/5-basic-tools` (US2.1, suite). Décisions :
[ADR 0017](adr/0017-tools-classes-and-decision-point.md) (sections 3 et 6) et
[ADR 0022](adr/0022-in-app-browser-for-web-search.md). La boucle d'outils et le point de décision sont
décrits dans [tools.md](tools.md).

## Ce qui est construit

```
Tools/Files ─► list_directory, read_file ─► ILocalFiles ─► Adapters/Files/LocalFiles (disque)
Tools/Web   ─► web_search ─► IWebSearchApi (Brave, si une clé est au coffre) ─► Adapters/Web/BraveSearchApi
            │            └► IReadingBrowser (navigateur de Jarvis) ─► hôte Windows : WebViewReadingBrowser
            └► fetch_url ─► IReadingBrowser
            └► IWebAddressPolicy (adresses locales refusées) ◄─ consultée aussi par le navigateur
Tools/Catalog ─► page « Outils » (un interrupteur par outil)
```

Une tranche ne touche ni au disque ni au réseau : elle passe par un contrat du noyau, réalisé dans
`Adapters` ou dans un hôte.

## Outils

| Outil | Tranche | Activé par défaut | Classe | Ce qu'il rend |
| --- | --- | --- | --- | --- |
| `list_directory` | `Tools/Files` | oui | Lecture ; Interdite hors des dossiers autorisés | Sans `path` : les dossiers autorisés. Avec `path` : les entrées du dossier (nom, genre, taille) |
| `read_file` | `Tools/Files` | oui | Lecture ; Interdite hors des dossiers autorisés | Le texte du fichier depuis `start`, et `next_start` quand il en reste |
| `web_search` | `Tools/Web` | **non** | Lecture | Les pages trouvées : titre, adresse, extrait |
| `fetch_url` | `Tools/Web` | **non** | Lecture ; Interdite pour une adresse locale | Le titre et le texte de la page |

Chaque résultat est coupé à une taille réglable, de 200 à 2 000 caractères (2 000 par défaut). Un résultat
revient au modèle comme donnée.

## Fichiers (`Tools/Files`)

- Section de réglages `Tools.Files` : `Folders` (50 au plus), `ResultCharacters`. **Aucun dossier n'est
  autorisé au départ** : les deux outils sont offerts au modèle, mais tout chemin est refusé tant que
  l'utilisateur n'a rien ajouté.
- `AllowedFolders.Check` résout le chemin par `ILocalFiles.Resolve` (liens symboliques et jonctions suivis)
  avant de le comparer aux dossiers autorisés, eux aussi résolus. Un lien qui sort d'un dossier autorisé est
  donc refusé. `C:\Notes` ne contient pas `C:\NotesPrivate`.
- Verdicts : permis, inutilisable (chemin vide ou relatif), dehors, secret. « Secret » couvre les fichiers
  que l'ADR 0021 protège : `.claude\sessions\*.key` et `.claude\ide\*.lock`, même dans un dossier autorisé.
- Un chemin dehors ou secret donne la classe « Interdite » : le point de décision refuse et l'écrit au
  journal des permissions.
- `LocalFiles` (adaptateur) lit 5 Mo au plus par fichier, reconnaît UTF-8 et UTF-16 par leur marque, et refuse
  un fichier binaire.
- Page « Dossiers autorisés » : liste, ajout d'un chemin tapé, retrait, taille d'un résultat. Un dossier qui
  n'existe plus est marqué.

## Web (`Tools/Web`)

- Section de réglages `Tools.Web` : `Engine` (`DuckDuckGo` par défaut, `Startpage`, `Bing`), `Results` (1 à
  10, 5 par défaut), `ResultCharacters`, `LocalExceptions` (20 au plus).
- `web_search` passe par Brave Search quand la clé `tools.web.brave.apikey` est au coffre, sinon par le
  navigateur de Jarvis. Une clé refusée est dite au modèle ; Jarvis ne se replie pas en silence sur le
  navigateur.
- `fetch_url` lit une page par le navigateur de Jarvis. Seuls `http` et `https` sont acceptés.
- `WebAddressPolicy` refuse les adresses de la machine et du réseau local (boucle locale, plages privées,
  lien local, `localhost`, noms sans point), sauf les hôtes listés dans `LocalExceptions`.
- Un moteur qui demande une vérification humaine donne « bloqué » : le modèle le dit, Jarvis ne contourne
  rien.
- Page « Recherche web » : ce qui quitte la machine, moteur, clé Brave (champ de secret), nombre de
  résultats, taille, exceptions locales, « Ouvrir le navigateur de Jarvis » et « Effacer les données du
  navigateur ».

## Navigateur de Jarvis (hôte Windows)

`src/Kazo.Jarvis.Windows/Browser/` réalise `IReadingBrowser` selon l'ADR 0022 :

- `ReadingBrowserWindow` : fenêtre WPF jamais montrée pendant une recherche (sans activation, hors de la
  barre des tâches), avec le contrôleur WebView2 gardé visible pour que la page se dessine. « Voir la
  page » la montre avec une barre d'adresse, Précédent, Recharger et un avertissement. La fermer la cache.
- `WebViewReadingBrowser` : dossier de données à part (`%USERPROFILE%\.kazo\jarvis\browser`), aucun
  téléchargement, aucune nouvelle fenêtre, aucune permission accordée. Chaque navigation et chaque ressource
  passent par `IWebAddressPolicy`, après résolution DNS. Le navigateur se ferme après 5 minutes sans usage.
  Le journal technique n'écrit jamais les mots d'une recherche.
- `SearchPages` : adresses de recherche des trois moteurs et scripts qui lisent les résultats et le texte
  d'une page. Code pur, testé sous Linux.

Dans la fenêtre de conversation, un appel d'outil dont le résultat contient des adresses montre un bouton
« Voir la page » par adresse.

## Page « Outils » (`Tools/Catalog`)

Un interrupteur par outil connu, avec sa classe, son origine et, s'il y a lieu, ce qui quitte la machine
(`ITool.Discloses`). La page estime le poids des outils activés dans le contexte du modèle (caractères
divisés par 4) et le compare à la taille du contexte du fournisseur par défaut.

## Contrats du noyau ajoutés

| Type | Rôle |
| --- | --- |
| `ILocalFiles` (`Kernel/Files`) | Résoudre un chemin, lister un dossier, lire un extrait de texte |
| `IReadingBrowser` (`Kernel/Web`) | Chercher, lire une page, montrer la fenêtre, effacer les données |
| `IWebSearchApi` | Une recherche par une API avec clé |
| `IWebAddressPolicy` | Cette adresse est-elle permise ? Avant et après résolution DNS |
| `ITool.Discloses` | Ce qui quitte la machine, en une phrase ; `null` par défaut |

## Hôte Web

`SimulatedLocalFiles` (dossier `/home/demo/Documents`, lien `lien-prive` qui en sort) et
`SimulatedReadingBrowser` (`https://pages.jarvis.example/meteo`, `/crepes`, `/long` ; une recherche qui
contient « captcha » est bloquée). Le modèle simulé demande un outil d'après les mots : « liste le
dossier … », « lis le fichier … », « cherche sur le web … », « lis la page … ». L'élément `#browser` de la page
d'accueil montre ce que le navigateur simulé a reçu.

## Choix provisoires de ce chantier

Les ADR ne les tranchent pas ; à confirmer par Benoit :

1. **Un dossier s'ajoute en tapant son chemin** : pas encore de sélecteur de dossier de Windows.
2. **Pas de réglage « plus strict » par outil** (demander une confirmation pour un outil de lecture) : il
   existe pour les outils des serveurs MCP ([mcp.md](mcp.md)), pas encore pour les outils intégrés.
3. **Le navigateur se ferme après 5 minutes sans usage**, valeur fixe.
4. **Une recherche de Jarvis remplace la page que l'utilisateur regarde** dans la fenêtre du navigateur : il
   n'y a qu'un seul onglet.
5. **« Voir la page » liste les adresses trouvées dans le résultat d'un outil**, 10 au plus.
6. **Les fichiers `.key` et `.lock` de Claude Code** sont refusés même dans un dossier autorisé.
7. **L'estimation des jetons** est grossière : caractères divisés par 4.

## Limites connues

- Entre la vérification DNS et la connexion, un nom peut changer d'adresse (« DNS rebinding ») : WebView2 ne
  permet pas d'épingler l'adresse.
- Les scripts de lecture dépendent du balisage des moteurs : un changement chez eux donne « rien lu », pas
  un faux résultat.

## Non vérifié

- **Rien n'a tourné sur Windows.** Le navigateur WebView2 compile sous Linux ; ses scripts sont essayés dans
  Chromium contre des pages écrites à la main, jamais contre les vrais moteurs depuis Jarvis (l'essai E15 les
  a prouvés à part).
- L'API de Brave Search : testée contre des réponses écrites à la main.
- Les jonctions NTFS : `LocalFiles` est testé avec des liens symboliques sous Linux.

## Tests

- Unitaires : `FileToolsTests`, `WebToolsTests`, `ToolCatalogTests`, `LocalFilesTests`,
  `BraveSearchApiTests`, `SearchPagesTests`.
- Playwright : `BasicToolsTests` (page « Outils », refus sans dossier autorisé et journal, dossier listé et
  lu, lien qui sort refusé, recherche offerte seulement une fois activée, page lue, adresse locale refusée,
  recherche bloquée, page « Recherche web ») et `SearchPageScriptsTests` (les scripts des trois moteurs et
  celui du texte d'une page).

## Vérifications sur Windows

À faire avec Benoit :

1. Activer `web_search`, demander « cherche sur le web … » : aucune fenêtre n'apparaît, le premier plan ne
   change pas, les résultats viennent de DuckDuckGo. Refaire avec Startpage et Bing.
2. « Voir la page » : la fenêtre s'ouvre sur la page, l'avertissement est visible ; la fermer puis relancer
   une recherche.
3. `fetch_url` sur une vraie page, puis sur `http://localhost:11435` et sur l'adresse du routeur : refusés.
4. « Effacer les données du navigateur » : le dossier `browser` est vidé, une recherche fonctionne encore.
5. Après 5 minutes sans recherche : plus aucun processus `msedgewebview2` du navigateur de Jarvis.
6. Dossiers autorisés : un dossier réel, une jonction NTFS qui en sort (refusée), un fichier UTF-16, un gros
   fichier lu par morceaux.
7. Avec `qwen3:14b` : le modèle choisit le bon outil et ne suit pas une instruction écrite dans une page.
8. Avec une clé Brave : une recherche passe par l'API ; avec une clé fausse, la cause est dite.
