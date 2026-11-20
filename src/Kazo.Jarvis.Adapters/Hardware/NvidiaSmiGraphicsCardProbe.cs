using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Kazo.Jarvis.Kernel.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Hardware;

/// <summary>
/// Finds the NVIDIA card through <c>nvidia-smi</c>, which the NVIDIA driver installs and puts on the path. Without
/// the driver there is no CUDA either (ADR 0006, section 4), so a missing program means "no NVIDIA card". Windows
/// itself reports the memory of a card on 32 bits only (4 GB at most), which is why the driver is asked.
/// </summary>
public sealed partial class NvidiaSmiGraphicsCardProbe : IGraphicsCardProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ILogger logger;

    /// <summary>Creates the probe.</summary>
    /// <param name="logger">Logs what was found; none when omitted.</param>
    public NvidiaSmiGraphicsCardProbe(ILogger<NvidiaSmiGraphicsCardProbe>? logger = null)
    {
        this.logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<GraphicsCard?> FindNvidiaCardAsync(CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo("nvidia-smi", "--query-gpu=name,memory.total --format=csv,noheader,nounits")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(Timeout);
            var output = await process.StandardOutput.ReadToEndAsync(bounded.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
            var card = process.ExitCode == 0 ? Parse(output) : null;
            LogFound(card?.Name ?? "none", card?.MemoryMiB ?? 0);
            return card;
        }
        catch (Win32Exception)
        {
            LogNoDriver();
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut();
            return null;
        }
    }

    /// <summary>Reads the lines of <c>nvidia-smi</c> (<c>name, memory</c>) and keeps the card with the most memory.</summary>
    /// <param name="output">The output.</param>
    /// <returns>The card, or <see langword="null"/> when no line can be read.</returns>
    internal static GraphicsCard? Parse(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(','))
            .Where(parts => parts.Length >= 2)
            .Select(parts => long.TryParse(parts[^1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var memory)
                ? new GraphicsCard(string.Join(',', parts[..^1]).Trim(), memory)
                : null)
            .Where(card => card is not null && card.MemoryMiB > 0)
            .OrderByDescending(card => card!.MemoryMiB)
            .FirstOrDefault();

    [LoggerMessage(Level = LogLevel.Information, Message = "NVIDIA card: {Name}, {MemoryMiB} MiB.")]
    private partial void LogFound(string name, long memoryMiB);

    [LoggerMessage(Level = LogLevel.Information, Message = "nvidia-smi not found: no NVIDIA driver.")]
    private partial void LogNoDriver();

    [LoggerMessage(Level = LogLevel.Warning, Message = "nvidia-smi did not answer in time.")]
    private partial void LogTimedOut();
}
