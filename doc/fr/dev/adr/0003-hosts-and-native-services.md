# ADR 0003 : hôtes (Windows WPF, Web de test, Android) et services natifs

## Statut

Proposé le 2026-11-18, en attente de l'accord de Benoit (chantier `spikes/11-stage-1-adrs`).

- **Sources :** décisions n° 4, 7, 8, 15 et 36 de la [conception](../plans/design.md) ; récits US1.1, US1.2,
  US1.3 et US1.6 ; essai E6 ([résultats](../../../../spikes/e6-orb-window/RESULTS.md)) ; hôte Windows de
  Kazo Bar.
- **Complète :** [ADR 0001](0001-target-dotnet-10-and-projects.md) et
  [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md).
- **Complété par :** [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (orbe),
  [ADR 0006](0006-voice-chain.md) (voix).

## Contexte

- Sur Windows, Jarvis est un orbe flottant, transparent, toujours au premier plan, avec une icône dans la
  zone de notification. Les paramètres et la conversation ont leurs propres fenêtres (décision n° 4).
- Kazo Bar a établi le montage : une fenêtre WPF transparente qui héberge `BlazorWebView`, rendu par
  `WebView2CompositionControl`. Les pixels d'alpha zéro laissent passer les clics vers l'application
  dessous. Ses fenêtres de dialogue ont chacune leur vue web, mais partagent le même fournisseur de services.
- L'essai E6 a montré qu'une fenêtre WPF transparente animée coûte cher : chaque image modifiée est recopiée
  par le processeur. Une image fixe coûte 1,3 % d'un cœur.
- Le téléphone est un satellite : il affiche les mêmes tranches, et tout le traitement reste sur le PC
  (décisions n° 7 et 8). L'essai E10 n'est pas fait.

## Décision

### 1. Trois hôtes, les mêmes tranches

| Hôte | Technologie | Rôle | Services | Créé à |
| --- | --- | --- | --- | --- |
| `Kazo.Jarvis.Windows` | WPF et `BlazorWebView` | Le produit sur le PC | Réels : les siens et ceux d'`Adapters` | Étape 1 |
| `Kazo.Jarvis.Web` | Application web Blazor | Hôte de test, cible de Playwright | Simulés | Étape 1 |
| `Kazo.Jarvis.Mobile` | MAUI Blazor Hybrid, Android | Satellite du PC | Les siens, plus un relais vers le PC | Étape 4 |

Un hôte ne contient que le démarrage, la composition des services et le code propre à sa plateforme. Aucune
fonctionnalité n'y est écrite.

### 2. Hôte Windows

- **Un seul processus, une seule instance par session Windows.** Un second lancement affiche l'orbe de
  l'instance déjà ouverte, puis se termine.
- **Fenêtre de l'orbe :** fenêtre WPF transparente, sans bordure, toujours au premier plan, absente de la
  barre des tâches, qui ne prend pas le focus à l'affichage. Elle héberge `BlazorWebView`
  (`WebView2CompositionControl`). Les clics autour de l'orbe atteignent l'application dessous par les pixels
  d'alpha zéro. La fenêtre se déplace, et sa position est mémorisée (US1.1).
- **Fenêtres Paramètres et Conversation :** fenêtres normales, redimensionnables, chacune avec sa vue web,
  toutes construites sur le même fournisseur de services. Un changement fait dans une fenêtre est vu par les
  autres aussitôt. Une tranche ouvre une fenêtre par un contrat de dialogue, comme dans Kazo Bar. Une seule
  fenêtre par clé.
- **Zone de notification** (US1.2), **raccourci clavier global** (US1.6), **capture et lecture audio**,
  **démarrage avec Windows** : services de l'hôte.
- **Orbe immobile en veille** (E6) : la règle d'animation est donnée par l'hôte au composant de l'orbe
  (ADR 0005).
- **Le traitement audio et les modèles tournent hors du fil d'interface.**
- **Serveur intégré** (pour le mobile et les webhooks) : il appartient à cet hôte et reste fermé par défaut.
  Il n'est pas construit à l'étape 1. Son ADR s'écrit avec la première étape qui en a besoin.
- **WebView2 :** le moteur d'exécution WebView2 est requis. S'il manque, l'hôte le dit au démarrage avec le
  lien d'installation, au lieu d'afficher une fenêtre vide.

### 3. Hôte Web de test

- Il référence `Kernel` et `Features`, jamais `Adapters`.
- Tous les contrats y sont simulés et déterministes : mot d'éveil déclenché par un bouton, transcription et
  réponse du modèle écrites d'avance, voix muette avec un niveau sonore simulé.
- Il porte la page de démonstration qui déclenche les sept états de l'orbe sans micro ni modèle (US1.3).
- Les dialogues s'y affichent par-dessus la page, puisqu'il ne peut pas ouvrir de fenêtre.
- C'est la cible des tests Playwright.

### 4. Hôte mobile

- MAUI Blazor Hybrid, Android d'abord. Créé à l'étape 4, après l'essai E10.
- Il affiche les mêmes tranches. Leurs demandes voyagent jusqu'au serveur intégré de l'hôte Windows.
- Ses services natifs (micro, haut-parleur, service Android maintenu) et le transport sont fixés par un ADR
  de l'étape 4.

### 5. Services natifs derrière des contrats

- Chaque service natif a un contrat dans le noyau. Le contrat ne nomme aucun type de plateforme (pas de
  `Window`, de poignée de fenêtre, de type NAudio).
- Chaque contrat a une mise en œuvre réelle et une mise en œuvre simulée dans l'hôte Web.
- Partage entre l'hôte Windows et `Adapters` :

| Va dans `Kazo.Jarvis.Windows` | Va dans `Kazo.Jarvis.Adapters` |
| --- | --- |
| Ce qui a besoin de WPF, de Win32 ou de la fenêtre | Ce qui enveloppe une technologie externe et se teste sans fenêtre |
| Fenêtre de l'orbe, fenêtres de dialogue, zone de notification, raccourci global, périphériques audio, démarrage avec Windows, serveur intégré | Mot d'éveil, reconnaissance, synthèse, clients des modèles, fichier de configuration, stockage, coffre, puis MCP, Claude Code, Home Assistant et Google |

- Contrats natifs de l'étape 1 :

| Contrat | Sert à |
| --- | --- |
| Fenêtre de l'orbe | Afficher, cacher, déplacer, mémoriser la position |
| Dialogues | Ouvrir un composant dans sa fenêtre (ou par-dessus la page dans l'hôte Web) |
| Zone de notification | Icône, menu, clic gauche |
| Raccourci global | Enregistrer un raccourci et recevoir son déclenchement |
| Capture audio | Lister les micros, ouvrir un flux, couper le micro |
| Lecture audio | Lister les sorties, jouer, arrêter net |
| Démarrage avec Windows | Activer, désactiver, lire l'état |

- Un contrat est ajouté quand une tranche en a besoin, pas d'avance.

## Conséquences

- L'hôte Web prouve les tranches sans matériel. Ce qu'il ne peut pas prouver se vérifie à la main sur
  Windows, et la liste de ces vérifications fait partie du plan de chaque série : passage des clics autour
  de l'orbe, raccourci global quand une autre application a le focus, micro et sortie audio réels.
- Chaque contrat natif a deux mises en œuvre à tenir à jour dès l'étape 1, et une troisième à l'étape 4.
- Le passage des clics autour de l'orbe repose sur ce que Kazo Bar a établi. L'essai E6 ne l'a pas remesuré :
  c'est la première vérification de la tranche de l'orbe.
- Le démarrage avec Windows suit l'hypothèse n° 3 de la conception, que Benoit doit encore confirmer.
- L'hôte MAUI de Kazo Bar pour Windows n'est pas repris : MAUI ne sert ici que pour Android.
