using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Lifetime of the generic host in a desktop application: the WPF application decides when it ends (the
/// Quit menu), not Ctrl+C on a console it does not have (ADR 0013, section 4).
/// </summary>
internal sealed class DesktopLifetime : IHostLifetime
{
    /// <inheritdoc />
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
