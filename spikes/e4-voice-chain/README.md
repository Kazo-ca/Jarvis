# E4 / E5 : chaîne vocale en C#

Essai jetable. Vérifie que la chaîne vocale tient en C# sans Python (mot d'éveil ONNX, Whisper.net,
modèle local, Piper et Kokoro) et mesure le délai entre la fin de la phrase et le premier son. Sert aussi
à E5 (Whisper et le modèle local sur la même carte graphique).

## Prérequis

- SDK .NET 10, carte NVIDIA avec CUDA 12 pour Whisper sur GPU.
- Ollama joignable (par défaut `http://localhost:11435`, variable `JARVIS_OLLAMA`), modèle choisi par `JARVIS_LLM`.
- Les modèles dans `C:\jarvis\temp\models` (variable `JARVIS_MODELS`), jamais dans le dépôt :

```bash
cd C:/jarvis/temp/models
curl -LO https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/melspectrogram.onnx
curl -LO https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/embedding_model.onnx
curl -LO https://github.com/dscripka/openWakeWord/releases/download/v0.5.1/hey_jarvis_v0.1.onnx
curl -LO https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin
curl -LO https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin
curl -LO https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-fr_FR-tom-medium.tar.bz2
curl -LO https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-en_GB-alan-medium.tar.bz2
curl -LO https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2
for t in *.tar.bz2; do tar xjf "$t"; done
```

## Lancer

```bash
dotnet build -c Release
cd bin/Release/net10.0
dotnet VoiceChain.dll gen        # fabrique les clips d'essai (out/)
dotnet VoiceChain.dll wake       # score « hey jarvis » de chaque clip
dotnet VoiceChain.dll tts        # vitesse de Piper et Kokoro
JARVIS_LLM=qwen3:14b dotnet VoiceChain.dll chain ggml-large-v3-turbo-q5_0.bin gpu piper fr 4
```

Résultats : [RESULTS.md](RESULTS.md).
