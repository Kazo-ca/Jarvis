using Kazo.Jarvis.Adapters;
using Kazo.Jarvis.Features;
using Kazo.Jarvis.Features.Assistant.Orb;
using Kazo.Jarvis.Kernel.Agents;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Tests.Unit.Adapters;

/// <summary>
/// The services of the Windows host built together, the real adapters with every slice, as <c>Program.cs</c> registers
/// them. Nothing here runs on Windows: the test only shows that the services of the agents, the projects, the
/// call mode and the projects view (ADR 0025, ADR 0026) can all be built, without a cycle and without a wait
/// that never ends.
/// </summary>
[TestFixture]
public sealed class WindowsCompositionTests
{
    [Test]
    public async Task The_adapters_and_every_slice_are_built_together_as_the_windows_host_does()
    {
        using var folder = new TemporaryFolder();
        var services = new ServiceCollection().AddJarvisAdapters(folder.Paths);
        services.AddLogging();
        // What AddJarvisWindowsHost registers: windows, sound and hotkey, which exist on Windows only.
        services.TryAddSingleton<IDialogService, Dialogs>();
        services.TryAddSingleton<IPointerTimings>(new Timings());
        services.TryAddSingleton(Native<IOrbWindow>());
        services.TryAddSingleton(Native<ITaskFanSurface>());
        services.TryAddSingleton(Native<IGlobalHotkey>());
        services.TryAddSingleton(Native<ILaunchAtLogin>());
        services.TryAddSingleton(Native<Kazo.Jarvis.Kernel.Updates.IUpdateLauncher>());
        services.TryAddSingleton<Kazo.Jarvis.Kernel.Localization.ISystemLanguage>(new French());
        services.TryAddSingleton(Native<Kazo.Jarvis.Kernel.Audio.IAudioCapture>());
        services.TryAddSingleton(Native<Kazo.Jarvis.Kernel.Audio.IAudioPlayback>());
        services.AddJarvisFeatures();
        await using var provider = services.BuildServiceProvider();

        // On its own thread, with a limit: a cycle between two factories is a wait without end, not an exception.
        var built = Task.Run(() =>
        {
            var hosted = provider.GetServices<IHostedService>().Count();
            _ = provider.GetRequiredService<OrbActivation>();
            _ = provider.GetRequiredService<IAgentCatalog>();
            _ = provider.GetRequiredService<IProjectCatalog>();
            _ = provider.GetRequiredService<IClaudeTasks>();
            return (Hosted: hosted, ProjectsView: provider.GetServices<IEventHandler<ProjectsViewRequested>>().Count());
        });

        var ended = await Task.WhenAny(built, Task.Delay(TimeSpan.FromSeconds(30))) == built;
        Assert.That(ended, Is.True, "building the services never ends");
        var result = await built;
        Assert.Multiple(() =>
        {
            Assert.That(result.Hosted, Is.GreaterThan(0));
            Assert.That(result.ProjectsView, Is.EqualTo(1), "The double click on the orb has its answer.");
        });
    }

    // A native service that does nothing: building the services must not need more than its presence.
    private static T Native<T>()
        where T : class => System.Reflection.DispatchProxy.Create<T, Nothing>();

    /// <summary>Answers every call with the empty value of its return type.</summary>
    public class Nothing : System.Reflection.DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            var type = targetMethod!.ReturnType;
            if (type == typeof(Task))
            {
                return Task.CompletedTask;
            }

            return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }

    private sealed record French : Kazo.Jarvis.Kernel.Localization.ISystemLanguage
    {
        public System.Globalization.CultureInfo DisplayCulture => System.Globalization.CultureInfo.GetCultureInfo("fr-CA");
    }

    private sealed record Timings : IPointerTimings
    {
        public TimeSpan DoubleClickTime => TimeSpan.FromMilliseconds(500);
    }

    private sealed class Dialogs : IDialogService
    {
        public event Action<string>? Closed
        {
            add { }
            remove { }
        }

        public void Open(DialogRequest request)
        {
        }

        public void Close(string key)
        {
        }

        public bool IsOpen(string key) => false;
    }
}
