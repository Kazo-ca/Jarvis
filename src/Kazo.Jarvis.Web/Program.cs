using Kazo.Jarvis.Features;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Web.Components;
using Kazo.Jarvis.Web.Simulated;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// An existing profile, so every test starts on the orb; the first-run wizard opens from its button of the page
// (the in-memory store alone would simulate a first run and open it at start).
builder.Services.AddSingleton<ISettingsStore>(new InMemorySettingsStore(new SettingsLoadState(SettingsLoadOutcome.Loaded)));

// Simulated contracts first, then the slices (ADR 0002, section 6).
builder.Services.AddJarvisSimulatedServices();
builder.Services.AddJarvisFeatures();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(FeaturesServiceCollectionExtensions).Assembly);

app.Run();

/// <summary>Entry point, public so the end-to-end tests can start the host.</summary>
public partial class Program;
