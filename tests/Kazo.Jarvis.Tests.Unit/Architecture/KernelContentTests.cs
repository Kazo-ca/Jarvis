using System.Reflection;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>The kernel holds no UI component (ADR 0002, sections 3 and 4).</summary>
[TestFixture]
public sealed class KernelContentTests
{
    private static readonly string[] UiFilePatterns = ["*.razor", "*.cshtml", "*.css", "*.js"];

    [Test]
    public void Kernel_holds_no_razor_component_type()
    {
        var components = Assembly.Load("Kazo.Jarvis.Kernel").GetTypes()
            .Where(type => type.GetInterfaces().Any(contract => contract.FullName == "Microsoft.AspNetCore.Components.IComponent"))
            .Select(type => type.FullName);

        Assert.That(components, Is.Empty);
    }

    [Test]
    public void Kernel_holds_no_markup_style_or_script_file()
    {
        var folder = RepositoryLayout.ProjectFolder("Kazo.Jarvis.Kernel");
        var files = UiFilePatterns
            .SelectMany(pattern => RepositoryLayout.SourceFiles(folder, pattern))
            .Select(RepositoryLayout.Relative);

        Assert.That(files, Is.Empty);
    }
}
