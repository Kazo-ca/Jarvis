# E1 : Jarvis comme poste SIP sur le 3CX gratuit — en attente

**Pas encore fait : demande Benoit.** Rien n'a été installé ni deviné.

## Ce qu'il faut prouver

Un client SIP écrit en C# (SIPSorcery) s'enregistre comme poste sur l'édition gratuite de 3CX, reçoit un
appel, en émet un et échange l'audio dans les deux sens.

## Ce qui demande Benoit

1. **Installer 3CX.** La version 20 ne s'installe plus sous Windows : il faut une machine Debian (machine
   virtuelle, Proxmox, ou petit PC). L'édition gratuite demande un compte 3CX et une clé de licence.
2. **Créer un poste pour Jarvis** (numéro, identifiant d'authentification, mot de passe SIP) et un deuxième
   poste avec un téléphone logiciel pour appeler Jarvis.
3. Déposer ces informations dans `C:\jarvis\temp\e1-3cx.env`, jamais dans le dépôt.

## Plan de l'essai, une fois 3CX prêt

1. `SIPSorcery` : `SIPRegistrationUserAgent` vers le 3CX, journal de l'enregistrement (réponse 200 ou refus).
2. Appel entrant depuis le téléphone logiciel : décrocher, jouer une phrase Piper (G.711, 8 kHz), enregistrer
   ce qui est dit, transcrire avec Whisper.
3. Appel sortant de Jarvis vers le téléphone logiciel.
4. Si l'enregistrement est refusé (édition gratuite limitée aux applications de 3CX) : noter le code de
   refus et passer au repli prévu par la conception (pont SIP ou autre centrale).
