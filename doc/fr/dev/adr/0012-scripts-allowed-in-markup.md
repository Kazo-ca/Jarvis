# ADR 0012 : le script est permis dans le balisage

## Statut

Accepté par Benoit le 2026-11-19 (réponse à la question posée dans la PR du chantier `stage1/1-solution`).

- **Remplace en partie :** [ADR 0002](0002-vertical-slices-kernel-and-dependency-rules.md), section 7, pour
  le script seulement. La règle sur le style ne change pas.
- **Modifie :** section II d'`AGENTS.md` (règles 1 et 2).

## Contexte

- L'ADR 0002 (section 7) fait échouer les tests quand un fichier `.razor` contient `<style>`, `<script>` ou
  `style="…"`. La règle venait de Kazo Os (son ADR 0007).
- Le chantier `stage1/1-solution` a montré que l'hôte Web ne peut pas s'en passer : son document
  (`App.razor`) doit charger `blazor.web.js`. Une exception limitée à ce fichier avait été écrite dans le test.
- Réponse de Benoit du 2026-11-19 : « On peut utiliser du script sans problèmes. Cette restriction était pour
  Kazo Os. On peut utiliser du script quand on manipule le DOM d'une page sans restrictions. »

## Décision

1. **Le script est permis dans le balisage Razor**, en balise `<script>` comme en fichier chargé par
   `src`, et pour manipuler le DOM sans restriction.
2. **Le style reste interdit dans le balisage :** ni `<style>`, ni `style="…"`. Le CSS d'un composant va dans
   `MyComponent.razor.css`, à côté de lui.
3. Le test d'architecture `RazorMarkupTests` ne vérifie plus que le style. La liste des documents d'hôte
   autorisés à charger un script (`RazorMarkupRule.HostDocuments`) disparaît.
4. Ranger le JavaScript d'un composant dans `MyComponent.razor.js` reste conseillé quand il dépasse quelques
   lignes : il se lit, se met en cache et se teste mieux. C'est un conseil, pas une règle vérifiée.
5. « C# d'abord » ne change pas : la logique d'une fonctionnalité est écrite en C#. Le JavaScript sert aux
   API du navigateur que C# n'atteint pas, la manipulation du DOM comprise.

## Conséquences

- Plus d'exception à tenir à jour pour chaque hôte (Web aujourd'hui, Mobile à l'étape 4).
- Un composant peut écrire un petit script directement dans son balisage, par exemple pour l'orbe ou les
  animations, sans fichier séparé.
- Rien ne vérifie la place du JavaScript : la revue de code s'en charge.
