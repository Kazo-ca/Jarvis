# E10 : audio entre téléphone et PC, batterie — en attente du téléphone

Le coût en batterie de l'écoute permanente ne se mesure que sur l'appareil, sur plusieurs heures. Cet essai
ne bloque que l'étape 4.

## Ce qu'il faut prouver

1. Le transport audio entre l'application MAUI et le serveur de l'hôte Windows : WebSocket ou WebRTC.
2. Le coût en batterie de l'écoute permanente (mot d'éveil sur le téléphone), avec et sans « seulement en charge ».

## Piste recommandée avant l'essai

- **WebSocket** d'abord : le serveur intégré est en ASP.NET Core, l'audio part en blocs de 20 ms
  (PCM 16 kHz, ou Opus pour économiser le réseau), et le même canal porte les états de l'orbe et les
  notifications. WebRTC n'apporte un gain que pour l'écho et la gigue sur un réseau lointain (VPN).
- Mot d'éveil sur le téléphone : le même modèle ONNX que sur le PC (E4), par ONNX Runtime pour Android.

## Préparer le téléphone (à faire par Benoit)

Le PC a déjà ce qu'il faut : `adb` (Android SDK de Visual Studio) et la charge de travail .NET `android`.

1. **Activer les options pour les développeurs** : Paramètres, À propos du téléphone, toucher 7 fois
   « Numéro de build ».
2. **Activer le débogage** : Paramètres, Système, Options pour les développeurs :
   - « Débogage USB » pour un câble, ou
   - « Débogage sans fil » si le téléphone et le PC sont sur le même Wi-Fi (Associer l'appareil avec un
     code ; donner l'adresse IP, le port et le code de six chiffres).
3. **Brancher le téléphone au PC** et accepter l'empreinte de l'ordinateur sur l'écran du téléphone.
4. **Laisser le téléphone branché et déverrouillé** au début de l'essai ; je vérifierai qu'il apparaît avec
   `adb devices`.
5. **Pour la mesure de batterie** : le débrancher, le charger à 100 %, laisser l'application tourner une nuit,
   puis le rebrancher. Je lirai la consommation avec `adb shell dumpsys batterystats`.

Ce que j'installerai : une application de développement « Jarvis E10 » (désinstallable), qui demande la
permission du micro et affiche une notification permanente pendant l'écoute. Rien d'autre sur le téléphone
n'est lu ni modifié.
