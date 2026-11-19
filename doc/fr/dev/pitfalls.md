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

## Une garde `OperatingSystem.IsWindows()` ne couvre pas un lambda (chantier stage1/3-settings-storage-vault)

L'analyseur CA1416 signale un type `[SupportedOSPlatform("windows")]` créé dans un lambda d'enregistrement
(`services.TryAddSingleton(provider => new CredentialManagerVault())`), même à l'intérieur d'un
`if (OperatingSystem.IsWindows())` : le lambda s'exécute plus tard, hors de la garde. Mettre ces
enregistrements dans une méthode marquée `[SupportedOSPlatform("windows")]`, appelée sous la garde.

## NUnit 4 ignore les tests `[SupportedOSPlatform("windows")]` ailleurs (chantier stage1/3-settings-storage-vault)

Sous Linux, un test ou une classe marqué `[SupportedOSPlatform("windows")]` est ignoré (« Skipped »), sans
échec. Les tests Windows portent aussi `[Category("Windows")]`, pour que l'intégration continue les écarte
explicitement et que l'agent sur le PC de Benoit puisse les lancer seuls (`--filter "TestCategory=Windows"`).
