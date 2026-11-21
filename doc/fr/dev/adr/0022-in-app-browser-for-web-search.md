# ADR 0022 : recherche web dans un navigateur interne à Jarvis (remplace en partie 0017)

## Statut

Proposé le 2026-11-20 (chantier `stage2/in-app-browser-adr`). Les questions ouvertes sont dans la section
« Questions pour Benoit ».

- **Remplace en partie :** [ADR 0017](0017-tools-classes-and-decision-point.md), section 3 : les points
  « Navigateur piloté » et « Essai E15 », la réponse n° 2 de Benoit telle qu'elle y est lue, et les deux
  conséquences sur le navigateur. Le reste de l'ADR 0017 tient : deux fournisseurs de recherche derrière un
  contrat, Brave Search dès qu'une clé est fournie, `web_search` et `fetch_url` désactivés par défaut, les
  adresses refusées, la lecture seule.
- **S'appuie sur :** [ADR 0003](0003-hosts-and-native-services.md) (WebView2 est déjà requis par l'hôte
  Windows), [ADR 0009](0009-permission-and-confirmation-model.md) (une donnée n'est jamais une instruction ;
  ce qui sort de la machine).

## Contexte

- Benoit a demandé, le 2026-11-20, si la recherche pouvait se faire « dans un navigateur accessible à
  l'utilisateur ». L'ADR 0017 l'a lu comme Edge ou Chrome installé, piloté par Playwright.
- Benoit a corrigé le même jour : « je parlais d'un browser interne à l'application. Pas d'un browser natif
  sur l'ordinateur qui peut rentrer en conflit avec ce que l'utilisateur fait ».
- L'essai E15 lancé sur Edge a été arrêté avant de rendre un résultat. Rien n'en est retenu.
- L'hôte Windows affiche déjà son interface dans WebView2 (`BlazorWebView`). Le moteur est donc sur la
  machine, sans rien installer.

## Décision

### 1. Un navigateur interne, dans une fenêtre de Jarvis

- Le fournisseur par défaut de `web_search`, et le moyen de `fetch_url`, est un **navigateur interne** : un
  contrôle WebView2 dans une fenêtre de l'hôte Windows, « Navigateur de Jarvis ».
- **Jarvis ne lance ni ne pilote jamais Edge, Chrome ou un autre navigateur de l'utilisateur.** Il n'ouvre
  aucun onglet chez lui, ne lit pas son profil et ne dépend pas de ce qu'il y fait.
- La fenêtre ne prend jamais le focus quand Jarvis l'utilise, et n'apparaît pas dans la barre des tâches tant
  que l'utilisateur ne l'a pas ouverte. Quand et comment elle se montre : question n° 1.
- **L'utilisateur peut l'ouvrir** pour voir la page que Jarvis lit, et y naviguer lui-même : adresse,
  précédent, recharger, fermer. Fermer la fenêtre ne ferme pas le navigateur ; il s'arrête seul après un
  temps sans usage, réglable.
- Le navigateur est créé à la première recherche, pas au démarrage de Jarvis.

### 2. Séparé de l'interface de Jarvis et du reste de la machine

- **Un dossier de données à lui**, sous les données de Jarvis : ni celui d'Edge, ni celui du WebView2 qui
  affiche l'interface. Une page du web ne partage ni témoins ni processus avec l'interface de Jarvis.
- **Aucun pont vers l'application :** pas d'objet de l'hôte exposé aux pages, les messages des pages vers
  l'hôte sont ignorés. Une page ne peut rien appeler dans Jarvis.
- Jarvis le pilote par l'API de WebView2 (navigation, fin de chargement, lecture du texte par un script
  court). **Pas de Playwright, pas de port de débogage ouvert.**
- Ce que le navigateur garde d'une fois à l'autre (témoins, connexions faites par l'utilisateur) : question
  n° 2.

### 3. Lecture seule, par du code

- Jarvis n'y fait que deux choses : ouvrir la page de résultats d'un moteur de recherche pour une requête, et
  lire le texte d'une page. Il ne remplit aucun autre formulaire, ne clique sur rien d'autre que des liens à
  lire et ne se connecte à rien.
- Refusés par l'hôte, quoi que demande la page : les téléchargements, les nouvelles fenêtres (le lien
  s'ouvre dans la même vue), les demandes de caméra, de micro, de position et de notifications, le lancement
  d'une autre application par un lien, l'enregistrement de mots de passe.
- **Les adresses refusées par l'ADR 0017 (machine et réseau local) le sont à chaque navigation, redirections
  et ressources de la page comprises.**
- Le texte lu est une donnée, tronquée comme tout résultat d'outil (ADR 0017, section 4). Ce que
  l'utilisateur fait lui-même dans la fenêtre n'est pas une action du modèle et ne passe pas par le point de
  décision.

### 4. Moteur de recherche

- Le moteur par défaut est choisi par l'essai E15 (section 6) ; il se règle.
- Si le moteur bloque (CAPTCHA, page de refus), l'outil rend la cause au modèle. Jarvis ne contourne pas un
  blocage. L'utilisateur peut ouvrir la fenêtre et le résoudre lui-même ; la page des outils propose aussi
  Brave Search.

### 5. Contrat et hôtes

- Contrat du noyau : « navigateur de lecture » (chercher, lire une page, état). Mise en œuvre : hôte Windows.
- Hôte Web : une version simulée, avec des pages fixes, pour les tests Playwright du produit.
- Mobile : pas à cette étape.

### 6. Essai E15, redéfini

Sur le PC de Benoit, avant le chantier `stage2/5-basic-tools`, avec un WebView2 dans une fenêtre WPF comme
celle du produit :

- blocages (CAPTCHA, mur de consentement) sur plusieurs moteurs, sur une vingtaine de requêtes chacun ;
- délai d'une recherche à froid et à chaud ; mémoire prise par le navigateur ouvert ;
- le rendu et la lecture marchent-ils quand la fenêtre est cachée ;
- la fenêtre ne prend le focus ni à sa création, ni à la navigation ;
- lecture du texte de quelques pages de résultats ; quel moteur a la page la plus stable à lire.

**Si l'essai échoue, un nouvel ADR dit quoi changer** ; Brave Search reste alors le seul fournisseur.

## Questions pour Benoit

Posées dans la conversation le 2026-11-20.

1. **Quand la fenêtre du navigateur se montre-t-elle ?** Recommandation : elle reste cachée pendant une
   recherche ; la conversation affiche les pages lues avec un bouton « Voir la page » qui l'ouvre.
2. **Que garde le navigateur d'une fois à l'autre ?** Recommandation : ses témoins (moins de murs de
   consentement et de blocages) ; l'utilisateur peut s'y connecter lui-même à un site, derrière un
   avertissement ; un bouton efface tout.

## Options écartées

- **Edge ou Chrome installé, piloté par Playwright.** Écarté par Benoit : entre en conflit avec ce que
  l'utilisateur fait.
- **Le profil habituel de l'utilisateur.** N'a plus de sens : le navigateur interne a son propre dossier de
  données. Ce qui le remplace est la question n° 2.
- **Lire les pages par de simples requêtes HTTP, sans navigateur.** Beaucoup de pages et de moteurs ne
  rendent rien d'utile sans script, et l'utilisateur ne verrait pas ce que Jarvis lit.
- **Playwright branché sur le WebView2 par un port de débogage.** Un port local par lequel tout programme de
  la machine pourrait piloter le navigateur.

## Conséquences

- Aucune dépendance de plus : pas de Playwright dans le produit, pas de navigateur à télécharger.
- Un second groupe de processus WebView2 tourne quand le navigateur est ouvert. Son coût en mémoire n'est pas
  mesuré ; l'essai E15 le mesure, et l'arrêt après un temps sans usage le borne.
- La recherche dépend de la page d'un moteur, qui change sans prévenir et peut bloquer. Brave Search reste le
  second choix.
- Un accepté de l'ADR 0017 est remplacé le jour même : il reposait sur une mauvaise lecture de la demande de
  Benoit par l'agent.
