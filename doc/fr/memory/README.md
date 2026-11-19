# Mémoire du projet

Point de reprise pour tout agent. À mettre à jour dans le même changement que
ce qu'il décrit.

## État

- **Série en cours :** `spikes` (essais techniques E1 à E10), branche `spikes/base`.
  Faits le 18 novembre 2026 : E2 à E9. E1 abandonné (pas de 3CX, étape 5 retirée). En attente : E10
  (téléphone Android de Benoit). Synthèse : [dev/spikes.md](../dev/spikes.md).
- **Prochain numéro d'ADR libre :** 0010. Les ADR 0001 à 0009 (étape 1) sont écrits et **proposés** le
  18 novembre 2026 (chantier `spikes/11-stage-1-adrs`) ; ils attendent l'accord de Benoit.
- **ADR à venir pendant l'étape 1 :** couleurs et dessin de l'orbe, sur maquette (renvoyé par l'ADR 0005).
- **Décisions prises mais pas construites :** tout le document de conception.
- **Décisions issues des essais (prises le 18 novembre 2026, reprises dans les ADR 0003 à 0009) :** voir
  [dev/spikes.md](../dev/spikes.md#décisions-prises-le-18-novembre-2026). Orbe immobile en veille, `qwen3:14b`
  sans réflexion, Piper par défaut, E10 reporté avant l'étape 4.
- **Prochaine étape :** obtenir l'accord de Benoit sur les ADR 0001 à 0009, passer leur statut à « Accepté »,
  puis écrire le plan de la série de l'étape 1. Toujours aucun code.
- **Questions posées à Benoit le 18 novembre 2026 (les ADR retiennent l'option recommandée) :**
  1. Stockage : base SQLite pour la conversation, la mémoire et le journal, ou fichiers JSON comme Kazo Bar
     (ADR 0004).
  2. Contrat des fournisseurs LLM : abstractions `Microsoft.Extensions.AI` ou contrat maison (ADR 0007).
  3. Actions qui exigent un clic, la voix ne suffisant pas : la domotique de sécurité seulement (ADR 0009).
  4. Hypothèses n° 1, 2, 3, 7 et 9 de la conception, sur lesquelles les ADR s'appuient. Réponse partielle du
     19 novembre 2026 : les données vont dans `%USERPROFILE%` (ADR 0004 : `%USERPROFILE%\.kazo\jarvis`, nom
     du dossier à confirmer). Le reste attend.
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
