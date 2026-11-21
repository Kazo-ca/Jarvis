using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Tasks.Board;

/// <summary>
/// Tells the state machine how many tasks work in the background (ADR 0005, section 1; ADR 0011, section 4): the
/// orb then shows the "long task" look and its ring. A task counts while it is <c>pending</c> or <c>working</c>;
/// one that waits for the user is shown by the amber badge instead, and one whose turn ended no longer works.
/// </summary>
public sealed partial class TaskCount : IHostedService, IDisposable
{
    private readonly ITaskBoard board;
    private readonly IMessageBus bus;
    private readonly ILogger<TaskCount> logger;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private int told;

    /// <summary>Creates the counter.</summary>
    /// <param name="board">The tasks.</param>
    /// <param name="bus">Where the count goes.</param>
    /// <param name="logger">Logs a count that could not be told.</param>
    public TaskCount(ITaskBoard board, IMessageBus bus, ILogger<TaskCount> logger)
    {
        this.board = board;
        this.bus = bus;
        this.logger = logger;
    }

    /// <summary>How many tasks of <paramref name="tasks"/> work in the background: sub-tasks aside.</summary>
    /// <param name="tasks">The tasks of the board.</param>
    /// <returns>The count.</returns>
    public static int Working(IEnumerable<WorkTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        return tasks.Count(task => task.ParentId is null && task.Status is WorkTaskStatus.Pending or WorkTaskStatus.Working);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        board.Changed += OnChanged;
        OnChanged();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        board.Changed -= OnChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => oneAtATime.Dispose();

    // Raised on any thread, sometimes inside a write of the board: the count is told outside of it, in order.
    private void OnChanged() => _ = Task.Run(TellAsync);

    private async Task TellAsync()
    {
        try
        {
            await oneAtATime.WaitAsync().ConfigureAwait(false);
            try
            {
                var count = Working(board.Tasks);
                if (count != told)
                {
                    told = count;
                    await bus.PublishAsync(new DelegatedTaskCountChanged(count)).ConfigureAwait(false);
                }
            }
            finally
            {
                oneAtATime.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // The application is closing.
        }
        catch (Exception exception)
        {
            LogFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The number of working tasks could not be told to the assistant.")]
    private partial void LogFailed(Exception exception);
}
