// Spike E4/E5: the voice chain in C# (wake word, Whisper.net, local LLM, Piper/Kokoro) and its latency.
// Usage:
//   dotnet run -c Release -- gen                       create the test clips in out/
//   dotnet run -c Release -- wake                      score every clip with the "hey jarvis" model
//   dotnet run -c Release -- chain <ggml> <gpu|cpu> <piper|kokoro> [fr|en] [runs]
//   dotnet run -c Release -- tts                       time Piper and Kokoro on French and English sentences
using System.Diagnostics;
using Spikes.VoiceChain;

var models = Environment.GetEnvironmentVariable("JARVIS_MODELS") ?? @"C:\jarvis\temp\models";
var ollama = Environment.GetEnvironmentVariable("JARVIS_OLLAMA") ?? "http://localhost:11435";
var llm = Environment.GetEnvironmentVariable("JARVIS_LLM") ?? "local-ia-active:latest";
var outDir = Path.Combine(AppContext.BaseDirectory, "out");
Directory.CreateDirectory(outDir);

const string SystemFr = "Tu es Jarvis, le majordome du film. Poli, concis, humour sec. Tu vouvoies et dis « Monsieur ». Réponds en une ou deux phrases courtes, sans liste ni markdown.";
const string SystemEn = "You are Jarvis, the butler from the film. Polite, concise, dry humour. Address the user as \"Sir\". Answer in one or two short sentences, no lists or markdown.";

switch (args.FirstOrDefault())
{
    case "gen": Generate(); break;
    case "wake": Wake(); break;
    case "tts": Tts(); break;
    case "chain": await Chain(args[1], args[2] == "gpu", args[3], args.Length > 4 ? args[4] : "fr", args.Length > 5 ? int.Parse(args[5]) : 4); break;
    default: Console.WriteLine("gen | wake | tts | chain <ggml> <gpu|cpu> <piper|kokoro> [fr|en] [runs]"); break;
}

Speaker PiperFr() => Speaker.Piper(Path.Combine(models, "vits-piper-fr_FR-tom-medium"), "fr_FR-tom-medium.onnx");
Speaker PiperEn() => Speaker.Piper(Path.Combine(models, "vits-piper-en_GB-alan-medium"), "en_GB-alan-medium.onnx");
Speaker KokoroFr() => Speaker.Kokoro(Path.Combine(models, "kokoro-multi-lang-v1_0"), 30, "fr");   // ff_siwis
Speaker KokoroEn() => Speaker.Kokoro(Path.Combine(models, "kokoro-multi-lang-v1_0"), 26, "en-us"); // bm_george

void Clip(Speaker s, string text, string name)
{
    var a = Audio.Resample(s.Say(text), s.SampleRate, 16000);
    var clip = Audio.Silence(1).Concat(a).Concat(Audio.Silence(1)).ToArray();
    Audio.Save(Path.Combine(outDir, name + ".wav"), clip, 16000);
    Console.WriteLine($"{name}: {clip.Length / 16000.0:F1} s");
}

void Generate()
{
    using var pe = PiperEn(); using var pf = PiperFr(); using var ke = KokoroEn(); using var kf = KokoroFr();
    Clip(ke, "Hey Jarvis.", "wake_kokoro_en");
    Clip(pe, "Hey Jarvis.", "wake_piper_en");
    Clip(kf, "Hey Jarvis.", "wake_kokoro_fr");
    Clip(pf, "Hé Jarvis.", "wake_piper_fr");
    Clip(pe, "Hey Travis, how are you today?", "neg_travis_en");
    Clip(pf, "J'ai acheté du pain et des carottes au marché.", "neg_market_fr");
    Clip(pf, "Quelle est la capitale du Canada, et combien d'habitants y vivent ?", "question_fr");
    Clip(pe, "What is the capital of Canada, and how many people live there?", "question_en");
}

void Wake()
{
    using var det = new WakeWordDetector(models);
    foreach (var f in Directory.GetFiles(outDir, "*.wav").Where(f => !Path.GetFileName(f).StartsWith("question")).Order())
    {
        var samples = Read16k(f);
        var sw = Stopwatch.StartNew();
        var scores = det.Score(samples);
        var audioMs = samples.Length / 16.0;
        Console.WriteLine($"{Path.GetFileNameWithoutExtension(f),-16} max score {scores.DefaultIfEmpty(0).Max():F3}  ({sw.ElapsedMilliseconds} ms for {audioMs:F0} ms of audio, {100.0 * sw.ElapsedMilliseconds / audioMs:F1} % real time)");
    }
}

void Tts()
{
    var sentences = new[] { ("fr", "Bien sûr, Monsieur. La capitale du Canada est Ottawa."), ("en", "Certainly, Sir. The capital of Canada is Ottawa.") };
    foreach (var (name, make) in new (string, Func<Speaker>)[] { ("piper fr", PiperFr), ("piper en", PiperEn), ("kokoro fr", KokoroFr), ("kokoro en", KokoroEn) })
    {
        var sw = Stopwatch.StartNew(); using var s = make(); var load = sw.ElapsedMilliseconds;
        var text = sentences.First(x => name.EndsWith(x.Item1)).Item2;
        s.Say(text); // warm-up
        var times = Enumerable.Range(0, 3).Select(_ => { var t = Stopwatch.StartNew(); var a = s.Say(text); return (t.ElapsedMilliseconds, a.Length * 1000L / s.SampleRate); }).ToList();
        Console.WriteLine($"{name,-10} load {load} ms, synth {string.Join("/", times.Select(t => t.Item1))} ms for {times[0].Item2} ms of speech (RTF {times.Average(t => t.Item1) / times[0].Item2:F2})");
        Audio.Save(Path.Combine(outDir, $"tts_{name.Replace(' ', '_')}.wav"), s.Say(text), s.SampleRate);
    }
}

async Task Chain(string ggml, bool gpu, string tts, string lang, int runs)
{
    // The real pipeline needs the VAD to hear this much silence before it calls the end of the sentence.
    const int VadSilenceMs = 500;
    Transcriber.SelectRuntime(gpu);
    var sw = Stopwatch.StartNew();
    using var stt = new Transcriber(Path.Combine(models, ggml), lang, gpu);
    using var speaker = tts == "kokoro" ? (lang == "fr" ? KokoroFr() : KokoroEn()) : (lang == "fr" ? PiperFr() : PiperEn());
    var model = new LocalModel(ollama, llm);
    Console.WriteLine($"load {sw.ElapsedMilliseconds} ms, whisper runtime {Transcriber.LoadedRuntime}, {stt.Model}, {speaker.Name}, {llm}");
    Console.WriteLine($"GPU memory: {Gpu()}");
    var question = Read16k(Path.Combine(outDir, $"question_{lang}.wav"));
    for (var run = 1; run <= runs; run++)
    {
        var (text, sttMs) = await stt.TranscribeAsync(question);
        var answer = await model.AskAsync(lang == "fr" ? SystemFr : SystemEn, text);
        var t = Stopwatch.StartNew(); var audio = speaker.Say(answer.FirstSentence); var ttsMs = t.ElapsedMilliseconds;
        var total = VadSilenceMs + sttMs + answer.FirstSentenceMs + ttsMs;
        Console.WriteLine($"run {run}: total {total} ms = vad {VadSilenceMs} + stt {sttMs} + llm first sentence {answer.FirstSentenceMs} (first token {answer.FirstTokenMs}) + tts {ttsMs}");
        if (run == 1)
        {
            Console.WriteLine($"  heard : {text}");
            Console.WriteLine($"  answer: {answer.Full}");
            Audio.Save(Path.Combine(outDir, $"answer_{lang}_{tts}.wav"), audio, speaker.SampleRate);
        }
    }
    Console.WriteLine($"GPU memory: {Gpu()}");
}

static float[] Read16k(string path)
{
    using var r = new NAudio.Wave.AudioFileReader(path);
    var all = new List<float>(); var buf = new float[16000]; int n;
    while ((n = r.Read(buf.AsSpan())) > 0) all.AddRange(buf.AsSpan(0, n).ToArray());
    return Audio.Resample(all.ToArray(), r.WaveFormat.SampleRate, 16000);
}

static string Gpu()
{
    try
    {
        var p = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=memory.used,memory.total --format=csv,noheader") { RedirectStandardOutput = true })!;
        return p.StandardOutput.ReadToEnd().Trim();
    }
    catch (Exception ex) { return "unknown (" + ex.Message + ")"; }
}
