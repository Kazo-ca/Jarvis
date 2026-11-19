# Mémoire du projet

Point de reprise pour tout agent. À mettre à jour dans le même changement que
ce qu'il décrit.

## État

- **Série en cours :** `spikes` (essais techniques E1 à E10), branche `spikes/base`.
  Faits le 18 novembre 2026 : E2 à E9. E1 abandonné (pas de 3CX, étape 5 retirée). En attente : E10
  (téléphone Android de Benoit). Synthèse : [dev/spikes.md](../dev/spikes.md).
- **Prochain numéro d'ADR libre :** 0011. Les ADR 0001 à 0009 (étape 1) sont **acceptés** par Benoit le
  19 novembre 2026 (chantier `spikes/11-stage-1-adrs`). Ils ne se modifient plus : un nouvel ADR les remplace.
- **ADR 0010 (éventail des tâches et clic sur l'orbe) : proposé** le 19 novembre 2026 (chantier
  `spikes/12-task-fan-adr`), en attente de l'accord de Benoit. Benoit a choisi la maquette « éventail », sur
  l'ordinateur seulement, l'orbe dans n'importe quel coin de l'écran : décision ambre, bureau assombri,
  animation à l'ouverture et à la fermeture seulement. Réponse de Benoit du 19 novembre 2026 sur le clic :
  **le double-clic ouvre l'éventail, le simple clic lance l'écoute** (US1.6 et l'ADR 0005 ne changent pas).
- **Essai à faire au début de l'étape 2 :** E11, coût et fluidité de la fenêtre transparente de l'éventail
  (ADR 0010).
- **ADR à venir pendant l'étape 1 :** l'orbe, sur maquette (renvoyé par l'ADR 0005) : couleurs et dessin des
  six aspects, valeurs du cyan et de l'ambre, signal d'une décision en attente quand l'éventail est fermé.
- **Décisions prises mais pas construites :** tout le document de conception.
- **Décisions issues des essais (prises le 18 novembre 2026, reprises dans les ADR 0003 à 0009) :** voir
  [dev/spikes.md](../dev/spikes.md#décisions-prises-le-18-novembre-2026). Orbe immobile en veille, `qwen3:14b`
  sans réflexion, Piper par défaut, E10 reporté avant l'étape 4.
- **Prochaine étape :** écrire le plan de la série de l'étape 1, puis coder. Benoit décide quand
  `spikes/base` va dans `main`.
- **Réponses de Benoit du 19 novembre 2026 (inscrites dans les ADR) :** base SQLite pour la conversation, la
  mémoire et le journal (ADR 0004) ; abstractions `Microsoft.Extensions.AI` (ADR 0007) ; un clic exigé pour la
  domotique de sécurité seulement (ADR 0009) ; hypothèses n° 1, 2, 3, 7 et 9 de la conception acceptées ; les
  données vont dans `%USERPROFILE%\.kazo\jarvis`, pas dans `%APPDATA%` (le nom du dossier a été choisi par
  l'agent ; le changer demande un nouvel ADR).
- **À vérifier au début de l'étape 1** (les essais ne l'ont pas fait) : mot d'éveil avec la voix de Benoit et
  pendant que Jarvis parle, micro réel, fin de phrase par Silero, passage des clics autour de l'orbe, coût de
  l'orbe en veille sur l'hôte réel, rechargement des fenêtres au changement de langue.
- **Licences à régler avant une diffusion publique :** espeak-ng (GPLv3, dans sherpa-onnx 1.13.8), modèles
  d'openWakeWord (CC BY-NC-SA 4.0), licence de chaque voix (ADR 0006).
- **Échecs de tests connus :** aucun (pas encore de tests).

## Environnement de développement de Benoit

- GPU : NVIDIA RTX 3060 12 Go. Avec un modèle 14B et Whisper turbo, il reste environ 600 Mio.
- Ollama tourne dans Docker sur le port **11435** (pas 11434). Modèles : `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` et `qwen2.5:14b` (ajoutés pour E7).
- llama-server sur `127.0.0.1:8080`.
- Les secrets, modèles et artefacts jetables vont dans `C:\jarvis\temp` (modèles dans `C:\jarvis\temp\models`),
  jamais dans le dépôt.
