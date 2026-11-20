namespace Kazo.Jarvis.Kernel.Hardware;

/// <summary>An NVIDIA graphics card, as the driver describes it.</summary>
/// <param name="Name">Its name, for instance <c>NVIDIA GeForce RTX 3060</c>.</param>
/// <param name="MemoryMiB">Its memory in mebibytes.</param>
public sealed record GraphicsCard(string Name, long MemoryMiB);

/// <summary>
/// Finds the NVIDIA graphics card, so that the first-run wizard proposes models that fit it (stage 1 plan, work
/// item 14): Whisper and the local model need CUDA to reach their target delay (ADR 0006, section 4).
/// </summary>
public interface IGraphicsCardProbe
{
    /// <summary>Looks for an NVIDIA card. Never throws but for cancellation.</summary>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The card with the most memory, or <see langword="null"/> when there is none or it cannot be read.</returns>
    Task<GraphicsCard?> FindNvidiaCardAsync(CancellationToken cancellationToken = default);
}
