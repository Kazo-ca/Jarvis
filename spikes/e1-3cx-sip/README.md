# E1 : Jarvis comme poste SIP sur le 3CX gratuit — abandonné

**Abandonné le 18 novembre 2026.** Benoit a choisi de ne pas passer par 3CX : on parle à Jarvis hors du PC
seulement par l'application MAUI Blazor Hybrid. L'étape 5 de la conception est retirée, et cet essai n'a
plus de raison d'être. Rien n'a été installé.

Si la téléphonie revient un jour, le plan était : SIPSorcery (`SIPRegistrationUserAgent`) vers un 3CX v20
sur Debian, un appel entrant et un sortant, l'audio en G.711 8 kHz vers Whisper et depuis Piper.
