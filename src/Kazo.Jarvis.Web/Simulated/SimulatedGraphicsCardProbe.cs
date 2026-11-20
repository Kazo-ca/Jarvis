using Kazo.Jarvis.Kernel.Hardware;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IGraphicsCardProbe"/>: the card of Benoit's PC, an RTX 3060 with 12 GB, unless a test sets
/// <see cref="Card"/> to another one or to none.
/// </summary>
public sealed class SimulatedGraphicsCardProbe : IGraphicsCardProbe
{
    /// <summary>The card reported; <see langword="null"/> for a PC without an NVIDIA card.</summary>
    public GraphicsCard? Card { get; set; } = new("NVIDIA GeForce RTX 3060", 12_288);

    /// <inheritdoc />
    public Task<GraphicsCard?> FindNvidiaCardAsync(CancellationToken cancellationToken = default) => Task.FromResult(Card);
}
