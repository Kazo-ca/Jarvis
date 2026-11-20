# Hôtes

2026-11-19 · Chantier `stage1/4-hosts`. S'appuie sur l'[ADR 0003](adr/0003-hosts-and-native-services.md)
(hôtes et services natifs) et l'[ADR 0013](adr/0013-relais-readiness.md), section 4 (hôte générique .NET).

## Contrats du bureau

Dans le noyau, `Kazo.Jarvis.Kernel.Desktop`. Aucun ne nomme un type de plateforme.

| Contrat | Sert à | Hôte Windows | Hôte Web |
| --- | --- | --- | --- |
| `IOrbWindow` | Afficher, cacher, basculer l'orbe | `OrbWindowService` : fenêtre WPF transparente | `SimulatedOrbWindow` : l'orbe dans la page |
| `IDialogService` | Ouvrir un composant dans sa fenêtre, une par clé | `WindowDialogService` : une fenêtre et une vue web par clé, même fournisseur de services | `OverlayDialogService` : un calque par-dessus la page |
| `IGlobalHotkey` | Enregistrer le raccourci global et recevoir ses appuis | `Win32GlobalHotkey` : `RegisterHotKey` sur une fenêtre de messages | `SimulatedGlobalHotkey` : un bouton de la page |
| `ILaunchAtLogin` | Démarrage avec Windows | `RegistryLaunchAtLogin` : valeur `Kazo.Jarvis` de la clé `Run` de l'utilisateur | `InMemoryLaunchAtLogin` |

`HotkeyGesture` lit et écrit un raccourci comme dans les réglages (`Ctrl+Alt+J`). Ctrl, Alt ou Win est
obligatoire, pour qu'une saisie dans une autre application ne le déclenche jamais par hasard.

La zone de notification n'a pas de contrat : aucune tranche n'en a besoin (ADR 0003, section 5, « un contrat
est ajouté quand une tranche en a besoin »). Son menu publie `SettingsWindowRequested` et
`ConversationWindowRequested` (`Kernel.Messages.Desktop`), « Mises à jour… » publie
`UpdatesWindowRequested` ([updates.md](updates.md)), et « Assistant de configuration… » publie `FirstRunWizardRequested` ([first-run.md](first-run.md)) ; une entrée reste grisée tant qu'aucune tranche ne
traite son événement. « Paramètres » est actif depuis le chantier 5 (tranche `Settings/Window`, voir
[interface partagée](shared-ui.md)) ; « Conversation » est actif depuis le chantier 13 ([conversation](conversation.md)). « Couper le micro » est actif depuis le chantier 8 et coché tant que le micro est coupé ([audio](audio.md)).
« Résoudre le problème… » publie `FaultWindowRequested` ; il n'apparaît, en tête du menu, que pendant une panne
(chantier 15, [pannes](faults.md)).
Les textes du menu suivent la langue de l'assistant : ils sont relus à chaque ouverture du menu.

## Tranches

| Tranche | Réglage | Rôle |
| --- | --- | --- |
| `Features/Desktop/Hotkey` | `Desktop.Hotkey` : `gesture`, par défaut `Ctrl+Alt+J` | Enregistre le raccourci au démarrage et à chaque changement du réglage ; un appui publie `ActivationRequested(Hotkey)` |
| `Features/Desktop/LaunchAtLogin` | `Desktop.LaunchAtLogin` : `enabled`, par défaut `true` (hypothèse n° 3) | Applique le réglage au démarrage et à chaque changement |

Les deux sont des services de fond (`IHostedService`) : l'hôte Windows les démarre après l'affichage de
l'orbe, l'hôte Web au démarrage de son application.

## Hôte Windows

### Démarrage

1. **Instance unique** : un mutex nommé `Local\Kazo.Jarvis.Instance` (par session Windows). Un second
   lancement signale l'événement `Local\Kazo.Jarvis.ShowOrb`, ce qui affiche l'orbe de l'instance déjà
   ouverte, puis se termine.
2. **Hôte générique** : `Host.CreateApplicationBuilder` sans les réglages par défaut (pas
   d'`appsettings.json` ni de console : `config.json` reste le seul fichier de réglages, ADR 0004). Le journal
   technique va dans `%USERPROFILE%\.kazo\jarvis\logs\jarvis-AAAA-MM-JJ.log`, gardé 14 jours. La durée de vie
   est celle de l'application WPF (`DesktopLifetime`), pas celle d'une console.
3. **WebView2** : s'il manque, une boîte de dialogue le dit et propose la page de téléchargement, puis
   l'application se ferme.
4. **Zone de notification** : l'icône apparaît tout de suite, même sur une session verrouillée, ce qui
   permet de quitter.
5. **Orbe** : ouvert dès que le bureau de la session peut l'afficher (voir plus bas).
6. **Services de fond** : `IHost.StartAsync` part sur un fil de travail quand l'orbe est dessiné pour la
   première fois, sans bloquer le fil d'interface.

**Quitter** cache l'icône, ferme les fenêtres, arrête les services de fond par `StopAsync` en 10 secondes au
plus, puis termine l'application.

### Session verrouillée

Sur une session verrouillée, Direct3D 9 n'est pas disponible et `WebView2CompositionControl` ne peut pas créer
son périphérique ([pièges](pitfalls.md)). L'hôte ne crée donc pas l'orbe tant que la session est verrouillée
(`WTSQuerySessionInformation`, `WTSSessionInfoEx`) ou que le bureau qui reçoit les entrées n'est pas `Default`
(bureau sécurisé d'une demande d'identification, session à distance déconnectée). Le nom du bureau seul ne
suffit pas : sous Windows 10 et 11, l'écran de verrouillage tourne sur le bureau `Default` (constaté sur le PC
de Benoit). Il le vérifie chaque seconde,
attend que le bureau soit prêt depuis 2 secondes, puis ouvre l'orbe. Pendant l'attente :

- une ligne d'avertissement dans le journal dit pourquoi l'orbe n'est pas là ;
- l'info-bulle de l'icône dit « en attente du déverrouillage de la session ».

Si la vue web perd quand même Direct3D plus tard (verrouillage pendant le dessin, sortie de veille :
`D3DERR_NOTAVAILABLE` ou `D3DERR_DEVICELOST`), l'erreur est attrapée, journalisée, la fenêtre de l'orbe est
fermée et une nouvelle s'ouvre quand le bureau est de nouveau prêt. Toute autre erreur non attrapée est
journalisée, puis une boîte de dialogue donne le chemin du journal avant l'arrêt : jamais de plantage muet.

### Fenêtre de l'orbe

- Transparente (`AllowsTransparency`), sans bordure, toujours au premier plan, hors de la barre des tâches et
  d'Alt+Tab (`WS_EX_TOOLWINDOW`), sans prise de focus (`ShowActivated = false`, `WS_EX_NOACTIVATE`).
- 160 × 160 pixels indépendants ; l'orbe de la tranche de l'orbe ([orb.md](orb.md)) la remplit, avec une
  marge pour son halo. Les pixels transparents autour laissent passer les clics.
- Un appui suivi d'un glissement déplace la fenêtre (`DragMove`) ; la position est enregistrée dans la
  section `Desktop.OrbWindow` de `config.json` (`screenX`, `screenY`), propre à l'hôte, **en pixels physiques
  de l'écran** : en pixels indépendants, WPF la convertit avec l'échelle de l'écran où la fenêtre est créée,
  et l'orbe dérivait à chaque relance avec des écrans d'échelles différentes ([pièges](pitfalls.md)). Au
  démarrage, la fenêtre est placée par `SetWindowPos` avant d'être affichée ; une position hors de toutes
  les zones de travail est remplacée par le coin inférieur droit de l'écran principal.
- Un appui sans déplacement (moins de 4 pixels) est un clic. La fenêtre attrape l'appui elle-même pour
  distinguer un clic d'un déplacement (la vue web ne le reçoit jamais) et passe le clic à `OrbActivation` de
  la tranche de l'orbe, qui publie `ActivationRequested(Click)` : c'est le même chemin que le clic sur le
  composant dans l'hôte Web.
- La vue web a un fond transparent (`DefaultBackgroundColor` et `WEBVIEW2_DEFAULT_BACKGROUND_COLOR`).
- Le profil WebView2 de toutes les fenêtres va dans `%USERPROFILE%\.kazo\jarvis\webview`, pas à côté de
  l'exécutable (le dossier d'installation n'est pas forcément accessible en écriture). Ce dossier n'est pas
  dans la liste de l'ADR 0004 : c'est WebView2 qui le crée.

### Démarrage avec Windows

Valeur `Kazo.Jarvis` de `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, réécrite au démarrage si
l'exécutable a changé de place. **Une construction de développement** (lancée depuis `bin\Debug` ou
`bin\Release`) **ne s'inscrit pas**, pour qu'essayer une branche ne la laisse pas démarrer à chaque
ouverture de session. Pour vérifier l'inscription depuis une telle construction : variable d'environnement
`KAZO_JARVIS_REGISTER_STARTUP=1`. Couper le réglage retire la valeur dans tous les cas.

## Hôte Web

La page d'accueil montre l'orbe, nourri par la machine à états, à la taille de la fenêtre de l'hôte Windows,
un lien vers la [page de démonstration de l'orbe](orb.md#page-de-démonstration) (`/orb`) et des boutons : simuler le raccourci, afficher ou cacher l'orbe,
ouvrir un dialogue d'essai. Elle expose la phase de l'assistant dans `data-phase` et `data-interactive="true"`
quand le circuit interactif est prêt (la page est d'abord rendue côté serveur : un clic plus tôt est perdu).
Les dialogues s'affichent sous les boutons, qui restent utilisables.

## Vérifications sur Windows

Ce que l'hôte Web ne prouve pas (plan de l'étape 1, chantier 4) :

1. Les clics autour de l'orbe atteignent l'application dessous.
2. Le raccourci marche quand une autre application a le focus.
3. Un second lancement montre l'orbe déjà ouvert, sans seconde icône.
4. La position revient au redémarrage.
5. Coût de la fenêtre immobile (méthode de l'essai E6).
6. Lancé sur une session verrouillée, l'hôte attend et ouvre l'orbe au déverrouillage.
7. Quitter arrête le processus en moins de 10 secondes.
8. Chantier 5 : la fenêtre des paramètres ouverte, choisir l'autre langue recharge ses textes sans
   redémarrer (titre de la page, onglet, aide), et le menu de la zone de notification est traduit à
   l'ouverture suivante. Les polices (Rajdhani pour les titres) s'affichent sans connexion à Internet.
9. Chantier 6 : la [liste de l'orbe](orb.md#vérifications-sur-windows).
10. Chantier 14 : la [liste du premier lancement](first-run.md#vérifications-sur-windows).
