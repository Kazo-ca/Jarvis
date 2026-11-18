# Mémoire du projet

Point de reprise pour tout agent. À mettre à jour dans le même changement que
ce qu'il décrit.

## État

- **Série en cours :** `spikes` (essais techniques E1 à E10 du document de conception).
- **Prochain numéro d'ADR libre :** 0001.
- **Décisions prises mais pas construites :** tout le document de conception.
- **Échecs de tests connus :** aucun (pas encore de tests).

## Environnement de développement de Benoit

- GPU : NVIDIA RTX 3060 12 Go.
- Ollama tourne dans Docker sur le port **11435** (pas 11434).
- llama-server sur `127.0.0.1:8080`.
- Les secrets et artefacts jetables vont dans `C:\jarvis\temp`, jamais dans le dépôt.
