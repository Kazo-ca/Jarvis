# Pièges

Pièges rencontrés pendant le développement et leur solution. Ajouter une entrée
dès qu'un problème a coûté du temps à diagnostiquer.

## sherpa-onnx 2.0 n'a plus espeak-ng (essai E4)

Avec `org.k2fsa.sherpa.onnx` 2.0.x, Piper et Kokoro hors anglais échouent : « phoneme_codepoints is empty,
no lexicon, and no frontend available ». La 2.0 a retiré espeak-ng et piper-phonemize (PR 4055). Rester sur
**1.13.8**, ou fournir soi-même les phonèmes. espeak-ng est sous GPLv3 : à considérer pour la licence.

## Kokoro multilingue : pas de langue `en-gb` (essai E4)

La voix britannique `bm_george` s'utilise avec `Lang = "en-us"` et le lexique `lexicon-us-en.txt`.
`en-gb` fait échouer espeak-ng (« Failed to set eSpeak-ng voice »).

## qwen2.5-coder écrit ses appels d'outils en texte (essai E7)

Le modèle renvoie `{"name": ..., "arguments": ...}` dans `content` au lieu de `tool_calls`. Utiliser
qwen3 (avec `think: false`) ou un analyseur tolérant qui n'accepte qu'un objet JSON entier nommant un outil déclaré.

## Une fenêtre WPF transparente animée coûte cher (essai E6)

`AllowsTransparency=True` crée une fenêtre « layered » : chaque image modifiée est recopiée par le processeur.
Avec `WebView2CompositionControl`, la capture ajoute 4 à 5 % d'un cœur dès que la page bouge. Garder l'orbe
immobile en veille.

## `requestAnimationFrame` réveille le moteur même sans dessiner (essai E6)

Une boucle qui saute des images réveille quand même le moteur 60 fois par seconde. Sous 10 images par
seconde, utiliser une minuterie.

## La connexion du Claude Code en ligne de commande expire à part (essai E8)

Les sessions de Claude Desktop restent connectées alors que `claude -p` répond « OAuth session expired ».
Kazo.Jarvis doit détecter ce message et demander de relancer `/login`, au lieu d'échouer en silence.

## NAudio 3 utilise `Span<float>`

`ISampleProvider.Read` prend un `Span<float>`. Les exemples écrits pour NAudio 2 ne compilent plus.
