# Mémoire du projet

Point de reprise pour tout agent. À mettre à jour dans le même changement que
ce qu'il décrit.

## État

- **Série en cours :** `spikes` (essais techniques E1 à E10), branche `spikes/base`.
  Faits le 18 novembre 2026 : E2 à E9. E1 abandonné (pas de 3CX, étape 5 retirée). En attente : E10
  (téléphone Android de Benoit). Synthèse : [dev/spikes.md](../dev/spikes.md).
- **Prochain numéro d'ADR libre :** 0001.
- **Décisions prises mais pas construites :** tout le document de conception.
- **Décisions ouvertes issues des essais :** orbe en veille (E6), modèle local par défaut (E7, qwen3:14b
  recommandé), voix par défaut (E4, Piper recommandé), seuil du mot d'éveil (E4, à essayer avec la voix de Benoit).
- **Échecs de tests connus :** aucun (pas encore de tests).

## Environnement de développement de Benoit

- GPU : NVIDIA RTX 3060 12 Go. Avec un modèle 14B et Whisper turbo, il reste environ 600 Mio.
- Ollama tourne dans Docker sur le port **11435** (pas 11434). Modèles : `local-ia-active` (qwen2.5-coder 14B),
  `qwen3:14b` et `qwen2.5:14b` (ajoutés pour E7).
- llama-server sur `127.0.0.1:8080`.
- Les secrets, modèles et artefacts jetables vont dans `C:\jarvis\temp` (modèles dans `C:\jarvis\temp\models`),
  jamais dans le dépôt.
