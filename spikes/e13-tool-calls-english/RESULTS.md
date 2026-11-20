# Résultats E13 — 20 novembre 2026

**E13 : réussi.** `qwen3:14b` (réflexion coupée) choisit le bon outil **117 fois sur 117** : 39 sur 39 en
anglais, 39 sur 39 en français, 39 sur 39 avec des descriptions anglaises et des demandes françaises. Tous les
appels passent par le canal d'outils officiel d'Ollama, aucun n'est écrit en JSON dans le texte. Le paramètre
`kind` de `hand_off_to_claude` est juste 43 fois sur 45 ; les deux écarts portent sur une demande ambiguë.

## Protocole

Celui de l'essai E7 : Ollama 11435, `/api/chat`, `think: false`, température 0,2, contexte 4096, 7 outils (date
et heure, calcul, recherche web, allumer, éteindre, lire un fichier, passer la main), 13 demandes par montage, 3
essais chacune, un appel de chauffe non compté. Deux demandes ne demandent aucun outil (« Merci Jarvis, ce sera
tout », « Comment vas-tu ce matin ? » et leur traduction). Cinq demandes passent la main : trois tâches (corriger
un bogue et ouvrir une PR, écrire et tester un script, résumer les changements d'un dépôt) et deux questions
(relativité restreinte et générale, architecture d'une application hors ligne).

`hand_off_to_claude` a deux paramètres obligatoires : `kind` (énumération `question` ou `task`) et `reason`.

## Résultats

| Montage | Bon outil | Canal officiel | JSON dans le texte | `kind` juste | Moyenne | Médiane | 90e centile |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Anglais (`en`) | **39 / 39** | 33 / 33 | 0 | 13 / 15 | 1,07 s | 0,94 s | 1,75 s |
| Français (`fr`) | **39 / 39** | 33 / 33 | 0 | 15 / 15 | 1,06 s | 0,92 s | 1,59 s |
| Descriptions anglaises, demandes françaises (`mixed`) | **39 / 39** | 33 / 33 | 0 | 15 / 15 | 1,12 s | 1,08 s | 1,63 s |

33 appels attendus par montage (11 demandes avec outil × 3), 6 réponses sans outil, toutes justes. Aucun appel
n'a d'arguments vides.

## Cas ratés et remarques

- **`kind` :** « Ask Claude to summarize this week's changes in the Kazo Os repository » donne `question` 2 fois
  sur 3 (attendu `task`). La demande est ambiguë (un résumé est une réponse, mais il demande de lire un dépôt) ;
  la version française donne `task` 3 fois sur 3. Jarvis ne doit pas faire dépendre une permission de `kind` :
  c'est une indication sur la forme de la réponse, pas une classe d'action (ADR 0009).
- **Langue des arguments avec des descriptions anglaises (`mixed`) :** le modèle traduit parfois l'argument
  (« Éteins les lumières de la cuisine » → `target: "kitchen"`) et écrit le motif de passage de main en anglais.
  Le nom envoyé à Home Assistant doit rester dans la langue de la maison : avec des descriptions anglaises,
  « kitchen » ne trouverait pas la pièce « Cuisine ». Pour la recherche web, la demande est recopiée telle
  quelle (« Quel temps fera-t-il demain à Québec ? ») au lieu d'une requête courte.
- Avec les descriptions dans la langue de la demande (`en` et `fr`), les arguments sont dans la bonne langue
  (« salon », « living room », « météo Québec demain »).
- Les temps sont ceux de l'essai E7 (médiane 0,9 s) : l'anglais et le nouvel outil ne coûtent rien.

## Recommandation

- Garder la règle de l'ADR 0007 : **noms d'outils en anglais, descriptions dans la langue de l'assistant**.
  L'essai la confirme : en anglais comme en français, 100 %, et des arguments dans la langue de l'utilisateur.
  Des descriptions dans une autre langue que la demande marchent pour le choix de l'outil, mais traduisent les
  noms de pièces.
- `hand_off_to_claude(kind, reason)` est fiable dans les deux langues ; `kind` sert à choisir la cible (réponse
  à voix haute ou tâche en arrière-plan) et peut se tromper sur une demande ambiguë : la cible reste modifiable.

## Non vérifié

- Le tour complet (résultat de l'outil renvoyé au modèle, réponse finale à voix haute).
- Une longue liste d'outils : les 47 outils du serveur MCP de Home Assistant (essai E14) n'ont pas été donnés
  au modèle local.
- Les demandes mêlant les deux langues dans une même phrase.
