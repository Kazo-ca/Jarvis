using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Features.Setup.Models;

/// <summary>Which local language model fits the graphics card.</summary>
public enum LanguageModelAdvice
{
    /// <summary>The default one, <c>qwen3:14b</c> (ADR 0007, section 5).</summary>
    Default,

    /// <summary>A model of 7 to 8 billion parameters, the fallback measured at work item 11.</summary>
    Smaller,
}

/// <summary>
/// What the models page proposes for the graphics card found (stage 1 plan, work item 14): the default chain needs an
/// NVIDIA card with about 12 GB (ADR 0006, section 4); with less, or without one, a smaller Whisper and a smaller
/// language model. The thresholds are provisional choices of work item 14.
/// </summary>
/// <param name="Card">The NVIDIA card, or <see langword="null"/>.</param>
/// <param name="Whisper">The Whisper model proposed.</param>
/// <param name="Device">Where it runs.</param>
/// <param name="LanguageModel">The local language model to prefer.</param>
public sealed record ModelAdvice(GraphicsCard? Card, WhisperModel Whisper, ComputeDevice Device, LanguageModelAdvice LanguageModel)
{
    /// <summary>
    /// Memory from which the default chain is proposed: <c>qwen3:14b</c> and Whisper turbo leave about 600 MiB on the
    /// 12 GB card of Benoit; 10 GB keeps a margin for cards sold as 11 or 12 GB.
    /// </summary>
    public const long FullChainMiB = 10 * 1024;

    /// <summary>The advice for a card.</summary>
    /// <param name="card">The NVIDIA card, or <see langword="null"/> when none was found.</param>
    /// <returns>The advice.</returns>
    public static ModelAdvice For(GraphicsCard? card) => card switch
    {
        null => new(null, WhisperModel.Base, ComputeDevice.Cpu, LanguageModelAdvice.Smaller),
        { MemoryMiB: >= FullChainMiB } => new(card, WhisperModel.LargeV3Turbo, ComputeDevice.Gpu, LanguageModelAdvice.Default),
        _ => new(card, WhisperModel.Small, ComputeDevice.Gpu, LanguageModelAdvice.Smaller),
    };
}
