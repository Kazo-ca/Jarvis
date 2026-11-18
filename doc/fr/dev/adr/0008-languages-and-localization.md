# ADR 0008 : langues et localisation

## Statut

Proposé le 2026-11-18, en attente de l'accord de Benoit (chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 19, 26 et 32 de la [conception](../plans/design.md) ; récits US1.4, US1.11 et
  US1.15 ; hypothèse n° 9 ; ADR 0104 de Kazo Os (localisation).
- **Complète :** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md) (chaque tranche porte ses
  textes), [ADR 0006](0006-voice-chain.md) et [ADR 0007](0007-llm-providers-and-tool-format.md).

## Contexte

- Le français et l'anglais sont obligatoires dès la première version. Les autres langues attendent la v2.
- La langue se choisit dans l'assistant du premier lancement et se change ensuite dans les paramètres. Le
  choix s'applique à l'interface, à la reconnaissance et à la voix (US1.4).
- La personnalité par défaut est le majordome, en français et en anglais (US1.15).
- Kazo Os utilise des ressources `.resx` avec `IStringLocalizer`, l'anglais comme langue neutre, et un test
  qui compare les clés des deux langues.

## Décision

### 1. Un seul réglage de langue

- Jarvis a **un** réglage, la langue de l'assistant : `fr` ou `en`. Il fixe d'un coup :

| Ce qui suit la langue | Comment |
| --- | --- |
| Textes de l'interface | Ressources de la langue |
| Reconnaissance | Whisper reçoit la langue, sans détection automatique |
| Voix | La voix par défaut de cette langue (US1.11) |
| Personnalité | Le message système de cette langue |
| Règle donnée au modèle | « Réponds en français » ou son équivalent anglais |

- Au premier lancement, la langue proposée est celle de Windows : français si l'interface de Windows est en
  français, anglais sinon.
- Les formats (dates, heures, nombres) suivent les réglages régionaux de Windows, pas ce réglage. Un
  utilisateur en français au Québec garde ses formats.
- **Un changement de langue s'applique sans redémarrer l'application.** Les fenêtres ouvertes rechargent
  leur contenu, la reconnaissance et la voix changent au tour suivant.

### 2. Ressources

- Fichiers `.resx` et `IStringLocalizer`. **L'anglais est la langue neutre** (`Nom.resx`), le français est
  dans `Nom.fr.resx`.
- **Chaque tranche porte ses ressources**, dans son dossier. Les textes communs à trois tranches ou plus
  vont dans `Features/Shared/`.
- Aucun texte visible n'est écrit en dur dans un composant ou un message d'erreur montré à l'utilisateur.
- Clé absente en français : le texte anglais s'affiche, avec un avertissement dans le journal en mode
  débogage.
- Les hôtes ne publient que ces deux langues (`SatelliteResourceLanguages` à `en;fr`).

### 3. Voix et reconnaissance

- Chaque langue a sa voix par défaut, choisie dans la page de test des voix (US1.11). Valeurs de départ :
  `fr_FR-tom-medium` et `en_GB-alan-medium` (ADR 0006).
- Whisper travaille dans une seule langue à la fois. Une phrase dite dans l'autre langue sera mal
  transcrite : c'est le prix d'une reconnaissance plus rapide et plus sûre.
- Le mot d'éveil est « hey jarvis » dans les deux langues (hypothèse n° 9).

### 4. Personnalité

- Deux messages système par défaut, un par langue, fournis comme ressources : le majordome du film, poli,
  concis, humour sec, vouvoiement et « Monsieur » en français, son équivalent en anglais.
- Une version modifiée par l'utilisateur est enregistrée par langue dans la configuration. « Réinitialiser »
  revient au texte par défaut de la langue.

### 5. Ce qui n'est pas traduit

- Le code, les noms de fichiers, les identifiants, les noms d'outils (ADR 0007) et les journaux techniques
  restent en anglais.
- Les clés de la configuration et les noms de collections ne sont pas traduits.
- La documentation suit sa propre règle : français d'abord, anglais en miroir (`AGENTS.md`).

### 6. Tests

- Un test vérifie que chaque `.fr.resx` a toutes les clés de son `.resx`, et inversement.
- Un test Playwright change la langue dans l'hôte Web et vérifie une page de réglages et la conversation.

### 7. Version 2

- Ajouter une langue revient à ajouter des ressources, une voix par défaut, la langue de Whisper et un
  message système. Aucune tranche ne doit avoir à changer de code.

## Conséquences

- Toute tranche écrit ses textes dans les deux langues dès sa première version. Le test des clés fait partie
  du seuil de fusion.
- Une maison où l'on parle les deux langues doit en choisir une à la fois. Passer de l'une à l'autre à la
  voix n'est pas prévu dans la première version.
- Le mobile (étape 4) réutilise les mêmes ressources, puisqu'il affiche les mêmes tranches.
- Le rechargement à chaud des fenêtres est à prouver dans la première tranche qui a une page de réglages.
  S'il n'est pas fiable dans `BlazorWebView`, un nouvel ADR dira que le changement de langue de l'interface
  s'applique au prochain démarrage.
