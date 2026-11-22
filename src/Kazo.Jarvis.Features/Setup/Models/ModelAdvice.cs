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
/// NVIDIA card with about 16 GB; a card of about 12 GB keeps Whisper turbo with a smaller language model; with less,
/// or without a card, a smaller Whisper too. The thresholds are provisional choices of work item 14, corrected by
/// the acceptance of stage 2.
/// </summary>
/// <param name="Card">The NVIDIA card, or <see langword="null"/>.</param>
/// <param name="Whisper">The Whisper model proposed.</param>
/// <param name="Device">Where it runs.</param>
/// <param name="LanguageModel">The local language model to prefer.</param>
public sealed record ModelAdvice(GraphicsCard? Card, WhisperModel Whisper, ComputeDevice Device, LanguageModelAdvice LanguageModel)
{
    /// <summary>
    /// Memory from which the default chain is proposed. On a 12 GB card, <c>qwen3:14b</c> and Whisper turbo leave
    /// about 600 MiB when measured alone (ADR 0006, section 4), but at the acceptance of stage 2 the prompt with its
    /// tools no longer fitted: the model fell back on the processor in part and read 7 tokens a second. 15 GB keeps
    /// a margin for cards sold as 16 GB.
    /// </summary>
    public const long FullChainMiB = 15 * 1024;

    /// <summary>
    /// Memory from which Whisper turbo stays on the card, next to a smaller language model: what works on the 12 GB
    /// card of the acceptance (turbo and <c>qwen3:8b</c>); 10 GB keeps a margin for cards sold as 11 or 12 GB.
    /// </summary>
    public const long TurboMiB = 10 * 1024;

    /// <summary>The advice for a card.</summary>
    /// <param name="card">The NVIDIA card, or <see langword="null"/> when none was found.</param>
    /// <returns>The advice.</returns>
    public static ModelAdvice For(GraphicsCard? card) => card switch
    {
        null => new(null, WhisperModel.Base, ComputeDevice.Cpu, LanguageModelAdvice.Smaller),
        { MemoryMiB: >= FullChainMiB } => new(card, WhisperModel.LargeV3Turbo, ComputeDevice.Gpu, LanguageModelAdvice.Default),
        { MemoryMiB: >= TurboMiB } => new(card, WhisperModel.LargeV3Turbo, ComputeDevice.Gpu, LanguageModelAdvice.Smaller),
        _ => new(card, WhisperModel.Small, ComputeDevice.Gpu, LanguageModelAdvice.Smaller),
    };
}
