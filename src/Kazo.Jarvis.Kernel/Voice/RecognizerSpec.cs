namespace Kazo.Jarvis.Kernel.Voice;

/// <summary>The Whisper models offered (ADR 0006, section 4). Each one is a file of the models folder.</summary>
public enum WhisperModel
{
    /// <summary><c>ggml-large-v3-turbo-q5_0.bin</c>: the default, on the graphics card (0.43 to 0.68 s in spike E4).</summary>
    LargeV3Turbo,

    /// <summary><c>ggml-small.bin</c>: on the graphics card when its memory runs short (0.24 to 0.5 s).</summary>
    Small,

    /// <summary><c>ggml-base.bin</c>: the fallback without an NVIDIA card, on the processor (1.05 to 1.1 s).</summary>
    Base,
}

/// <summary>Where the recognition runs (ADR 0006, section 4).</summary>
public enum ComputeDevice
{
    /// <summary>The NVIDIA graphics card, through CUDA. Falls back on the processor when CUDA cannot load.</summary>
    Gpu,

    /// <summary>The processor.</summary>
    Cpu,
}

/// <summary>Which recognizer to load.</summary>
/// <param name="Model">The Whisper model.</param>
/// <param name="Device">Where it should run.</param>
public sealed record RecognizerSpec(WhisperModel Model, ComputeDevice Device);
