using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.ClaudeCode.Hooks;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.ClaudeCode;

/// <summary>
/// The installation of the hooks (ADR 0021, section 6): reading writes nothing to the user's file; the token
/// lives in the vault and leaves it with the hooks; a file that cannot be read or written stays as it was.
/// </summary>
[TestFixture]
public sealed class ClaudeHooksTests
{
    private TemporaryFolder folder = null!;
    private JsonFileSettingsStore settings = null!;
    private FakeVault vault = null!;
    private FakeFile file = null!;
    private ClaudeHooks hooks = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], new FakeTimeProvider());
        vault = new FakeVault();
        file = new FakeFile();
        hooks = new ClaudeHooks(settings, file, vault);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task Reading_shows_the_lines_and_writes_nothing_to_the_users_file()
    {
        var view = await hooks.ReadAsync();

        var token = vault.Secrets[LocalServerOptions.UserHookToken.Value];
        Assert.Multiple(() =>
        {
            Assert.That(view.Available, Is.True);
            Assert.That(view.State, Is.EqualTo(HookInstallState.None));
            Assert.That(view.FileMissing, Is.True);
            Assert.That(view.Port, Is.EqualTo(LocalServerOptions.DefaultPort));
            Assert.That(token, Has.Length.EqualTo(64));
            Assert.That(view.Preview, Does.Contain($"Bearer {token}"));
            Assert.That(view.Preview, Does.Contain($"http://127.0.0.1:{LocalServerOptions.DefaultPort}/hook/task.start"));
            Assert.That(file.Writes, Is.Zero);
        });
    }

    [Test]
    public async Task The_token_is_drawn_once()
    {
        await hooks.ReadAsync();
        var first = vault.Secrets[LocalServerOptions.UserHookToken.Value];

        await hooks.ReadAsync();
        await hooks.InstallAsync();

        Assert.That(vault.Secrets[LocalServerOptions.UserHookToken.Value], Is.EqualTo(first));
    }

    [Test]
    public async Task An_installation_writes_once_and_tells_the_copy()
    {
        file.Text = """{ "model": "opus" }""";

        var result = await hooks.InstallAsync();
        var view = await hooks.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new ClaudeHooksResult(ClaudeHooksOutcome.Done, "copy-1")));
            Assert.That(file.Writes, Is.EqualTo(1));
            Assert.That(file.Text, Does.Contain("\"model\": \"opus\""));
            Assert.That(view.State, Is.EqualTo(HookInstallState.Installed));
        });
    }

    [Test]
    public async Task A_change_of_port_makes_the_installed_hooks_outdated()
    {
        await hooks.InstallAsync();

        await settings.WriteAsync(LocalServerOptions.Section, new LocalServerOptions { Port = 50123 });
        var view = await hooks.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.State, Is.EqualTo(HookInstallState.Outdated));
            Assert.That(view.Preview, Does.Contain("http://127.0.0.1:50123/hook/task.start"));
        });
    }

    [Test]
    public async Task A_removal_takes_the_hooks_away_and_erases_the_token()
    {
        file.Text = """{ "model": "opus" }""";
        await hooks.InstallAsync();

        var result = await hooks.RemoveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ClaudeHooksOutcome.Done));
            Assert.That(file.Writes, Is.EqualTo(2));
            Assert.That(file.Text, Does.Not.Contain("/hook/"));
            Assert.That(file.Text, Does.Contain("opus"));
            Assert.That(vault.Secrets, Is.Empty, "The lines kept elsewhere open nothing any more.");
        });
    }

    [Test]
    public async Task A_removal_with_nothing_to_remove_does_not_touch_the_file()
    {
        file.Text = """{ "model": "opus" }""";

        var result = await hooks.RemoveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ClaudeHooksOutcome.Nothing));
            Assert.That(file.Writes, Is.Zero);
        });
    }

    [TestCase("{ // comment\n }")]
    [TestCase("{ \"hooks\": [] }")]
    public async Task A_file_that_is_not_plain_settings_is_never_written(string text)
    {
        file.Text = text;

        var view = await hooks.ReadAsync();
        var installed = await hooks.InstallAsync();
        var removed = await hooks.RemoveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.State, Is.EqualTo(HookInstallState.Unreadable));
            Assert.That(view.Preview, Is.Not.Null, "The lines to paste by hand.");
            Assert.That(installed.Outcome, Is.EqualTo(ClaudeHooksOutcome.Unreadable));
            Assert.That(removed.Outcome, Is.EqualTo(ClaudeHooksOutcome.Unreadable));
            Assert.That(file.Writes, Is.Zero);
            Assert.That(file.Text, Is.EqualTo(text));
        });
    }

    [Test]
    public async Task A_file_that_cannot_be_opened_is_never_written()
    {
        file.Unreadable = true;

        var view = await hooks.ReadAsync();
        var installed = await hooks.InstallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.State, Is.EqualTo(HookInstallState.Unreadable));
            Assert.That(installed.Outcome, Is.EqualTo(ClaudeHooksOutcome.Unreadable));
            Assert.That(file.Writes, Is.Zero);
        });
    }

    [Test]
    public async Task A_write_that_fails_is_told_and_keeps_the_token()
    {
        await hooks.InstallAsync();
        file.FailWrites = true;

        var installed = await hooks.InstallAsync();
        var removed = await hooks.RemoveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(installed.Outcome, Is.EqualTo(ClaudeHooksOutcome.Failed));
            Assert.That(removed.Outcome, Is.EqualTo(ClaudeHooksOutcome.Failed));
            Assert.That(vault.Secrets, Has.Count.EqualTo(1), "The hooks are still in the file: their token still works.");
        });
    }

    [Test]
    public async Task Without_a_vault_that_keeps_the_token_nothing_is_installed()
    {
        vault.WritesBeforeFailure = 0;

        var view = await hooks.ReadAsync();
        var installed = await hooks.InstallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.Preview, Is.Null);
            Assert.That(installed.Outcome, Is.EqualTo(ClaudeHooksOutcome.Unavailable));
            Assert.That(file.Writes, Is.Zero);
        });
    }

    [Test]
    public async Task A_host_without_the_services_says_so()
    {
        var bare = new ClaudeHooks(settings);

        var view = await bare.ReadAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(view.Available, Is.False);
            Assert.That((await bare.InstallAsync()).Outcome, Is.EqualTo(ClaudeHooksOutcome.Unavailable));
            Assert.That((await bare.RemoveAsync()).Outcome, Is.EqualTo(ClaudeHooksOutcome.Unavailable));
        });
    }

    private sealed class FakeFile : IClaudeUserSettings
    {
        public string? Text { get; set; }

        public bool Unreadable { get; set; }

        public bool FailWrites { get; set; }

        public int Writes { get; private set; }

        public ValueTask<ClaudeUserSettingsFile> ReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Unreadable
                ? new ClaudeUserSettingsFile(ClaudeUserSettingsState.Unreadable, "settings.json")
                : Text is null
                    ? new ClaudeUserSettingsFile(ClaudeUserSettingsState.Missing, "settings.json")
                    : new ClaudeUserSettingsFile(ClaudeUserSettingsState.Read, "settings.json", Text));

        public ValueTask<string?> WriteAsync(string text, CancellationToken cancellationToken = default)
        {
            if (FailWrites)
            {
                throw new IOException("The disk is full.");
            }

            var copy = Text is null ? null : $"copy-{Writes + 1}";
            Text = text;
            Writes++;
            return ValueTask.FromResult(copy);
        }
    }
}
