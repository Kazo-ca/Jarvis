using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Kazo.Jarvis.Adapters.LocalServer;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.LocalServer;

/// <summary>
/// The local server (ADR 0021, section 3), on a real port of the loopback address: no route answers without a
/// token, the token of the user's hooks opens <c>/hook/…</c> only, the server is bound to <c>127.0.0.1</c> and
/// nothing else, and neither a failing route nor a port already taken stops it.
/// </summary>
[TestFixture]
public sealed class KestrelLocalServerTests
{
    private const string UserToken = "user-hook-token";

    private TemporaryFolder folder = null!;
    private JsonFileSettingsStore settings = null!;
    private FakeVault vault = null!;
    private FakeRoute hook = null!;
    private FakeRoute other = null!;
    private KestrelLocalServer server = null!;
    private HttpClient client = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], new FakeTimeProvider());
        vault = new FakeVault();
        vault.Secrets[LocalServerOptions.UserHookToken.Value] = UserToken;
        hook = new FakeRoute(HookPaths.Start);
        other = new FakeRoute("/tools/other");
        server = new KestrelLocalServer(settings, [hook, other], vault, null, fixedPort: 0);
        await server.StartAsync(CancellationToken.None);
        client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Status.Port}") };
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await server.DisposeAsync();
        folder.Dispose();
    }

    [Test]
    public void The_server_listens_on_a_port_of_the_loopback_address()
    {
        Assert.Multiple(() =>
        {
            Assert.That(server.Status.Listening, Is.True);
            Assert.That(server.Status.Port, Is.GreaterThan(0));
            Assert.That(server.Status.Problem, Is.Null);
            Assert.That(server.SessionToken, Has.Length.EqualTo(64), "32 random bytes.");
        });
    }

    [Test]
    public void The_server_does_not_answer_on_another_address_of_the_computer()
    {
        var addresses = Dns.GetHostAddresses(Dns.GetHostName())
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .ToList();
        if (addresses.Count == 0)
        {
            Assert.Ignore("This computer has no address other than the loopback one.");
        }

        foreach (var address in addresses)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Assert.That(
                () => socket.Connect(new IPEndPoint(address, server.Status.Port)),
                Throws.InstanceOf<SocketException>(),
                $"The port must be closed on {address}.");
        }
    }

    [Test]
    public async Task A_request_without_a_token_is_refused_whatever_the_path()
    {
        using var known = await PostAsync(HookPaths.Start, null);
        using var unknown = await PostAsync("/nothing/here", null);
        using var wrong = await PostAsync(HookPaths.Start, "not-the-token");
        using var basic = await SendAsync(HttpMethod.Post, HookPaths.Start, new AuthenticationHeaderValue("Basic", server.SessionToken));

        Assert.Multiple(() =>
        {
            Assert.That(known.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "Without a token, a caller does not learn which routes exist.");
            Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(basic.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(hook.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task The_token_of_the_run_opens_every_route_as_a_session_of_Jarvis()
    {
        using var first = await PostAsync(HookPaths.Start, server.SessionToken, """{"session_id":"s1"}""", ("X-Relais-Task", "task-1"));
        using var second = await PostAsync("/tools/other", server.SessionToken, "{}");

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(hook.Requests, Has.Count.EqualTo(1));
            Assert.That(hook.Requests[0].Caller, Is.EqualTo(LocalCaller.JarvisSession));
            Assert.That(hook.Requests[0].Body, Is.EqualTo("""{"session_id":"s1"}"""));
            Assert.That(hook.Requests[0].TaskId, Is.EqualTo("task-1"));
            Assert.That(hook.Requests[0].Headers.Keys, Has.None.EqualTo("Authorization").IgnoreCase, "A route never sees the token.");
            Assert.That(other.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task The_token_of_the_users_hooks_opens_the_hook_routes_only()
    {
        using var onHook = await PostAsync(HookPaths.Start, UserToken, "{}");
        using var elsewhere = await PostAsync("/tools/other", UserToken, "{}");

        Assert.Multiple(() =>
        {
            Assert.That(onHook.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(hook.Requests.Single().Caller, Is.EqualTo(LocalCaller.UserHook));
            Assert.That(elsewhere.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(other.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Without_a_token_in_the_vault_no_user_hook_is_accepted()
    {
        vault.Secrets.Clear();

        using var response = await PostAsync(HookPaths.Start, UserToken, "{}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task An_unknown_path_and_another_method_are_refused_after_the_token()
    {
        using var unknown = await PostAsync("/hook/unknown", server.SessionToken, "{}");
        using var get = await SendAsync(HttpMethod.Get, HookPaths.Start, new AuthenticationHeaderValue("Bearer", server.SessionToken));

        Assert.Multiple(() =>
        {
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(hook.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task The_answer_of_a_route_is_given_back()
    {
        hook.Answer = new LocalResponse(200, """{"ok":true}""");

        using var response = await PostAsync(HookPaths.Start, server.SessionToken, "{}");

        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""{"ok":true}"""));
            Assert.That(response.Headers.Contains("Server"), Is.False);
        });
    }

    [Test]
    public async Task A_route_that_fails_does_not_stop_the_server_nor_tell_why()
    {
        hook.Failure = new InvalidOperationException("secret detail");

        using var failed = await PostAsync(HookPaths.Start, server.SessionToken, "{}");
        hook.Failure = null;
        using var after = await PostAsync(HookPaths.Start, server.SessionToken, "{}");

        Assert.Multiple(async () =>
        {
            Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await failed.Content.ReadAsStringAsync(), Is.Empty);
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
    }

    [Test]
    public async Task A_body_larger_than_the_limit_is_refused()
    {
        var large = new string('a', (int)KestrelLocalServer.MostBody + 1);

        using var response = await PostAsync(HookPaths.Start, server.SessionToken, large);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
            Assert.That(hook.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task A_port_already_taken_is_told_and_does_not_throw()
    {
        await using var second = new KestrelLocalServer(settings, [], vault, null, fixedPort: server.Status.Port);
        var changes = 0;
        second.Changed += () => changes++;

        await second.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(second.Status.Listening, Is.False);
            Assert.That(second.Status.Port, Is.EqualTo(server.Status.Port));
            Assert.That(second.Status.Problem, Is.Not.Null.And.Not.Empty);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(server.Status.Listening, Is.True);
        });
    }

    [Test]
    public async Task A_new_port_in_the_settings_moves_the_server()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var free = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await settings.WriteAsync(LocalServerOptions.Section, new LocalServerOptions { Port = free });
        await using var moving = new KestrelLocalServer(settings, [hook], vault);
        await moving.StartAsync(CancellationToken.None);
        Assert.That(moving.Status, Is.EqualTo(new LocalServerStatus(true, free)));

        listener.Start();
        var next = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var moved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        moving.Changed += () => moved.TrySetResult();
        await settings.WriteAsync(LocalServerOptions.Section, new LocalServerOptions { Port = next });
        await moved.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(moving.Status, Is.EqualTo(new LocalServerStatus(true, next)));
    }

    [Test]
    public async Task A_stopped_server_no_longer_listens()
    {
        var port = server.Status.Port;

        await server.StopAsync(CancellationToken.None);

        Assert.That(server.Status.Listening, Is.False);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Assert.That(() => socket.Connect(new IPEndPoint(IPAddress.Loopback, port)), Throws.InstanceOf<SocketException>());
    }

    [Test]
    public async Task The_command_written_in_the_users_settings_posts_the_body_of_the_hook_and_fails_in_silence_once_Jarvis_is_closed()
    {
        // The very line of the installed hooks, run by a shell as Claude Code does, with the body on the standard input.
        var command = Kazo.Jarvis.Features.ClaudeCode.Hooks.HookEntries.Command(server.Status.Port, UserToken, HookPaths.Start);
        if (!OperatingSystem.IsWindows())
        {
            command = command.Replace("curl.exe", "curl", StringComparison.Ordinal);
        }

        var (code, output) = await RunAsync(command, """{"session_id":"s-9","prompt":"Bonjour"}""");
        if (code == 127)
        {
            Assert.Ignore("curl is not on this computer.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output, Is.Empty, "An empty answer: Claude Code has nothing to read.");
            Assert.That(hook.Requests.Single().Caller, Is.EqualTo(LocalCaller.UserHook));
            Assert.That(hook.Requests.Single().Body, Is.EqualTo("""{"session_id":"s-9","prompt":"Bonjour"}"""));
        });

        await server.StopAsync(CancellationToken.None);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var (closedCode, closedOutput) = await RunAsync(command, "{}");

        Assert.Multiple(() =>
        {
            Assert.That(closedCode, Is.Not.Zero);
            Assert.That(closedOutput, Is.Empty, "Jarvis closed: the hook says nothing.");
            Assert.That(started.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4)), "Bounded by curl itself (-m 2).");
        });
    }

    private static async Task<(int Code, string Output)> RunAsync(string command, string input)
    {
        // cmd.exe reads its line as it is: given as a list, the arguments would be quoted again with \", which cmd.exe
        // does not understand, and the headers of the hook would be cut in pieces (seen on Windows).
        var start = OperatingSystem.IsWindows()
            ? new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + command)
            : new System.Diagnostics.ProcessStartInfo("/bin/sh", ["-c", command]);
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.UseShellExecute = false;
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output);
    }

    private Task<HttpResponseMessage> PostAsync(string path, string? token, string body = "{}", params (string Name, string Value)[] headers) =>
        SendAsync(HttpMethod.Post, path, token is null ? null : new AuthenticationHeaderValue("Bearer", token), body, headers);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, AuthenticationHeaderValue? authorization, string body = "{}", params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = authorization;
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    private sealed class FakeRoute(string path) : ILocalRoute
    {
        public List<LocalRequest> Requests { get; } = [];

        public LocalResponse Answer { get; set; } = LocalResponse.Done;

        public Exception? Failure { get; set; }

        public string Path => path;

        public ValueTask<LocalResponse> HandleAsync(LocalRequest request, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Requests.Add(request);
            return ValueTask.FromResult(Answer);
        }
    }
}
