# E10 : audio entre téléphone et PC, batterie — en attente

**Pas encore fait : demande un téléphone Android.** Le coût en batterie de l'écoute permanente ne se mesure
que sur l'appareil, sur plusieurs heures. Cet essai ne bloque que l'étape 4.

## Ce qu'il faut prouver

1. Le transport audio entre l'application MAUI et le serveur de l'hôte Windows : WebSocket ou WebRTC.
2. Le coût en batterie de l'écoute permanente (mot d'éveil sur le téléphone), avec et sans « seulement en charge ».

## Piste recommandée avant l'essai

- **WebSocket** d'abord : le serveur intégré est en ASP.NET Core, l'audio part en blocs de 20 ms
  (PCM 16 kHz, ou Opus pour économiser le réseau), et le même canal porte les états de l'orbe et les
  notifications. WebRTC n'apporte un gain que pour l'écho et la gigue sur un réseau lointain (VPN).
- Mot d'éveil sur le téléphone : le même modèle ONNX que sur le PC (E4), par ONNX Runtime pour Android.

## Ce qui demande Benoit

Un téléphone Android pour installer une version de développement, et une nuit de mesure batterie.
