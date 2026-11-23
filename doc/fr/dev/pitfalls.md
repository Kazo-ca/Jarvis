# Pièges

Pièges rencontrés pendant le développement et leur solution. Ajouter une entrée
dès qu'un problème a coûté du temps à diagnostiquer.

## sherpa-onnx 2.0 n'a plus espeak-ng (essai E4)

Avec `org.k2fsa.sherpa.onnx` 2.0.x, Piper et Kokoro hors anglais échouent : « phoneme_codepoints is empty,
no lexicon, and no frontend available ». La 2.0 a retiré espeak-ng et piper-phonemize (PR 4055). Rester sur
**1.13.8**, ou fournir soi-même les phonèmes. espeak-ng est sous GPLv3 : à considérer pour la licence.

## Kokoro multilingue : pas de langue `en-gb` (essai E4)

La voix britannique `bm_george` s'utilise avec `Lang = "en-us"` et le lexique `lexicon-us-en.txt`.
`en-gb` fait échouer espeak-ng (« Failed to set eSpeak-ng voice »).

## qwen2.5-coder écrit ses appels d'outils en texte (essai E7)

Le modèle renvoie `{"name": ..., "arguments": ...}` dans `content` au lieu de `tool_calls`. Utiliser
qwen3 (avec `think: false`) ou un analyseur tolérant qui n'accepte qu'un objet JSON entier nommant un outil déclaré.

## Une fenêtre WPF transparente animée coûte cher (essai E6)

`AllowsTransparency=True` crée une fenêtre « layered » : chaque image modifiée est recopiée par le processeur.
Avec `WebView2CompositionControl`, la capture ajoute 4 à 5 % d'un cœur dès que la page bouge. Garder l'orbe
immobile en veille.

## `requestAnimationFrame` réveille le moteur même sans dessiner (essai E6)

Une boucle qui saute des images réveille quand même le moteur 60 fois par seconde. Sous 10 images par
seconde, utiliser une minuterie.

## La connexion du Claude Code en ligne de commande expire à part (essai E8)

Les sessions de Claude Desktop restent connectées alors que `claude -p` répond « OAuth session expired ».
Kazo.Jarvis doit détecter ce message et demander de relancer `/login`, au lieu d'échouer en silence.

## NAudio 3 utilise `Span<float>`

`ISampleProvider.Read` prend un `Span<float>`. Les exemples écrits pour NAudio 2 ne compilent plus.

## NAudio 3 : `WasapiCapture` et `WasapiOut` sont obsolètes (chantier stage1/8-audio-devices)

Elles donnent l'avertissement CS0618, et la construction doit rester sans avertissement. Utiliser
`WasapiRecorderBuilder` et `WasapiPlayerBuilder`. `DataAvailable` reçoit alors directement un
`ReadOnlySpan<byte>` (plus de `WaveInEventArgs`) ; `MMDevice.AudioClient` est obsolète aussi
(`WasapiPlayer.DeviceMixFormat` donne le format de mélange). Les événements d'un enregistreur ou d'un lecteur
passent par le contexte de synchronisation du fil qui le crée : le créer sur un fil du pool, jamais sur le fil
d'interface.

## Un `[SetUpFixture]` NUnit ne vaut que pour son espace de noms (chantier stage1/1-solution)

Placé dans `Kazo.Jarvis.Tests.E2E.Drivers`, le démarrage de l'hôte Web ne tournait pas pour les tests de
`Kazo.Jarvis.Tests.E2E.Scenarios` : l'adresse restait celle par défaut et Chromium répondait
`ERR_CONNECTION_REFUSED`. Le mettre dans l'espace de noms racine de l'assembly de tests.

## `WebApplicationFactory.UseKestrel(0)` ne donne pas l'adresse choisie (chantier stage1/1-solution)

Après `StartServer()`, `ClientOptions.BaseAddress` reste `http://localhost/`. Lire l'adresse réelle dans
`IServer.Features.Get<IServerAddressesFeature>()`.

## `Directory.Packages.props` à la racine s'applique aussi aux essais (chantier stage1/1-solution)

La gestion centrale des versions casse les `.csproj` des essais, qui donnent leurs propres versions.
`spikes/Directory.Packages.props` la coupe pour eux (`ManagePackageVersionsCentrally` à `false`).

## L'hôte Windows meurt s'il démarre sur une session verrouillée (chantier stage1/1-solution)

`WebView2CompositionControl` crée un périphérique Direct3D 9 au chargement. Sur un bureau verrouillé,
Direct3D 9 n'est pas disponible : `COMException 0x8876086A` (D3DERR_NOTAVAILABLE) dans
`Direct3DHelper.CreateD3D9Device`, non attrapée, et le processus se termine avec le code `0xC000041D`, sans
fenêtre. Déverrouillé, l'hôte démarre normalement. Le démarrage avec Windows et la sortie de veille peuvent
tomber sur ce cas : le chantier `stage1/4-hosts` doit attendre le déverrouillage (ou réessayer) au lieu de
mourir. `LogonUI.exe` tourne aussi en session Bureau à distance : ce n'est pas un bon signe de verrouillage.
**Réglé au chantier `stage1/4-hosts`** : l'hôte attend que la session soit déverrouillée (`WTSSessionInfoEx` ;
le bureau d'entrée reste `Default` sur l'écran de verrouillage de Windows 10 et 11) avant de créer l'orbe, et rattrape `D3DERR_NOTAVAILABLE` ou `D3DERR_DEVICELOST` en rouvrant l'orbe plus tard
([hôtes](hosts.md#session-verrouillée)).

## Une garde `OperatingSystem.IsWindows()` ne couvre pas un lambda (chantier stage1/3-settings-storage-vault)

L'analyseur CA1416 signale un type `[SupportedOSPlatform("windows")]` créé dans un lambda d'enregistrement
(`services.TryAddSingleton(provider => new CredentialManagerVault())`), même à l'intérieur d'un
`if (OperatingSystem.IsWindows())` : le lambda s'exécute plus tard, hors de la garde. Mettre ces
enregistrements dans une méthode marquée `[SupportedOSPlatform("windows")]`, appelée sous la garde.

## NUnit 4 ignore les tests `[SupportedOSPlatform("windows")]` ailleurs (chantier stage1/3-settings-storage-vault)

Sous Linux, un test ou une classe marqué `[SupportedOSPlatform("windows")]` est ignoré (« Skipped »), sans
échec. Les tests Windows portent aussi `[Category("Windows")]`, pour que l'intégration continue les écarte
explicitement et que l'agent sur le PC de Benoit puisse les lancer seuls (`--filter "TestCategory=Windows"`).

## Un clic Playwright sur une page Blazor pré-rendue se perd (chantier stage1/4-hosts)

Une page `InteractiveServer` est d'abord rendue côté serveur : les boutons sont là, mais leurs `@onclick` ne
répondent qu'une fois le circuit SignalR établi. Un clic plus tôt ne fait rien, et le test échoue au hasard.
Attendre un signe d'interactivité : la page d'accueil de l'hôte Web expose
`data-interactive="@RendererInfo.IsInteractive"`, et les tests attendent `true` avant de cliquer.

## Windows Forms et WPF dans le même projet : `Application` devient ambigu (chantier stage1/4-hosts)

`UseWindowsForms` (pour l'icône de la zone de notification) ajoute les `using` globaux `System.Windows.Forms`
et `System.Drawing`, dont `Application`, `MessageBox` et `Color` entrent en conflit avec ceux de WPF. Les
retirer dans le `.csproj` (`<Using Remove="System.Windows.Forms" />`, idem pour `System.Drawing`) et écrire
les `using` à la main dans les seuls fichiers qui en ont besoin.

## `BackgroundService` démarre `ExecuteAsync` sur un autre fil depuis .NET 10 (chantier stage1/16-updates)

Depuis .NET 10, `BackgroundService.StartAsync` lance `ExecuteAsync` sur un fil du pool au lieu de l'exécuter
jusqu'au premier `await`. Avec `FakeTimeProvider`, le premier `Task.Delay` peut donc ne pas exister encore
quand le test avance l'horloge, et l'attente est décalée. Après `StartAsync`, laisser la boucle atteindre son
attente (quelques millisecondes réelles) avant d'appeler `Advance`.

## Position WPF et écrans d'échelles différentes (chantier stage1/4-hosts)

Sur le PC de Benoit (écrans à 150 % et à 100 %), la position de l'orbe enregistrée par `Window.Left`/`Top`
(-639, 387) revenait à (-959, 581) au redémarrage : × 1,5 à chaque relance. Avant l'affichage, WPF convertit
`Left`/`Top` avec l'échelle de l'écran principal, pas de celui de destination. Enregistrer la position en
pixels physiques (`GetWindowRect`) et la rendre par `SetWindowPos` dans `SourceInitialized`, avant l'affichage :
Windows envoie alors le changement d'échelle et WPF redimensionne la fenêtre.

## `@page.Key` dans un fichier Razor est pris pour la directive `@page` (chantier stage1/5-shared-ui-language)

Une variable de boucle nommée `page` donne `@page.Key` dans le balisage, que Razor lit comme la directive
`@page` mal placée (erreurs RZ2005 et RZ9979). Nommer la variable autrement (`item`).

## Lancer l'hôte Web à la main sans profil : environnement `Development` obligatoire (chantier stage1/5-shared-ui-language)

`dotnet run --no-launch-profile` démarre en `Production`, où `MapStaticAssets` cherche les fichiers publiés
(`blazor.web.js`, `_content/...`) dans `wwwroot` et ne les trouve pas : la page reste sans interactivité.
Ajouter `ASPNETCORE_ENVIRONMENT=Development` et lancer depuis le dossier du projet (racine du contenu).

## Deux `onnxruntime` natifs : celui de sherpa-onnx et celui de `Microsoft.ML.OnnxRuntime` (chantier stage1/9-wake-word)

`org.k2fsa.sherpa.onnx` 1.13.8 livre son propre `onnxruntime.dll` / `libonnxruntime.so` (version 1.28.2), et
`Microsoft.ML.OnnxRuntime` 1.32.0 un autre, sous le même nom. Dans un même dossier de sortie, le second
écrase le premier. Sous Linux, `libsherpa-onnx-c-api.so` exige alors le symbole `OrtGetApiBase@VERS_1.28.2`
absent de la 1.32 : Piper et Silero ne se chargent plus (« Unable to load shared library
'sherpa-onnx-c-api' »). Solution du chantier 9 : le mot d'éveil prend `Microsoft.ML.OnnxRuntime.Managed`
**1.28.0** (la partie .NET seule) et s'appuie sur le moteur natif 1.28.2 de sherpa-onnx ; un seul
`onnxruntime` dans le processus. Une version managée plus récente que le natif échoue (`GetApi` rend null) :
monter l'une demande de monter l'autre.

## Un cercle de dépendances par fabriques fige le démarrage sans erreur (recette de l'étape 2)

Le conteneur ne détecte un cercle qu'entre des constructeurs. Avec des fabriques
(`provider => new X(provider.GetRequiredService<Y>())`), il descend jusqu'à saturer la pile, passe sur un
autre fil et attend le verrou du premier : `host.StartAsync()` ne rend jamais la main, sans exception ni ligne
au journal. Vu à la recette : serveur local → `IClaudeApprovals` → lanceur des sessions → `IClaudeCode` →
serveur local. Ni le mot d'éveil, ni les serveurs MCP, ni le premier lancement ne démarraient, alors que l'orbe
répondait. L'hôte Web ne le voyait pas : son `IClaudeCode` simulé n'a pas besoin du serveur local.

Règle : un adaptateur qui a besoin d'un service fourni par une tranche le demande **au moment de l'appel**
(`Func<T?>`), pas à sa construction. Le test `AdaptersRegistrationTests` construit les services de fond avec les
vrais adaptateurs et une limite de temps.

Même source : un objet rendu par plusieurs inscriptions à fabrique (lui-même, son interface, le service de
fond) est libéré une fois par inscription. Son `DisposeAsync` doit supporter plusieurs appels.

## La fin d'un composant n'est pas un geste de l'utilisateur (étape 2, test de l'éventail en échec sur l'intégration continue seulement)

La fenêtre de confirmation refusait sa demande dans `Dispose`, en lisant la fin du composant comme « l'utilisateur
a fermé la fenêtre ». Dans l'hôte Web, les dialogues vivent dans l'hôte : **chaque** page connectée les dessine,
même une page dont le navigateur est parti et dont le serveur n'a pas encore abandonné la connexion. Quand le
serveur mettait fin à une telle connexion pendant qu'une demande était ouverte, ses composants étaient libérés
et la demande était refusée sans aucun clic. Sur GitHub, les pages des tests précédents étaient abandonnées plus
tard que sur un poste, donc un test n'échouait que là : la fenêtre « n'apparaissait jamais » ou avait disparu un
instant plus tard. La même chose arriverait quand `LanguageScope` reconstruit le contenu pour une autre langue.

Règle : un composant ne répond jamais à la place de l'utilisateur dans `Dispose`. Ce que l'utilisateur ferme est
dit par l'hôte (`IDialogService.Closed`). Et dans l'hôte Web, se rappeler qu'un composant peut exister une fois
par page connectée.
