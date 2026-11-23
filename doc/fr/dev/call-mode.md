# Le mode « Appel » : une conversation suivie

Chantier `stage2/20-call-mode`. Décision : [ADR 0025](adr/0025-task-agents-and-their-memories.md), sections 12
et 13 (point 3). Les agents sont décrits dans [agents.md](agents.md), les projets dans [projects.md](projects.md).

Ce chantier construit ce que le mode « Appel » veut dire pour les agents sur le PC : il commence, l'agent garde
la main, Jarvis écoute la suite sans « Hey Jarvis », il se termine. **Reste à construire :** les appels dans
l'application Android (étape 5 du document de conception).

## Ce qui est construit

```
« On doit discuter de… »  ou  bouton « Appeler » ──► CallMode.Start()
        │
        ▼
ConversationEngine ── pendant l'appel : l'agent du tour précédent reçoit la demande (CallMode.Agent),
        │             avec le projet de l'appel (CallMode.Project)
        │             « au revoir » : Jarvis répond « Au revoir. » sans le modèle, CallMode.End()
        ▼
CallListener ── un tour parlé se termine, Jarvis est au repos ──► ActivationRequested(Call) ──► écoute
```

Tout vit dans la tranche `Conversation/Dialogue`. Le noyau gagne une seule chose : la source d'activation
`ActivationSource.Call`.

## Commencer (`CallMode.Opens`)

- **Le bouton « Appeler »** de la fenêtre de conversation. Jarvis écoute tout de suite, si le micro le permet.
- **Une phrase**, à la voix ou au clavier : « on doit discuter », « on doit parler », « il faut qu'on parle »,
  « il faut qu'on discute », « j'aimerais discuter », « je veux discuter », et en anglais « we need to talk »,
  « we need to discuss », « let's discuss »… Comparée par du code, sans casse ni accents, jamais par un modèle.
  La phrase est ensuite traitée comme une demande ordinaire : l'agent Jarvis trie et passe la main.

## Pendant l'appel

- **L'agent qui a répondu garde la main** au tour suivant, à la voix comme au clavier, avec le projet du tour.
  L'agent Jarvis ne trie plus tant qu'un autre agent a la main.
- **Revenir à Jarvis** : l'agent appelle `return_to_jarvis`, comme hors appel. L'appel continue avec l'agent
  Jarvis, qui peut repasser la main.
- Un agent choisi dans la fenêtre pour une demande écrite passe avant l'agent de l'appel.
- **Jarvis écoute la suite sans « Hey Jarvis »** après un tour parlé qui a reçu sa réponse. C'est une activation
  ordinaire : refusée quand le micro est coupé ou absent (ADR 0005), et l'orbe montre l'écoute.
- `ActivationSource.Call` ne lance l'écoute qu'au repos : elle n'annule jamais une écoute et n'interrompt jamais
  Jarvis.
- **Jarvis n'insiste pas** : une écoute que l'utilisateur annule (clic, raccourci, micro coupé), un tour écrit,
  une annonce ou une panne ne relancent pas l'écoute. L'utilisateur reprend avec « Hey Jarvis » ou un clic.
- **Après trois écoutes de suite sans rien entendre**, Jarvis n'écoute plus de lui-même. L'appel reste ouvert.
- La fenêtre de conversation dit « Appel en cours avec l'agent X ».

## Terminer

- **« Au revoir »**, seul dans la demande (« Au revoir », « Au revoir Jarvis », « Bye », « Goodbye », « À plus
  tard », « À la prochaine »). Jarvis répond « Au revoir. » sans appeler le modèle.
- Le bouton **« Raccrocher »** de la fenêtre.
- L'arrêt de Jarvis : l'appel n'est pas gardé.

## Choix provisoires de ce chantier

À confirmer par Benoit :

1. Les phrases qui commencent un appel et celles qui le terminent sont des listes fixes dans le code.
2. Après trois silences de suite, Jarvis n'écoute plus de lui-même, mais l'appel reste ouvert.
3. Un tour écrit pendant l'appel ne relance pas l'écoute.
4. Le bouton « Appeler » est dans la fenêtre de conversation ; rien sur l'orbe ne dit qu'un appel est en cours,
   à part l'écoute elle-même.
5. Revenir à Jarvis garde le projet de l'appel.
6. « Au revoir » hors appel est une demande ordinaire, envoyée au modèle.
7. Une demande passée à Claude (« Demande à Claude… ») pendant un appel ne change pas l'agent qui a la main.

## Vérifications sur Windows

Rien de ce chantier n'a tourné sur Windows, et l'écoute sans « Hey Jarvis » n'a jamais été essayée avec un vrai
micro.

1. « On doit discuter de Kazo OS », puis deux questions de suite sans « Hey Jarvis » : l'agent « Projet »
   répond-il aux deux ?
2. **L'écoute reprend-elle trop tôt ?** Jarvis ne doit pas s'entendre lui-même à la fin de sa réponse.
3. Trois silences : l'écoute s'arrête-t-elle, et « Hey Jarvis » la reprend-il dans le même appel ?
4. « Au revoir » termine l'appel ; la demande suivante repasse par l'agent Jarvis.
5. Micro coupé pendant un appel : aucune écoute ne démarre.
