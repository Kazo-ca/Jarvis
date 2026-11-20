# Périphériques audio

Chantier `stage1/8-audio-devices` (US1.13, ADR [0003](adr/0003-hosts-and-native-services.md) et
[0006](adr/0006-voice-chain.md)) : le micro, la sortie audio et la coupure du micro.
English: [../../en/dev/audio.md](../../en/dev/audio.md).

## Ce qui est construit

```
micro ─► IAudioCapture (hôte) ─► MicrophoneService (tranche) ─┬─► MicrophoneFeed (noyau) ─► mot d'éveil, transcription
                                                              └─► ISoundLevelSink (noyau) ─► orbe
OutputBinder (tranche) ─► IAudioPlayback.SelectOutput (hôte) ◄─ voix, page de test (chantier 12)
```

### Noyau (`Kernel/Audio`, `Kernel/Messages/Audio`)

| Type | Rôle |
| --- | --- |
| `IAudioCapture` | Contrat natif : lister les micros, ouvrir un flux (`IAudioCaptureStream`), suivre les branchements. Le flux livre du **16 kHz mono en `float`** (`MicrophoneFormat`) ; le fermer libère le périphérique. |
| `IAudioPlayback` | Contrat natif : lister les sorties, choisir la sortie, jouer un `AudioClip` (mono, tout débit), tout arrêter d'un coup (US1.12). |
| `IMicrophoneSink` / `IMicrophoneSource`, `MicrophoneFeed` | Ce que capte le micro : un flux en direct et un **tampon circulaire de 30 s en mémoire**, jamais écrit sur le disque. Hors du bus, comme le niveau sonore (ADR 0002, section 5) : il sert à trois tranches (audio, mot d'éveil, transcription). |
| `MicrophoneMuteRequested` | Demande de coupure ou de rétablissement (zone de notification, page des paramètres). |

### Tranche `Features/Audio/Devices`

- **`MicrophoneService`** (service de fond) garde un seul flux ouvert sur le bon micro. Chaque changement
  (démarrage, réglage, branchement, fin d'un flux, demande de coupure) refait la même comparaison entre le
  flux ouvert et celui qu'il faut.
  - Micro choisi présent : il est ouvert. Absent : celui de Windows par défaut, jusqu'à son retour.
  - Aucun micro, ou micro qui refuse de s'ouvrir : condition « panne » avec la cause `MicrophoneMissing`,
    levée dès qu'un micro s'ouvre de nouveau. Les activations sont refusées pendant ce temps (ADR 0005).
  - Micro coupé (US1.13) : le flux est fermé, le tampon est vidé, le niveau retombe à zéro,
    `MicrophoneMuteChanged` est publié vers la machine à états, et le choix est enregistré.
  - Chaque bloc capté va dans `MicrophoneFeed` et son niveau (`MicrophoneLevel`, en décibels de −60 à 0)
    vers l'orbe.
- **`OutputBinder`** donne la sortie choisie à `IAudioPlayback` au démarrage et à chaque changement : les
  tranches qui jouent un son ne lisent pas les réglages de celle-ci.
- **Page des paramètres « Audio »** (`Audio.Devices`, après la langue, l'orbe et les modèles de langage) : micro (avec « Par défaut de
  Windows (nom) »), niveau en direct, case « Couper le micro », sortie, bouton « Tester la sortie » (deux
  notes générées, `OutputChime`).
- Réglages : section `Audio.Devices` de `config.json` (`microphoneId`, `outputId`, `microphoneMuted`).

### Hôte Windows (`Kazo.Jarvis.Windows/Audio`)

- NAudio 3.3, WASAPI en mode partagé, avec les nouvelles classes `WasapiRecorderBuilder` et
  `WasapiPlayerBuilder` (voir les [pièges](pitfalls.md)).
- **Capture :** format du périphérique (souvent 48 kHz stéréo `float`), converti par `CaptureConverter` :
  moyenne des canaux, filtre passe-bas à 7 kHz, puis interpolation vers 16 kHz. Tampon de 50 ms. Un
  micro débranché arrête le flux (`AUDCLNT_E_DEVICE_INVALIDATED`), ce qui déclenche la comparaison.
- **Lecture :** chaque clip est converti au format de mélange de la sortie (`PlaybackConverter`) et joué par
  son propre lecteur ; Windows mélange les clips simultanés. Latence de 60 ms.
- **Branchements :** `MMDeviceNotificationClient` (ajout, retrait, état, périphérique par défaut).
- Tout appel Core Audio passe par un fil du pool : les objets ne sont jamais créés sur le fil d'interface.
- **Zone de notification :** l'entrée « Couper le micro » est active et cochée tant que le micro est coupé.

### Hôte Web

`SimulatedAudioCapture` (deux micros, un son de 220 Hz dont le volume monte et descend, boutons pour
débrancher et rebrancher) et `SimulatedAudioPlayback` (silencieuse, garde la dernière sortie utilisée).

## Choix provisoires de ce chantier

Les ADR 0003 et 0006 ne les tranchent pas ; à confirmer par Benoit :

1. **La coupure du micro survit au redémarrage** (réglage `microphoneMuted`) : un micro coupé ne se rouvre
   pas tout seul.
2. **Micro choisi absent :** Jarvis écoute par celui de Windows par défaut jusqu'à son retour, sans panne.
   La panne n'est posée que s'il n'y a aucun micro.
3. **Couper le micro lève la panne « micro absent »** : rien ne manque tant que l'utilisateur ne veut pas
   du micro.
4. **Tampon d'écoute de 30 s** (environ 2 Mo).
5. **Niveau sonore en décibels**, de −60 dB (0) à 0 dB (1).
6. Conversion vers 16 kHz faite par Jarvis (filtre et interpolation), pas par le moteur audio de Windows.
7. Pas de niveau de la voix dans la lecture : il arrive avec la synthèse (chantier 12).

## Tests

- Unitaires : `MicrophoneFeedTests` (tampon circulaire), `MicrophoneServiceTests` (ouverture, choix,
  coupure, débranchement, panne, avec la vraie machine à états), `AudioPiecesTests` (niveau, son de test,
  sortie), `AudioConvertersTests` (conversions de l'hôte Windows, compilées aussi sous Linux).
- Playwright (`AudioDevicesTests`) : coupure depuis la zone de notification et depuis la page, micros
  débranchés, changement de micro et niveau, son de test sur la sortie choisie.

## Vérifications sur Windows

À faire à la prochaine séance avec Benoit (l'hôte Web ne les prouve pas) :

1. **Micro réel :** la page « Audio » liste les micros de Windows, le niveau bouge quand on parle, le choix
   d'un autre micro s'applique sans redémarrer. Noter le format du périphérique dans le journal.
2. **Micro coupé :** depuis la zone de notification (entrée cochée) puis depuis la page ; l'orbe passe en
   « micro coupé » et **Windows n'indique plus d'application qui écoute** (icône du micro dans la barre des
   tâches, Paramètres > Confidentialité > Microphone). Après une relance, le micro reste coupé.
3. **Micro débranché pendant que Jarvis tourne :** avec deux micros, Jarvis passe sur l'autre ; avec un
   seul, l'orbe passe en erreur, puis revient au rebranchement. Rien dans le journal au niveau `Error`.
4. **Sortie :** « Tester la sortie » joue les deux notes sur la sortie choisie (haut-parleurs, puis casque),
   sans craquement.
5. **Coût :** micro ouvert en veille, part d'un cœur du processus (méthode de l'essai E6), à noter dans la
   mémoire.
