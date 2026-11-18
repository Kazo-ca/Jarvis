# Essais techniques

Synthèse des dix essais du [document de conception](plans/design.md#essais-techniques-à-faire-avant-de-coder),
faits le 18 novembre 2026 sur le PC de Benoit (RTX 3060 12 Go, .NET 10). Le détail, les mesures et la façon
de relancer chaque essai sont dans `spikes/<nom>/`.

| Essai | Verdict | En une phrase | Détail |
| --- | --- | --- | --- |
| E1 3CX | **Abandonné** | Benoit a retiré la téléphonie 3CX : seulement l'application MAUI. | [e1-3cx-sip](../../../spikes/e1-3cx-sip/README.md) |
| E2 sessions Claude Code | **Réussi** | Le registre `~/.claude/sessions/<pid>.json` donne chaque session et son état (`busy`, `idle`, `waiting`), lu en 73 ms. | [e2-claude-sessions](../../../spikes/e2-claude-sessions/RESULTS.md) |
| E3 Claude Desktop | **Réussi** | Ses sessions de code sont des processus Claude Code inscrits au même registre. | [e2-claude-sessions](../../../spikes/e2-claude-sessions/RESULTS.md) |
| E4 chaîne vocale | **Réussi, deux réserves** | 1,6 à 1,8 s entre la fin de la phrase et le premier son, en C# sans Python ; « hey jarvis » à la française mal reconnu ; Kokoro lent sur processeur. | [e4-voice-chain](../../../spikes/e4-voice-chain/RESULTS.md) |
| E5 mémoire GPU | **Réussi, marge mince** | Whisper turbo et le modèle 14B tiennent ensemble : 11,7 Go sur 12,3. Whisper sur processeur trop lent. | [e4-voice-chain](../../../spikes/e4-voice-chain/RESULTS.md) |
| E6 orbe WPF | **Échec sur la cible** | Orbe animé : 6 à 18 % d'un cœur avec WebView2, 4 à 7 % en WPF pur. Image fixe : 1,3 %. | [e6-orb-window](../../../spikes/e6-orb-window/RESULTS.md) |
| E7 outils du modèle local | **Réussi, changer de modèle** | qwen3 14B (réflexion coupée) : 42 sur 42, 0,9 s. qwen2.5-coder écrit ses appels en texte. | [e7-local-tool-calls](../../../spikes/e7-local-tool-calls/RESULTS.md) |
| E8 piloter Claude Code | **Réussi** | `claude -p` en flux JSON depuis C#, sans SDK : lecture faite, écritures non autorisées refusées et signalées. | [e8-drive-claude-code](../../../spikes/e8-drive-claude-code/RESULTS.md) |
| E9 événements Home Assistant | **Non par MCP, oui par WebSocket** | MCP sans notifications ; `subscribe_trigger` par l'API WebSocket fonctionne (4 déclenchements sur 4). | [e9-home-assistant-events](../../../spikes/e9-home-assistant-events/RESULTS.md) |
| E10 audio mobile | **En attente** | Demande un téléphone Android et une nuit de mesure batterie. | [e10-mobile-audio](../../../spikes/e10-mobile-audio/README.md) |

## Décisions à prendre avant les ADR de l'étape 1

1. **Orbe en veille** (E6) : veille immobile et animation seulement quand Jarvis écoute ou parle, ou bien
   une veille animée à environ 6 % d'un cœur, ou bien un orbe natif GPU pour le bureau.
2. **Modèle local par défaut** (E7) : qwen3:14b avec la réflexion coupée.
3. **Voix par défaut** (E4) : Piper, Kokoro en option tant qu'il n'est pas accéléré par le GPU.
4. **Mot d'éveil** (E4) : à essayer avec la voix de Benoit avant de choisir le seuil ou un modèle entraîné.
