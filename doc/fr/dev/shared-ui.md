# Interface partagée et langue

Chantier `stage1/5-shared-ui-language` du [plan de l'étape 1](plans/stage-1.md). Décisions :
[ADR 0008](adr/0008-languages-and-localization.md) (langues) et [ADR 0011](adr/0011-orb-colors-and-drawing.md),
section 2 (couleurs et polices).

## Style commun

- `Kazo.Jarvis.Features/wwwroot/css/jarvis.css`, servi à `_content/Kazo.Jarvis.Features/css/jarvis.css` et lié par
  chaque page d'hôte (`index.html` et `orb.html` de l'hôte Windows, `App.razor` de l'hôte Web).
- Il définit une seule fois les couleurs de l'ADR 0011 et du catalogue en variables CSS (`--jarvis-cyan`,
  `--jarvis-violet`, `--jarvis-green`, `--jarvis-red`, `--jarvis-grey`, `--jarvis-amber`, `--jarvis-text`,
  `--jarvis-text-muted`, `--jarvis-background`, lignes et panneaux) et les trois familles de polices
  (`--jarvis-font-display`, `--jarvis-font-body`, `--jarvis-font-mono`). Les composants n'écrivent aucune valeur
  de couleur : ils utilisent ces variables dans leur `.razor.css`.
- **Polices embarquées** dans `wwwroot/fonts/`, jamais téléchargées à l'exécution : Rajdhani 500 et 600
  (titres), Inter 400 et 600 (texte), JetBrains Mono 400 et 500 (étiquettes, chiffres). Fichiers `woff2` du
  sous-ensemble latin (les accents français y sont), tirés des paquets `@fontsource` 5.3.0. **Licence
  vérifiée :** les trois sont sous SIL Open Font License 1.1, qui permet de les distribuer avec une application
  sous Apache 2.0 ; le texte de chaque licence est à côté des fichiers (`OFL-*.txt`).

## Traductions

- Ressources `.resx` à côté du composant ou de la classe qui les utilise : `Nom.resx` en anglais (langue
  neutre, `<NeutralLanguage>en</NeutralLanguage>`), `Nom.fr.resx` en français. Le nom de manifeste est le nom
  complet du type, donc le dossier suit l'espace de noms.
- Les composants injectent `IStringLocalizer<T>`. `AddJarvisFeatures` le remplace par
  `LanguageStringLocalizer<T>` (`Features/Shared/Localization`), qui lit **la langue de l'assistant** à chaque
  recherche, jamais la culture du fil d'exécution. Ainsi les dates et les nombres gardent les réglages
  régionaux de Windows (ADR 0008, section 1), et un changement de langue vaut pour toutes les fenêtres au
  rendu suivant.
- Texte absent en français : le texte anglais s'affiche et une ligne de niveau `Debug` le signale au journal.
  Clé absente partout : le nom de la clé s'affiche, avec un avertissement.
- Les textes communs à trois tranches ou plus sont dans `Features/Shared/SharedTexts.resx`.
- Les textes de l'hôte Windows (menu de la zone de notification, messages d'erreur) sont dans
  `Kazo.Jarvis.Windows/Desktop/HostTexts.resx`. Ceux qui s'affichent avant les services (WebView2 absent)
  suivent la langue de Windows, les autres la langue de l'assistant ; le menu relit ses textes à chaque
  ouverture.
- Les hôtes ne publient que `en` et `fr` (`SatelliteResourceLanguages`).
- **Test des clés** (`Architecture/ResourceKeysTests`) : chaque `.resx` sous `src/` a son `.fr.resx` avec les
  mêmes clés, et inversement, sans texte vide. Il fait partie du seuil de fusion.

## Réglage de langue (tranche `Settings/Language`)

- Section `Settings.Language` de `config.json` : `{ "code": "fr" }` ou `"en"`. Absente ou `null` : la langue
  proposée d'après Windows (français si l'affichage de Windows est en français, quelle que soit la région ;
  anglais sinon). Un autre code est refusé par la validation.
- `LanguageKeeper` fournit `IAssistantLanguageReader` (noyau) et publie `AssistantLanguageChanged` après
  chaque changement, une fois `Current` à jour. Les autres tranches (transcription, voix, conversation)
  liront la langue par ce contrat.
- L'hôte fournit `ISystemLanguage` : la langue d'affichage de Windows lue au démarrage (hôte Windows), ou
  `Simulated:SystemLanguage` de la configuration, anglais par défaut (hôte Web).
- **Rechargement sans redémarrer :** chaque fenêtre est enveloppée dans `LanguageScope`
  (`Features/Shared/Localization`). Au changement de langue, son contenu est reconstruit sous une nouvelle
  clé, donc tous les textes sont relus. Ce qui n'était pas enregistré (un champ à moitié tapé) est perdu,
  comme à la réouverture de la fenêtre. Prouvé dans l'hôte Web par Playwright ; **à vérifier sous Windows**
  dans `BlazorWebView` (voir [hôtes](hosts.md#vérifications-sur-windows)). Si ce n'est pas fiable, un nouvel
  ADR dira que l'interface change au prochain démarrage (ADR 0008).
- La barre de titre d'une fenêtre déjà ouverte garde la langue de son ouverture (le titre est fixé par
  `DialogRequest`) ; le contenu, lui, change.

## Fenêtre des paramètres (tranche `Settings/Window`)

- Elle traite `SettingsWindowRequested` : l'entrée « Paramètres » de la zone de notification n'est plus
  grisée. Une seule fenêtre (clé `Settings.Window`).
- Les pages viennent des tranches, par un enregistrement `SettingsPage` du noyau (clé, composant, ressource
  et clé du titre, ordre). La fenêtre ne connaît aucune tranche. La langue est la première page (ordre 0).
- La page choisie est gardée par `SettingsWindowState`, pour que le changement de langue laisse
  l'utilisateur sur la même page.
- Dans l'hôte Web, le bouton « Open the settings » publie le même événement que le menu.

## Composants partagés (`Features/Shared/`)

| Composant | Rôle |
| --- | --- |
| `SettingsFrame` | Cadre d'une page de réglages : titre en Rajdhani, description, champs |
| `SettingsField` | Un champ : étiquette, contrôle, phrase d'aide ; un `fieldset`, lisible par un lecteur d'écran |
| `SecretField` | Saisie d'une clé ou d'un jeton vers le coffre (ADR 0004, section 4) |
| `LanguageScope` | Reconstruit le contenu d'une fenêtre au changement de langue |

Le champ de secret ne relit jamais la valeur enregistrée : il apprend seulement si une clé existe, par la
liste des références du coffre. Il retire les espaces autour d'une clé collée, vide sa saisie dès que la clé
est dans le coffre, et dit « coffre inaccessible » si le coffre refuse. Sa logique est dans `SecretEntry`,
testée sans navigateur. Il servira aux fournisseurs (chantier 11), aux voix cloud (chantier 12) et plus tard
aux connecteurs.
