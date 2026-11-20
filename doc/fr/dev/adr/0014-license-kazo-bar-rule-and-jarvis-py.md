# ADR 0014 : licence Apache 2.0, règle sur Kazo Bar et retrait de `jarvis.py`

## Statut

Accepté par Benoit le 2026-11-19 (réponses données dans la conversation sur le document de conception).

- **Précise :** les ADR 0001 à 0004, qui citent Kazo Bar comme modèle. Leurs décisions ne changent pas.
- **Modifie :** section I d'`AGENTS.md` (règle 3) et le [document de conception](../plans/design.md)
  (décision 36, hypothèses n° 4, 7 et 8).

## Contexte

- Le document de conception disait « on garde de Kazo Bar les hôtes » et « repris de Kazo Bar ». Ces mots
  pouvaient se lire comme une dépendance vers le dépôt `Kazo-ca/bar`.
- Réponses de Benoit du 2026-11-19 : « On n'utilise pas Kazo Bar. On fait comme lui. » puis « On peut copier
  le code de Kazo Bar, mais on ne le référence pas. »
- L'hypothèse n° 7 de la conception disait « même licence que Kazo Bar ». Kazo Bar n'a pas de fichier de
  licence. Correction de Benoit du 2026-11-19 : « Le dépôt est public et sous la même licence que
  OpenJarvis. » OpenJarvis est sous licence Apache 2.0.
- L'hypothèse n° 8 prévoyait que `jarvis.py`, l'ancien assistant vocal de Benoit, tourne jusqu'à la fin de
  l'étape 2. Correction de Benoit du 2026-11-19 : « `jarvis.py` est remplacé et n'est plus utilisé. »

## Décision

### 1. Kazo Bar : copie permise, aucune référence

1. Kazo.Jarvis ne référence jamais Kazo Bar : ni référence de projet, ni paquet, ni sous-module Git, ni
   fichier lié depuis un autre dépôt.
2. Le code de Kazo Bar peut être copié dans ce dépôt quand il convient. La copie est adaptée aux règles
   d'ici : tranches verticales (ADR 0002), noms `Kazo.Jarvis.*`, tests au même chemin.
3. Le code copié devient du code de Kazo.Jarvis. Il évolue séparément et rien ne le resynchronise avec
   Kazo Bar.
4. Aucun module Jarvis n'est ajouté dans Kazo Bar.
5. Dans les docs, Kazo Bar se cite comme un modèle (« comme dans Kazo Bar », « calqué sur »). On n'écrit plus
   « repris de » ni « on garde de », qui laissent croire à une dépendance.

### 2. Licence Apache 2.0

1. Le dépôt est sous licence Apache 2.0, la même qu'OpenJarvis. Le fichier `LICENSE` est à la racine.
2. Le dépôt est destiné à être public. Le changement de visibilité sur GitHub revient à Benoit.
3. Les licences des composants tiers restent à régler **avant** une diffusion publique d'un paquet :
   espeak-ng (GPLv3, dans sherpa-onnx 1.13.8), modèles d'openWakeWord (CC BY-NC-SA 4.0) et licence de chaque
   voix (ADR 0006). Cet ADR ne les règle pas.

### 3. `jarvis.py` est retiré

1. `jarvis.py` (`C:\jarvis\jarvis.py`, hors dépôt) n'est plus utilisé. Il n'y a pas de période de transition.
2. Il n'est ni une référence de comportement, ni une dépendance. La référence reste OpenJarvis
   (`AGENTS.md`).
3. Rien dans Kazo.Jarvis ne lit sa configuration ni ses fichiers.

## Conséquences

- Les tests d'architecture peuvent vérifier qu'aucun projet ne référence un assemblage `Kazo.Bar.*`. Cette
  vérification s'ajoute au chantier `stage1/2-kernel` ou au suivant qui touche ces tests.
- Un correctif fait dans Kazo Bar n'arrive pas seul dans Kazo.Jarvis : il faut le recopier à la main.
- Tant que `jarvis.py` n'est plus utilisé et que l'étape 1 n'est pas livrée, Benoit n'a pas d'assistant vocal
  en service.
- Le dépôt public expose son historique : aucun secret ne doit s'y trouver (`AGENTS.md`, section II, règle 5).
