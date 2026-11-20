using Kazo.Jarvis.Kernel.Models;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IModelInstaller"/>: nothing leaves the machine. A download takes 0.6 s in ten steps and
/// "installs" the target in memory, so every page load of a test starts with no model. A source whose host ends
/// with <c>.invalid</c> cannot be reached, and <see cref="FailNext"/> makes the next download fail.
/// </summary>
public sealed class SimulatedModelInstaller : IModelInstaller
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(60);

    private readonly Lock gate = new();
    private readonly HashSet<string> installed = new(StringComparer.Ordinal);

    /// <summary>When set, the next download fails with this cause, then the flag clears.</summary>
    public ModelDownloadFailure? FailNext { get; set; }

    /// <summary>The targets installed so far, in order.</summary>
    public IReadOnlyList<string> Installed
    {
        get
        {
            lock (gate)
            {
                return [.. installed];
            }
        }
    }

    /// <inheritdoc />
    public bool IsInstalled(ModelDownload download)
    {
        ArgumentNullException.ThrowIfNull(download);
        lock (gate)
        {
            return installed.Contains(download.Target);
        }
    }

    /// <inheritdoc />
    public async Task InstallAsync(ModelDownload download, IProgress<long>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(download);
        if (download.Source.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelDownloadException(ModelDownloadFailure.Unreachable, "Simulated: nothing answers.");
        }

        for (var step = 1; step <= 10; step++)
        {
            await Task.Delay(StepDelay, cancellationToken).ConfigureAwait(false);
            progress?.Report(download.Size * step / 10);
        }

        if (FailNext is { } failure)
        {
            FailNext = null;
            throw new ModelDownloadException(failure, "Simulated failure.");
        }

        lock (gate)
        {
            installed.Add(download.Target);
        }
    }
}
