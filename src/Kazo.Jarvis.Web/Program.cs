using Kazo.Jarvis.Features;
using Kazo.Jarvis.Web.Components;
using Kazo.Jarvis.Web.Simulated;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
