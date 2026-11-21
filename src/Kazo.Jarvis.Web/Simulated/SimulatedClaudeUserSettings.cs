using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// The user's Claude Code settings in memory (ADR 0003, section 3), with a setting and a hook of the user's own,
/// to show that an installation keeps them. The test page can make the file one that is not JSON.
/// </summary>
public sealed class SimulatedClaudeUserSettings : IClaudeUserSettings
{
    private const string Path = @"C:\Users\test\.claude\settings.json";

    private const string Own = """
        {
          "model": "opus",
          "hooks": {
            "Stop": [
              { "hooks": [ { "type": "command", "command": "echo done" } ] }
            ]
          }
        }
        """;

    private readonly Lock gate = new();
    private string text = Own;
    private int copies;

    /// <summary>Raised when the content changes, for the test page.</summary>
    public event EventHandler? Changed;

    /// <summary>The content of now.</summary>
    public string Text
    {
        get
        {
            lock (gate)
            {
                return text;
            }
        }
    }

    /// <summary>How many dated copies were made, one per writing.</summary>
    public int Copies
    {
        get
        {
            lock (gate)
            {
                return copies;
            }
        }
    }

    /// <summary>Makes the file one that is not JSON, as after a wrong edit by hand.</summary>
    public void Break()
    {
        lock (gate)
        {
            text = "{ \"model\": \"opus\", // my model\n";
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Puts the file back as at the start.</summary>
    public void Reset()
    {
        lock (gate)
        {
            text = Own;
            copies = 0;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public ValueTask<ClaudeUserSettingsFile> ReadAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new ClaudeUserSettingsFile(ClaudeUserSettingsState.Read, Path, Text));

    /// <inheritdoc />
    public ValueTask<string?> WriteAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        string copy;
        lock (gate)
        {
            this.text = text;
            copies++;
            copy = $"{Path}.jarvis-20261121-09000{copies}.bak";
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return ValueTask.FromResult<string?>(copy);
    }
}
