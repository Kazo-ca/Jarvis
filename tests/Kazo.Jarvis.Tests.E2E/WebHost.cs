using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.E2E;

/// <summary>
/// Starts the Web test host once for the whole run (a set-up fixture in the root namespace runs
/// around every test of the assembly), on Kestrel and a free port, so that a real
/// browser can reach it (ADR 0003, section 3).
/// </summary>
[SetUpFixture]
public sealed class WebHost
{
    private static WebApplicationFactory<Program>? _factory;

    /// <summary>Address of the running host, ending with a slash.</summary>
    public static Uri BaseAddress { get; private set; } = new("http://localhost/");

    /// <summary>Starts the host before the first test.</summary>
    [OneTimeSetUp]
    public void Start()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        _factory.UseKestrel(0);
        _factory.StartServer();

        // Port 0 lets Kestrel pick a free port; the address it chose is read back from the server.
        var address = _factory.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        BaseAddress = new Uri(address.TrimEnd('/') + "/");
    }

    /// <summary>Stops the host after the last test.</summary>
    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }
}
