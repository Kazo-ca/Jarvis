# Résultats E7 — 18 novembre 2026

**E7 : réussi, avec un changement de modèle recommandé.** Le modèle actuel (qwen2.5-coder 14B, `local-ia-active`)
choisit toujours le bon outil, « passer la main » compris, mais il écrit l'appel en JSON dans le texte au lieu
d'utiliser le canal d'outils d'Ollama : sans analyseur tolérant, 6 réponses justes sur 42. **qwen3 14B, réflexion
désactivée**, fait 42 sur 42 par le canal officiel, et plus vite.

## Protocole

14 demandes, 3 essais chacune, température 0,2, contexte 4096, API `/api/chat` d'Ollama avec 7 outils :
date et heure, calcul, recherche web, allumer et éteindre (Home Assistant), lire un fichier, passer la main à
Claude. 11 demandes en français, 3 en anglais, dont 2 qui ne demandent aucun outil (« Merci Jarvis, ce sera
tout », « Comment vas-tu ce matin ? »). Le message système est celui du majordome.

## Résultats

| Modèle | Bon outil | Par le canal officiel | Médiane | 90e centile |
| --- | --- | --- | --- | --- |
| qwen2.5-coder 14B (`local-ia-active`), strict | 6 / 42 (14 %) | 6 | 1,0 s | 1,8 s |
| qwen2.5-coder 14B, analyseur tolérant | **42 / 42** | 6 (36 en JSON dans le texte) | 1,0 s | 1,9 s |
| qwen2.5 14B (instruct) | 37 / 42 (88 %) | 37 | 1,1 s | 4,1 s |
| qwen3 14B, réflexion active | 42 / 42 | 42 | 5,5 s | 16,8 s |
| **qwen3 14B, réflexion désactivée** (`think: false`) | **42 / 42** | **42** | **0,9 s** | 2,0 s |

Les erreurs de qwen2.5 14B : il passe la main à Claude pour « Lis-moi le fichier C:\notes\courses.txt » (3 fois)
et répond l'heure sans outil (2 fois). Aucun modèle n'a appelé d'outil sur les deux phrases de conversation.

Dans la chaîne vocale (E4), qwen3 14B réflexion désactivée donne sa première phrase en 0,46 à 0,49 s.

## Recommandation

- Modèle local par défaut : **qwen3:14b** avec `think: false` (même taille en mémoire que qwen2.5-coder 14B).
- Garder dans l'adaptateur Ollama un analyseur tolérant pour les modèles qui écrivent l'appel en texte : il
  n'accepte qu'une réponse entière qui est un objet JSON nommant un outil déclaré.

## Non vérifié

- Le tour complet : appel d'outil, résultat renvoyé au modèle, réponse finale à voix haute.
- Les arguments : l'essai vérifie le nom de l'outil, pas la justesse des arguments (« salon », « 347*29 »).
- Une liste d'outils longue (les dizaines d'outils d'un serveur MCP Home Assistant).
