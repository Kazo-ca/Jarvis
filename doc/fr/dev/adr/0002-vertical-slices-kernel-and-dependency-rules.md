# ADR 0002 : tranches verticales, noyau partagé et règles de dépendance

## Statut

Accepté par Benoit le 2026-11-19 (proposé le 2026-11-18, chantier `spikes/11-stage-1-adrs`).

- **Sources :** décision n° 36 de la [conception](../plans/design.md) et sa section « Architecture » ;
  section I d'`AGENTS.md`.
- **Complète :** [ADR 0001](0001-target-dotnet-10-and-projects.md) (liste des projets).
- **Complété par :** [ADR 0003](0003-hosts-and-native-services.md) (hôtes et contrats natifs),
  [ADR 0005](0005-assistant-state-machine-and-orb-contract.md) (machine à états).

## Contexte

- Benoit a demandé une architecture en tranches verticales (VSA) : le code est rangé par fonctionnalité, pas
  par couche technique.
- Kazo Bar range son code par couche (`Core`, `Infrastructure`, `UI`, `Plugins`). On en garde les hôtes et
  les services natifs derrière des interfaces, pas ce découpage.
- La conception donne quatre règles de dépendance et les renvoie à cet ADR.
- Une règle qui n'est vérifiée par personne finit par être oubliée. Kazo Os fait échouer la construction
  quand un composant contient du style ou du script dans son balisage (son ADR 0007).

## Décision

### 1. Sens des dépendances

```
Hôtes (Windows, Web, Mobile)  ──►  Features  ──►  Kernel
Hôte Windows                  ──►  Adapters  ──►  Kernel
```

- `Features` ne référence jamais `Adapters` ni un hôte.
- `Adapters` ne référence jamais `Features`.
- `Kernel` ne référence aucun projet.
- Seul l'hôte Windows charge `Adapters`. L'hôte Web fournit des services simulés.

### 2. Anatomie d'une tranche

Une tranche est un dossier `Features/<Domaine>/<Tranche>/` qui contient tout ce qu'il faut pour une
fonctionnalité visible par l'utilisateur, en général un récit ou un petit groupe de récits liés.

| Élément | Exemple | Règle |
| --- | --- | --- |
| Composants Razor | `WakeWordSettings.razor` | CSS et JavaScript dans `.razor.css` et `.razor.js`, à côté |
| Demande, traitement, résultat | `DetectWakeWord.cs` | La logique de la fonctionnalité vit ici |
| Options | `WakeWordOptions.cs` | Section de configuration de la tranche ([ADR 0004](0004-configuration-storage-and-secret-vault.md)) |
| Textes | `WakeWord.resx`, `WakeWord.fr.resx` | [ADR 0008](0008-languages-and-localization.md) |
| Enregistrement | une méthode d'extension par tranche | Seul point d'entrée connu de l'extérieur |
| Tests | `tests/Kazo.Jarvis.Tests.Unit/Features/<Domaine>/<Tranche>/` | Même chemin que la tranche |

- L'espace de noms suit le dossier : `Kazo.Jarvis.Features.<Domaine>.<Tranche>`.
- Un domaine (`Voice`, `Orb`, `Handoff`…) n'est qu'un dossier de rangement. Il ne contient pas de code
  partagé entre ses tranches.

### 3. Règles de dépendance

1. **Une tranche ne référence jamais une autre tranche.** Elles se parlent par des messages définis dans le
   noyau.
2. **Une tranche dépend du noyau et de contrats, jamais d'un adaptateur concret.**
3. **On préfère dupliquer un peu de code entre deux tranches plutôt que créer trop tôt une abstraction
   partagée.** Ce qui sert à trois tranches ou plus monte dans le noyau.
4. **Le noyau ne contient aucune logique métier, aucun composant d'interface et aucune dépendance native.**

Précision ajoutée par cet ADR : un composant d'interface ne peut pas monter dans le noyau. Un composant de
présentation utilisé par trois tranches ou plus (un champ de secret, un cadre de page de réglages) va dans
`Features/Shared/`. Ce dossier n'est pas une tranche : il ne contient aucune logique métier, ne dépend que du
noyau, et toute tranche peut l'utiliser.

### 4. Contenu du noyau

| Permis | Interdit |
| --- | --- |
| Messages entre tranches | Logique d'une fonctionnalité |
| Machine à états de l'assistant (ADR 0005) | Composant Razor, CSS, JavaScript |
| Contrats des services natifs et externes (ADR 0003) | Type d'une technologie concrète (WPF, Whisper, Ollama, NAudio) |
| Contrats de configuration, de stockage et de coffre (ADR 0004) | Accès au disque, au réseau ou au matériel |
| Petits types purs : identifiants, résultats, erreurs | Paquet autre que les abstractions `Microsoft.Extensions.*` |

La machine à états est la seule règle de coordination admise dans le noyau : c'est une table de transitions
pure, sans entrée ni sortie.

### 5. Messages entre tranches

- Deux sortes de messages, tous des enregistrements immuables sans comportement :
  - un **événement** décrit un fait passé (`WakeWordDetected`) ; il a zéro, un ou plusieurs abonnés ;
  - une **demande** attend une réponse (`IRequest<TResult>`) ; elle a exactement un traitement.
- Le noyau fournit un petit bus en mémoire, typé et asynchrone. **Aucune bibliothèque de médiation externe**
  n'est utilisée : le besoin tient en quelques dizaines de lignes, comme l'`EventAggregator` de Kazo Bar.
- Un abonné qui échoue n'empêche pas les autres de recevoir l'événement. L'erreur est journalisée.
- Les messages sont rangés par domaine dans le noyau (`Kernel/Messages/<Domaine>/`).
- Les flux à haut débit (échantillons audio, niveau sonore) ne passent pas par le bus. Ils ont leur propre
  contrat (ADR 0005 et [ADR 0006](0006-voice-chain.md)).
- Un message ne transporte ni type d'interface, ni type d'un adaptateur.

### 6. Composition par les hôtes

- Chaque tranche expose une méthode d'enregistrement. `Features` expose une méthode qui les appelle toutes.
- Un hôte enregistre d'abord les mises en œuvre des contrats (adaptateurs réels ou services simulés), puis
  les tranches. Il ne contient que le démarrage et le code propre à sa plateforme.

### 7. Vérification par des tests

Les règles sont vérifiées par des tests d'architecture dans `Kazo.Jarvis.Tests.Unit`, écrits avec les
premiers projets :

- aucun type d'une tranche n'utilise un type d'une autre tranche (`Features.Shared` excepté) ;
- `Features` ne référence ni `Adapters` ni un hôte ; `Adapters` ne référence pas `Features` ; `Kernel` ne
  référence aucun projet ni paquet interdit ;
- `Kernel` ne contient aucun composant Razor ;
- aucun fichier `.razor` ne contient `<style>`, `<script>` ou `style="…"`.

Ces tests font partie du seuil de fusion. Aucun n'est désactivé pour obtenir le vert.

### 8. Tests des tranches

- Chaque tranche a ses tests unitaires, avec des contrats simulés. Aucun micro, modèle ou réseau réel.
- Les parcours visibles sont testés par Playwright contre l'hôte Web.

## Conséquences

- Supprimer une fonctionnalité revient à supprimer son dossier, son enregistrement et ses tests.
- Le noyau grossit avec les messages. C'est voulu : la liste des messages est la carte des échanges entre
  fonctionnalités.
- Un peu de code sera dupliqué entre deux tranches. On ne le factorise qu'à la troisième utilisation.
- Une tranche ne peut pas appeler directement une autre « pour aller plus vite ». Le test d'architecture
  échoue, et il faut définir le message.
- Un adaptateur se remplace sans toucher aux tranches, et une tranche se teste sans adaptateur.
