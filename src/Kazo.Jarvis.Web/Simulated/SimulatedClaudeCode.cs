using System.Text.Json;
using System.Threading.Channels;
using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Claude Code in memory, in place of the program (ADR 0003, section 3). A session follows a small script chosen
/// by words of its first message: it asks to write a file of its folder through the same
/// <see cref="IClaudeApprovals"/> the real <c>approve</c> tool reaches; with « outside » or « hors » it asks to
/// write outside its folder; with « without asking » or « sans demander » it runs a command that never asked;
/// with « wait » or « attends » it works until it is stopped. Then it gives its result and ends. A session that
/// is resumed takes its message as an answer and ends at once.
/// </summary>
public sealed class SimulatedClaudeCode : IClaudeCode
{
    /// <summary>The cost every simulated session announces, in US dollars.</summary>
    public const decimal Cost = 0.42m;

    private readonly IServiceProvider services;
    private readonly Lock gate = new();
    private readonly List<ClaudeLaunch> launches = [];

    /// <summary>Creates the simulation.</summary>
    /// <param name="services">Where the approvals are found when a session asks: the slice that owns them is built after this service.</param>
    public SimulatedClaudeCode(IServiceProvider services) => this.services = services;

    /// <summary>Raised after a session started or ended.</summary>
    public event EventHandler? Changed;

    /// <summary>The sessions started so far.</summary>
    public IReadOnlyList<ClaudeLaunch> Launches
    {
        get
        {
            lock (gate)
            {
                return [.. launches];
            }
        }
    }

    /// <summary>How the last session that ended ended: <c>done</c>, <c>stopped</c>, or empty.</summary>
    public string LastEnd { get; private set; } = string.Empty;

    /// <inheritdoc />
    public ValueTask<ClaudeCodeInstall> FindAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new ClaudeCodeInstall(true, ClaudeCodeInstall.Checked));

    /// <inheritdoc />
    public ValueTask<IClaudeRun> StartAsync(ClaudeLaunch launch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launch);
        lock (gate)
        {
            launches.Add(launch);
        }

        var run = new Run(this, launch);
        Changed?.Invoke(this, EventArgs.Empty);
        return ValueTask.FromResult<IClaudeRun>(run);
    }

    private void Ended(string how)
    {
        LastEnd = how;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Run : IClaudeRun
    {
        private readonly Channel<ClaudeRunEvent> events = Channel.CreateUnbounded<ClaudeRunEvent>();
        private readonly CancellationTokenSource stop = new();
        private readonly SimulatedClaudeCode owner;
        private readonly ClaudeLaunch launch;
        private readonly Task work;

        public Run(SimulatedClaudeCode owner, ClaudeLaunch launch)
        {
            this.owner = owner;
            this.launch = launch;
            work = Task.Run(WorkAsync);
        }

        public IAsyncEnumerable<ClaudeRunEvent> ReadAsync(CancellationToken cancellationToken = default) => events.Reader.ReadAllAsync(cancellationToken);

        public async ValueTask StopAsync()
        {
            await stop.CancelAsync().ConfigureAwait(false);
            await work.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            stop.Dispose();
        }

        private static bool Says(string message, params string[] words) =>
            words.Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase));

        private async Task WorkAsync()
        {
            var message = launch.FirstMessage;
            var denials = 0;
            try
            {
                events.Writer.TryWrite(new ClaudeStarted("simulated-" + launch.TaskId, launch.Model ?? "claude-sonnet-5-5"));
                if (launch.ResumeSession is { } resumed)
                {
                    // A session resumed with an answer of the user (ADR 0026, section 4): it takes the answer and ends.
                    events.Writer.TryWrite(new ClaudeTurnEnded($"Session simulée reprise ({resumed}). Réponse reçue : {message}", Cost, false, 0));
                    events.Writer.TryWrite(new ClaudeExited(0, null));
                    owner.Ended("done");
                    return;
                }

                if (Says(message, "wait", "attends"))
                {
                    await Task.Delay(Timeout.Infinite, stop.Token).ConfigureAwait(false);
                }

                if (Says(message, "without asking", "sans demander"))
                {
                    // What a session must never do: Jarvis stops it when it sees the result.
                    var command = JsonSerializer.SerializeToElement(new { command = "git push --force" });
                    events.Writer.TryWrite(new ClaudeToolUse("use-unasked", "Bash", command));
                    events.Writer.TryWrite(new ClaudeToolResult("use-unasked", false));
                    await Task.Delay(Timeout.Infinite, stop.Token).ConfigureAwait(false);
                }

                var path = Says(message, "outside", "hors") ? "/home/demo/Private/secret.txt" : launch.Folder.TrimEnd('/') + "/NOTES.md";
                var input = JsonSerializer.SerializeToElement(new { file_path = path, content = "Écrit par la session simulée." });
                events.Writer.TryWrite(new ClaudeToolUse("use-1", "Write", input));
                var approvals = Approvals();
                var answer = approvals is null
                    ? ClaudeApproval.Deny("No approvals on this host.")
                    : await approvals.AskAsync(new ClaudeApprovalRequest(launch.TaskId, "Write", input, "use-1"), stop.Token).ConfigureAwait(false);
                denials += answer.Allowed ? 0 : 1;
                events.Writer.TryWrite(new ClaudeToolResult("use-1", !answer.Allowed));
                var text = answer.Allowed
                    ? $"Session simulée : {path} est écrit. Demande reçue : {message}"
                    : $"Session simulée : l'écriture de {path} a été refusée ({answer.Message}). Demande reçue : {message}";
                events.Writer.TryWrite(new ClaudeTurnEnded(text, Cost, false, denials));
                events.Writer.TryWrite(new ClaudeExited(0, null));
                owner.Ended("done");
            }
            catch (OperationCanceledException)
            {
                events.Writer.TryWrite(new ClaudeExited(-1, null));
                owner.Ended("stopped");
            }
            finally
            {
                events.Writer.TryComplete();
            }

            IClaudeApprovals? Approvals() => owner.services.GetService(typeof(IClaudeApprovals)) as IClaudeApprovals;
        }
    }
}
