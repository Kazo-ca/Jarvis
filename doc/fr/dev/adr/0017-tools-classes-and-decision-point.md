# ADR 0017 : outils de base, classe de chaque outil et point de décision des permissions

## Statut

Accepté par Benoit le 2026-11-20 (chantier `stage2/2-adrs`). Ses réponses sont dans la section « Réponses de
Benoit ». Le navigateur piloté de la section 3 reste à prouver par l'essai E15.

- **Tranche les points laissés par :** [ADR 0007](0007-llm-providers-and-tool-format.md) (section 3 : « la
  classe de risque de chaque outil est fixée par l'ADR des outils, à l'étape 2 »),
  [ADR 0009](0009-permission-and-confirmation-model.md) (sections 2 et 10 : la classe de chaque outil, et le
  point de décision « construit avec son premier outil »).
- **Récits :** US2.1 et le socle de US2.10 du [document de conception](../plans/design.md).
- **Essais :** E7 et E13 ([synthèse](../spikes.md)) : `qwen3:14b` sans réflexion choisit le bon outil 42 fois
  sur 42 en français et 117 fois sur 117 avec les descriptions en anglais.
- **Ne décide pas :** les outils d'un serveur MCP et Home Assistant ([ADR 0018](0018-mcp-client-and-home-assistant.md)),
  le passage de main ([ADR 0020](0020-hand-off-targets-and-spend-cap.md)), les sessions Claude Code
  ([ADR 0021](0021-claude-code-sessions.md)), la forme de la demande de confirmation (ADR 0009, section 3,
  construite au chantier `stage2/4-confirmation`).

## Contexte

- L'ADR 0007 fixe le format d'un outil (nom anglais stable, description dans la langue de l'assistant, schéma
  JSON) et dit que Jarvis garde la boucle d'outils : aucun appel n'est exécuté automatiquement.
- L'ADR 0009 fixe cinq classes d'actions et quatre principes, dont « le modèle propose, le code décide » et
  « refus par défaut ». Il ne dit pas où ce code se trouve ni quelle classe a chaque outil.
- Essai E14 : avec Home Assistant, la classe d'un appel dépend de l'appareil visé, pas du nom de l'outil. La
  classe se décide donc **par appel**, avec ses arguments.
- Essai E12 : une session Claude Code demande ses permissions à un outil local de Jarvis. Cette demande doit
  suivre le même chemin que celle d'un outil du modèle local.
- Le contexte du modèle local est de 4096 jetons (ADR 0007, section 5). La liste des outils et leurs
  résultats y entrent.

## Décision

### 1. Ce qu'est un outil pour Jarvis

- Le modèle reçoit des `AIFunction` (ADR 0007, section 2). Dans le noyau, chaque outil du registre porte en
  plus :
  - son **origine** : `builtin`, `mcp:<serveur>` ou `hand-off` ;
  - ses **descriptions par langue** (pour un outil intégré ; un outil MCP garde celle de son serveur) ;
  - son **classement** : du code qui reçoit les arguments d'un appel et rend une classe de l'ADR 0009 ;
  - son **réglage d'activation** (section 6).
- Le **registre des outils** est un contrat du noyau. Chaque tranche y inscrit ses outils ; la tranche
  `Conversation/Dialogue` y lit la liste à donner au modèle. Aucune tranche n'en référence une autre
  ([ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md)).
- **Noms en anglais, descriptions dans la langue de l'assistant** : la règle de l'ADR 0007 est confirmée par
  l'essai E13. Changer la langue de l'assistant change les descriptions au tour suivant.

### 2. Un seul point de décision

- Contrat du noyau : « cette action est-elle permise ? ». Il reçoit **qui** demande (outil, session ou
  tâche), **quoi** (nom et arguments exacts), et rend « permis » ou « refusé, avec la cause ».
- Sa mise en œuvre est une tranche, `Permissions/Decision`. **Toute action demandée par un modèle y passe** :
  un appel d'outil du modèle local, un appel d'outil d'une cible cloud (ADR 0020, section 4), une demande de
  permission d'une session Claude Code lancée par Jarvis (ADR 0021).
- Déroulement d'une décision :
  1. l'outil est-il connu et activé ? Sinon : refus ;
  2. le classement de l'outil rend la classe de **cet appel** ;
  3. « Lecture » et « Effet courant » : permis ;
  4. « Sensible » et « Critique » : demande de confirmation (ADR 0009, section 3), sur les canaux que la
     classe accepte ; la réponse humaine, ou le délai dépassé, décide ;
  5. « Interdite » : refus, sans question.
- **Refus par défaut à chaque pas :** arguments illisibles, classement qui échoue, outil sans classement,
  délai dépassé, interruption de l'utilisateur. Un classement ne peut jamais rendre une classe plus basse
  que celle fixée par les tableaux de cet ADR et des ADR 0018 et 0021.
- **Chaque décision est journalisée** dans `jarvis.db` : date, demandeur, outil, arguments (tronqués), classe,
  résultat, canal de la réponse s'il y en a une. Les valeurs de secrets n'y entrent jamais.
- Le refus revient au modèle comme le résultat de l'outil, donc comme une **donnée** : la cause en une phrase.
  Le modèle le dit à l'utilisateur ; il ne peut pas redemander la même action dans le même tour.

### 3. Outils intégrés de l'étape 2

| Outil | Ce qu'il fait | Classe | Activé par défaut | Chantier |
| --- | --- | --- | --- | --- |
| `get_datetime` | Date, heure et fuseau du PC | Lecture | Oui | 3 |
| `calculate` | Calcul d'une expression arithmétique, par du code (pas d'exécution de script) | Lecture | Oui | 3 |
| `list_directory` | Liste d'un dossier autorisé | Lecture | Oui (sans dossier autorisé, il ne voit rien) | 5 |
| `read_file` | Texte d'un fichier d'un dossier autorisé | Lecture | Oui (idem) | 5 |
| `web_search` | Recherche sur le web : navigateur piloté, ou Brave Search si une clé est fournie | Lecture | **Non** : la requête sort de la machine | 5 |
| `fetch_url` | Lit une page par `GET` | Lecture | **Non** : l'adresse sort de la machine | 5 |
| `hand_off_to_claude` | Passe la main (ADR 0020) | Selon `kind` (ADR 0020 et 0021) | Seulement si une cible est réglée | 9 |

- **Aucun outil intégré n'écrit de fichier ni ne lance de commande à l'étape 2.** Une écriture passe par une
  session Claude Code (ADR 0021), où chaque écriture est de classe « Sensible ».
- `read_file` et `list_directory` hors d'un dossier autorisé : classe « Interdite », refus journalisé. Le
  chemin est comparé après normalisation (ADR 0009, section 5). Ils refusent aussi, partout,
  `sessions/*.key` et `ide/*.lock` de Claude Code.
- `fetch_url` : `http` et `https` seulement, méthode `GET` seulement, sans en-tête ni corps choisis par le
  modèle, redirections suivies avec la même règle. **Les adresses de la machine et du réseau local sont
  refusées** (boucle locale, plages privées, lien local), pour qu'une page piégée ne puisse pas faire lire au
  modèle Home Assistant, le serveur local de Jarvis ou un routeur. Une liste d'exceptions se règle à la main.
- `web_search` : derrière un contrat du noyau, avec deux fournisseurs (réponse n° 1) :
  - **navigateur piloté**, sans clé ni coût, celui par défaut ;
  - **Brave Search**, par son API, utilisé dès que l'utilisateur a mis sa clé dans le coffre
    ([ADR 0004](0004-configuration-storage-and-secret-vault.md)).
- **Navigateur piloté** (réponse n° 2) :
  - Jarvis pilote Edge ou Chrome déjà installé, par Playwright pour .NET, sans télécharger de navigateur ;
    le service est natif, derrière un contrat du noyau, dans `Kazo.Jarvis.Adapters` ;
  - la fenêtre est **visible**, dans un **profil à part réservé à Jarvis** : aucun compte de l'utilisateur
    n'y est connecté ;
  - **lecture seulement** : ouvrir la page de résultats d'un moteur de recherche, lire le texte d'une page.
    Jarvis n'y remplit aucun formulaire autre que la recherche, ne s'y connecte à rien et ne télécharge
    rien. `fetch_url` peut passer par ce navigateur, avec les mêmes refus d'adresses ;
  - un réglage permet d'utiliser **le profil habituel de l'utilisateur**. Il est coupé par défaut et
    s'accompagne d'un avertissement : Jarvis voit alors les comptes ouverts (courriel, banque), et une page
    piégée peut tromper le modèle pendant qu'il lit. La lecture seule vaut aussi dans ce mode ;
  - le texte lu est une donnée, tronquée comme tout résultat (section 4).
- **Essai E15, avant le chantier 5 :** sur le PC de Benoit, prouver que la recherche par navigateur tient :
  blocages (CAPTCHA) sur plusieurs moteurs, délai, lecture des résultats quand la page change, fenêtre qui ne
  prend pas le focus. **Si l'essai échoue, un nouvel ADR dit quoi changer** ; Brave Search reste alors le seul
  fournisseur.
- L'écran qui active `web_search` ou `fetch_url` dit ce qui sort de la machine (ADR 0009, section 9), et les
  deux entrent dans la liste des connexions sortantes de la recette.

### 4. La boucle d'outils

- La tranche `Conversation/Dialogue` exécute les appels **un par un**, dans l'ordre demandé, chacun par le
  point de décision.
- **Bornes, réglables :** 8 appels d'outils au plus par tour ; 30 s par appel ; résultat rendu au modèle
  tronqué à 2 000 caractères, avec la mention qu'il est tronqué. À la borne des appels, le modèle répond
  avec ce qu'il a.
- Le résultat d'un outil est une donnée (ADR 0009, principe n° 2) : il est rendu dans le rôle « outil », jamais
  dans le message système.
- L'orbe reste en réflexion pendant un appel. Une interruption (US1.12) annule l'appel en cours et les
  suivants ; l'annulation est un refus pour le journal.
- **La liste donnée au modèle est celle des outils activés**, pas plus : un outil désactivé, ou dont la cible
  manque, n'est pas proposé. La page des outils affiche une estimation de la taille de la liste, en jetons,
  à côté de la taille du contexte.

### 5. Ce que la conversation montre

- Chaque appel s'affiche dans la fenêtre de conversation, dans le tour : nom de l'outil, arguments, puis
  résultat ou cause du refus, repliés par défaut (US2.1).
- L'appel, son résultat et sa durée sont enregistrés avec le tour. Le texte dit à voix haute ne lit jamais
  les arguments : seul le modèle parle, sauf pour une demande de confirmation.

### 6. Activation

- Section de réglages `Tools`, page « Outils » des paramètres : un interrupteur par outil, avec sa classe et
  son origine.
- Un outil qui fait sortir quelque chose de la machine est **désactivé par défaut**. Un outil d'un serveur MCP
  aussi (ADR 0018).
- L'utilisateur peut rendre un outil **plus strict** (par exemple demander une confirmation pour
  `fetch_url`), jamais moins strict que sa classe.

### 7. Tranches

- `Permissions/Decision` : le point de décision et le journal.
- `Tools/Clock`, `Tools/Calculator`, `Tools/Files`, `Tools/Web` : un outil ou une famille par tranche, avec
  ses options et sa page.
- `Tools/Catalog` : la page « Outils ».
- Le noyau porte les contrats : outil et registre, point de décision, classes, recherche web. Il ne porte
  aucune logique de classement.

## Réponses de Benoit

Questions posées dans la conversation le 2026-11-20, réponses du même jour.

1. **Fournisseur de la recherche web.** Le plan retenait Brave Search « avec une offre gratuite à vérifier ».
   Vérifié le 2026-11-20 sur la page de prix de Brave : il n'y a plus d'offre gratuite séparée ; le forfait
   « Search » est prépayé, 5 $ US par 1 000 requêtes, avec 5 $ de crédit offerts chaque mois (environ
   1 000 recherches). Benoit a demandé si la recherche pouvait se faire dans un navigateur visible, piloté
   par quelque chose comme Playwright, puis a tranché : « on devrait avoir la version gratuite et si
   l'utilisateur ajoute sa clé on peut utiliser l'option Brave ». Le navigateur piloté passe d'abord par
   l'essai E15 (section 3).
2. **Quel navigateur Jarvis pilote-t-il ?** Edge ou Chrome installé, fenêtre visible, dans un profil à part,
   en lecture seule. Benoit ajoute : on doit pouvoir choisir le profil habituel, « avec un avertissement à
   l'utilisateur ».

## Options écartées

- **Une classe fixe par outil.** Ne tient pas pour Home Assistant (essai E14) ni pour un chemin de fichier.
- **Laisser le modèle demander la confirmation lui-même.** Contraire au principe n° 1 de l'ADR 0009.
- **Un outil d'écriture de fichier dès l'étape 2.** Claude Code le fait mieux, avec le détail exact des
  différences à confirmer.
- **Brave Search seul.** Écarté par Benoit : il n'y a plus d'offre gratuite, et il veut une recherche sans clé.
- **Un navigateur caché, sans fenêtre.** L'utilisateur ne verrait pas ce que Jarvis ouvre.
- **`fetch_url` ouvert au réseau local.** Une page web pourrait faire lire au modèle un service interne.

## Conséquences

- Une injection dans une page ou un fichier peut faire demander un outil au modèle ; elle ne peut pas changer
  la classe de l'appel, ni l'approuver.
- La classe par appel coûte un peu de code par outil ; en échange, Home Assistant, les fichiers et Claude Code
  suivent la même règle.
- 2 000 caractères par résultat est peu pour une page web : `fetch_url` rend le début du texte utile. Un
  résumé par le modèle local d'une page entière demanderait un autre ADR.
- Avec la configuration par défaut, aucun outil ne fait sortir quoi que ce soit de la machine.
- La recherche par navigateur dépend de la page d'un moteur de recherche, qui change sans prévenir et peut
  bloquer un navigateur piloté. Quand elle échoue, l'outil rend la cause au modèle ; la page des outils
  propose alors Brave Search.
- **Risque accepté** avec le profil habituel : c'est un choix explicite de l'utilisateur, derrière un
  avertissement.
