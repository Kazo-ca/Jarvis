# Résultats E15 — 20 novembre 2026

**E15 : réussi.** Un `CoreWebView2Controller` (paquet `Microsoft.Web.WebView2` 1.0.3179.45, runtime Evergreen
157.0.4341) attaché à une fenêtre WPF **jamais affichée** fait des recherches web et en extrait titre, adresse et
extrait par `ExecuteScriptAsync`, sans jamais prendre le premier plan : **0 changement de fenêtre au premier plan
sur 180 vérifications** avant/après, plus un échantillonnage toutes les 100 ms. DuckDuckGo (version HTML et version
normale) et Startpage répondent 10 fois sur 10 ; Bing aussi une fois l'extraction corrigée ; **Google rend les
résultats mais cache les adresses** ; **Brave bloque dès la première requête** (HTTP 429 et CAPTCHA). Toutes les
protections « lecture seule » tiennent, dont le blocage de la boucle locale et du réseau privé : **0 requête reçue
par le serveur local sous protection, contre 18 sans**. Le navigateur Edge ou Chrome installé n'a jamais été lancé.

Mesures résumées : [measurements.json](measurements.json). Mode d'emploi : [README.md](README.md).

## Fenêtre cachée ou affichée sans activation

Fenêtre WPF sans bordure 480×360 en bas à droite, `ShowInTaskbar=false`, `ShowActivated=false`, styles étendus
`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (et sans `WS_EX_APPWINDOW`), poignée créée par `EnsureHandle()`.

| Variante | Création | Page (`visibilityState`) | `requestAnimationFrame` | `setTimeout(0)` | `setInterval(100)` | `CapturePreviewAsync` |
| --- | --- | --- | --- | --- | --- | --- |
| Jamais affichée, contrôleur `IsVisible=true` | 0,66 s | `visible` | 55/s | 223/s | 10/s | OK en 70 ms |
| Jamais affichée, contrôleur `IsVisible=false` | 0,31 s | `hidden` | **0/s** | **4/s** | **1/s** | **bloque 139 s** (deux essais) |
| Affichée sans activation, `IsVisible=true` | 0,38 s | `visible` | 56/s | 223/s | 10/s | OK en 51 ms |
| Jamais affichée puis `Show()` à la demande | — | `visible` | 60/s | 224/s | 10/s | OK en 67 ms |

- Dans les trois variantes, `NavigationCompleted` arrive et l'extraction par script fonctionne (DuckDuckGo HTML :
  5 résultats en 1,3 à 4,0 s ; Bing : 3 résultats en 0,7 s). La fenêtre jamais affichée **rend** la page (le pixel
  lu dans la capture a la couleur de la page de test).
- `IsVisible=false` fait passer la page en arrière-plan : Chromium bride les minuteries (une par seconde), arrête
  l'animation, et la capture attend 139 s. **À éviter** : garder le contrôleur visible dans une fenêtre jamais
  affichée.
- Afficher la fenêtre à la demande (`Show()` avec `ShowActivated=false`, donc `SW_SHOWNA`) ne change pas le
  premier plan.

## Premier plan

`GetForegroundWindow` est lu avant et après chaque création, navigation, recherche, lecture de page et
affichage, et par un échantillonneur toutes les 100 ms ; « à nous » = le processus de l'essai ou un de ses
processus WebView2 (`GetProcessInfos`).

| Passe | Vérifications | Premier plan changé | Premier plan à nous |
| --- | --- | --- | --- |
| Fenêtre cachée ou affichée (2 passes) | 28 | 0 | 0 |
| Recherche (60 requêtes, 3 pages lues) | 57 | 0 | 0 |
| Protections (3 passes) | 91 | 0 (l'échantillonneur a vu 2 changements vers d'autres programmes : l'utilisateur travaillait sur le poste) | 0 |
| Bing corrigé | 4 | 0 | 0 |

Aucune variante n'a dû être arrêtée.

## Moteurs

Fenêtre jamais affichée, contrôleur visible, 10 requêtes par moteur (5 en français, 5 en anglais : météo à
Montréal, une API .NET, l'actualité, une recette, un produit), moteurs entrelacés, au moins 6 s entre deux
requêtes au même moteur. Succès = au moins 3 résultats avec titre et adresse externe. Temps = de `Navigate` aux
résultats extraits.

| Moteur | Succès | CAPTCHA / mur | Médiane | Pire | Remarques |
| --- | --- | --- | --- | --- | --- |
| DuckDuckGo HTML (`html.duckduckgo.com`) | **10/10** | 0 | 1,76 s | 3,36 s (1re, à froid) | HTML statique, sans JavaScript ; liens `/l/?uddg=` décodés |
| DuckDuckGo normal | **10/10** | 0 | 1,30 s | 3,37 s (1re) | rendu par JavaScript, sélecteurs `data-testid` |
| Startpage | **10/10** | 0 | **1,01 s** | 1,38 s | |
| Bing | 3/10, puis **3/3** corrigé | 0 | 0,48 s (corrigé) | 0,64 s (corrigé) | bandeau de cookies sur chaque page, sans gêner ; liens `/ck/a?u=a1<base64>` décodés |
| Google | **0/10** | 0 (13 requêtes en tout) | — | 10,3 s (délai) | résultats rendus, mais liens opaques `/goto?url=<jeton>` : seul l'hôte est lisible (`cite`) |
| Brave Search | **0/3**, arrêté | **3/3** dès la 1re | — | 3,3 s | HTTP 429 et page « vérifiez que vous êtes humain » |

- **Bing** : la page contenait bien 10 résultats, mais `innerText` est vide pour ceux qui sont sous la ligne de
  flottaison et pas encore rendus (fenêtre de 360 px). Avec un repli sur `textContent` : 3 sur 3. Les chiffres de
  DuckDuckGo et Startpage viennent de la passe d'avant la correction, qui ne les gênait pas.
- **Google** n'a montré ni CAPTCHA ni mur de consentement depuis le Québec, mais l'adresse réelle ne s'obtient
  qu'en suivant la redirection `/goto` (une navigation de plus par résultat), ce que l'essai n'a pas fait.
- **Stabilité de l'analyse** : une heuristique générique (« lien qui contient un `h2`/`h3`, hors domaine du
  moteur ») trouve les mêmes 5 premiers résultats que les sélecteurs propres au moteur pour DuckDuckGo (les
  deux) et Startpage (recouvrement 5/5 sur 10 requêtes), et fait mieux que le sélecteur sur Bing (5/10 contre 3/10
  avant la correction). La page la plus simple à analyser est DuckDuckGo HTML (`a.result__a`, `.result__snippet`,
  page sans script) ; l'heuristique générique sert de filet quand un moteur change son balisage.

## Coût

| Mesure | Valeur |
| --- | --- |
| Création de l'environnement | 0,26 s |
| Création du contrôleur | 0,36 s |
| Première recherche (à froid) | 3,4 s, soit environ 4 s de la création au premier résultat |
| Deuxième recherche (premier passage sur un autre moteur) | 3,4 s ; ensuite 0,5 à 1,8 s (médianes) |
| Mémoire au repos après création (`about:blank`) | 5 processus, 124 Mo privés, 289 Mo de mémoire de travail |
| Mémoire pendant les recherches (pointe) | 9 processus, 1 042 Mo privés, 1 288 Mo de mémoire de travail |
| Mémoire au repos après les recherches (`about:blank`, 5 s) | 7 processus, 594 Mo privés, 877 Mo de mémoire de travail |
| Fermeture (`Controller.Close()` jusqu'à la sortie du processus navigateur) | 0,2 à 0,3 s (une fois 1,4 s), processus toujours sortis |

La mémoire ne redescend pas d'elle-même après les recherches : fermer le contrôleur entre deux séries (0,3 s), ou
après un délai d'inactivité, puis le recréer (0,6 s) coûte moins que garder une instance ouverte.

## Lire une page

Trois adresses tirées des résultats ; texte = `innerText` du premier `main`/`article`/`[role=main]`, sinon `body`.

| Site | Temps total | Texte | Calques |
| --- | --- | --- | --- |
| Recette (`ricardocuisine.com`) | 5,8 s | 6 369 caractères | bandeau de cookies plein écran (100 %) : le texte se lit quand même |
| Documentation .NET (`learn.microsoft.com`) | 3,2 s | **135 caractères** | aucun |
| Actualité (`regulation-ai.eu`) | 2,7 s | 7 460 caractères | bandeau de cookies (37 %) et un panneau fixe (62 %) |

- Les bandeaux de consentement ne bloquent pas la lecture par script (rien n'a été cliqué), mais leur texte reste
  à retirer.
- Sur `learn.microsoft.com`, le premier `article` est un petit bloc : prendre **le plus long** des
  `main`/`article`, ou une extraction de type « lisibilité », plutôt que le premier trouvé.

## Lecture seule imposée par l'hôte

Pages de test locales (`https://e15.test/`, hôte virtuel lié à un dossier), une origine `http` fictive servie par
l'hôte (`http://e15-public.example/`, page et redirection 302) et un petit serveur HTTP sur 127.0.0.1 qui compte
les requêtes reçues.

| Protection | Preuve |
| --- | --- |
| `DownloadStarting` annulé | 2 téléchargements (lien `download` en `data:`, navigation vers un `.bin`) : événement reçu, `Cancel=true`, **0 fichier** dans le dossier de téléchargement |
| `NewWindowRequested` redirigé | `window.open` et lien `target=_blank` : aucune fenêtre, la page demandée s'affiche dans la même vue |
| `PermissionRequested` refusé | caméra, micro, géolocalisation, notifications : 4 demandes refusées ; la page reçoit `NotAllowedError` (×2), le code d'erreur 1 et `denied` |
| Pas d'objet hôte, messages ignorés | `AreHostObjectsAllowed=false`, `IsWebMessageEnabled=false` : **`chrome.webview` existe encore** et `postMessage` ne lève pas d'erreur, mais `WebMessageReceived` a reçu **0** message ; aucun objet hôte n'est ajouté |
| Boîtes de dialogue | `alert`, `confirm`, `prompt` : `ScriptDialogOpening` reçu, rien d'affiché, la page reçoit `undefined`, `false`, `null` |
| `LaunchingExternalUriScheme` annulé | schéma non enregistré, et `mailto:` sans geste de l'utilisateur (clic par script, `Navigate`) : pas d'événement, rien de lancé (Chromium refuse déjà). `mailto:` avec un vrai clic (`Input.dispatchMouseEvent` par l'API WebView2) : événement reçu, `Cancel=true`, **aucun client de courriel lancé** |
| Mots de passe et remplissage automatique | `IsPasswordAutosaveEnabled=false` et `IsGeneralAutofillEnabled=false` relus ; comportement non éprouvé (aucun formulaire rempli) |

Boucle locale et réseau privé (`NavigationStarting`, `FrameNavigationStarting`, `WebResourceRequested` avec
`CoreWebView2WebResourceRequestSourceKinds.All`) :

| Cas | Sans protection (témoin) | Avec protection |
| --- | --- | --- |
| Navigation vers `http://127.0.0.1:<port>/` | atteint le serveur | annulée (`NavigationStarting`) |
| `http://localhost`, `[::1]`, `2130706433`, `0x7f.0.0.1` | — | annulées (Chromium normalise l'adresse avant l'événement) |
| `http://192.168.255.254/` | — | annulée |
| Nom qui pointe vers 127.0.0.1 (`localtest.me`, `127.0.0.1.nip.io`) | atteint le serveur | bloqué par résolution DNS dans `WebResourceRequested` (réponse 403 de l'hôte) |
| Redirection 302 vers 127.0.0.1 (navigation) | atteint le serveur | annulée (`NavigationStarting`, `IsRedirected`) |
| Sous-ressources vers 127.0.0.1 : `img`, `script`, `fetch`, `iframe` | atteignent le serveur | bloquées (`WebResourceRequested`, `FrameNavigationStarting` pour l'iframe) |
| Sous-ressource redirigée vers 127.0.0.1 (`img`, `fetch`) | atteint le serveur | bloquée : `WebResourceRequested` voit la cible de la redirection |
| Sous-ressources vers 192.168.255.254 depuis une page `http` | — | bloquées (`WebResourceRequested`) |
| **Requêtes reçues par le serveur local** | **18** | **0** |

Depuis une page `https`, Chromium bloque déjà de lui-même, avant l'événement, les sous-ressources `http` vers
192.168.x.x et vers un nom DNS (contenu mixte) ; 127.0.0.1 passe, car Chromium le tient pour sûr. D'où le test
supplémentaire depuis une page `http`.

## Profil

Dossier de données `C:\jarvis\temp\e15-webview-search\profile`, quatre processus successifs :

1. après les recherches : 75 témoins (cookies) sur 22 domaines ; ajout d'un témoin d'essai qui expire le lendemain → 76 ;
2. nouveau processus : 76 témoins, le témoin d'essai est là (**persistance prouvée**) ;
3. `Profile.ClearBrowsingDataAsync()` : 250 ms, **0 témoin** ;
4. nouveau processus : toujours 0.

## Recommandations

- **Moteur par défaut : DuckDuckGo HTML** (`html.duckduckgo.com/html/?q=`) : 10/10, aucun CAPTCHA, page sans
  JavaScript au balisage simple, liens décodables, environ 1,8 s. **Repli : Startpage** (10/10, le plus rapide),
  puis Bing (rapide, mais liens encodés et bandeau de cookies). Ne pas compter sur Google (adresses opaques) ni sur
  Brave (bloqué d'emblée depuis ce poste).
- **Fenêtre** : fenêtre WPF dont la poignée est créée par `EnsureHandle()` **sans `Show()`**, sans bordure, petite,
  dans un coin, `ShowInTaskbar=false`, `ShowActivated=false`, `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` ; contrôleur
  **`IsVisible=true`** ; `Show()` seulement si l'utilisateur demande à voir la page.
- **Réglages** : ceux des tableaux ci-dessus, plus `IsMuted=true`, `AreDevToolsEnabled=false`, menus contextuels et
  raccourcis du navigateur coupés, dossier de téléchargement dans le dossier de l'application, et le blocage du
  réseau privé **dans `WebResourceRequested` avec résolution DNS** (le filtre sur l'adresse littérale seul laisse
  passer `localtest.me`).
- Extraire avec `textContent` en repli d'`innerText`, garder l'heuristique générique en filet, et fermer le
  contrôleur après usage pour rendre la mémoire.

## Non vérifié

- Une vraie redirection externe vers 127.0.0.1 : `httpbin.org` et `httpbingo.org` répondent 403 à `redirect-to`.
  La redirection a donc été servie par l'hôte (origine fictive), ce qui passe par le même chemin dans WebView2.
- La reliaison DNS (*DNS rebinding*) : le nom est résolu par l'hôte au moment de la requête, puis de nouveau par
  Chromium ; un serveur qui change de réponse entre les deux passerait.
- L'enregistrement réel d'un mot de passe ou un remplissage automatique (réglages relus seulement).
- Le comportement des moteurs sur des centaines de requêtes ou sur plusieurs jours (une seule série de 10).
- DuckDuckGo et Startpage n'ont pas été rejoués après la correction `textContent` (ils n'en avaient pas besoin) ;
  Bing ne l'a été que sur 3 requêtes.
- L'adresse réelle des résultats Google (en suivant `/goto`), et Brave depuis une autre adresse IP.
- Le bridage d'une fenêtre minimisée ou masquée par une autre ; la consommation processeur.
