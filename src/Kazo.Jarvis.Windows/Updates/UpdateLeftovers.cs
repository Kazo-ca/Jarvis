using System.IO;
using Kazo.Jarvis.Adapters.Updates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Windows.Updates;

/// <summary>
/// At start, deletes what the last update left: the copy of the previous version that the updater keeps in
/// the installation folder (<c>.kazo-update-backup-*</c>) and the prepared folders under
/// <c>%USERPROFILE%\.kazo\jarvis\updates</c>. A folder still in use stays until the next start.
/// </summary>
internal sealed partial class UpdateLeftovers : IHostedService
{
    /// <summary>Prefix of the backup folders; the updater uses the same one.</summary>
    public const string BackupPrefix = ".kazo-update-backup";

    private readonly GitHubReleaseSource releases;
    private readonly ILogger<UpdateLeftovers> logger;

    /// <summary>Creates the clean-up.</summary>
    public UpdateLeftovers(GitHubReleaseSource releases, ILogger<UpdateLeftovers> logger)
    {
        this.releases = releases;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Off the start-up path: deleting a full copy of the previous version takes a moment.
        _ = Task.Run(Clean, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Clean()
    {
        try
        {
            foreach (var folder in Directory.GetDirectories(UpdaterLauncher.InstallFolder, BackupPrefix + "*"))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                    LogDeleted(folder);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    LogKept(folder, exception.Message);
                }
            }

            releases.DeleteLeftovers();
        }
        catch (Exception exception)
        {
            LogFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted the previous version kept by the updater: {Folder}.")]
    private partial void LogDeleted(string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "The previous version in {Folder} is still in use ({Reason}); deleted at the next start.")]
    private partial void LogKept(string folder, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The leftovers of the last update could not be cleaned.")]
    private partial void LogFailed(Exception exception);
}
