using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// Direction of the dependencies between projects (ADR 0002, section 1), read from the project files
/// so that a reference is caught even before any type uses it.
/// </summary>
[TestFixture]
public sealed partial class ProjectReferenceTests
{
    private const string Kernel = "Kazo.Jarvis.Kernel";
    private const string Features = "Kazo.Jarvis.Features";
    private const string Adapters = "Kazo.Jarvis.Adapters";
    private const string Windows = "Kazo.Jarvis.Windows";
    private const string Web = "Kazo.Jarvis.Web";

    /// <summary>
    /// The only project references allowed for each product project. Hosts sit at the top; Features and
    /// Adapters only see the kernel; the kernel sees nothing. The Web host never loads Adapters.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        [Kernel] = [],
        [Features] = [Kernel],
        [Adapters] = [Kernel],
        [Windows] = [Kernel, Features, Adapters],
        [Web] = [Kernel, Features],
    };

    [TestCaseSource(nameof(ProductProjects))]
    public void Project_references_follow_the_dependency_direction(string project)
    {
        var references = ProjectReferences(RepositoryLayout.ProjectFile(project));

        Assert.That(references, Is.SubsetOf(AllowedReferences[project]),
            $"{project} may only reference: {string.Join(", ", AllowedReferences[project].DefaultIfEmpty("no project"))}.");
    }

    [Test]
    public void Every_product_project_has_a_dependency_rule()
    {
        var projects = Directory.EnumerateDirectories(RepositoryLayout.Src).Select(Path.GetFileName);

        Assert.That(projects, Is.SubsetOf(AllowedReferences.Keys),
            "A new project under src/ needs its line in AllowedReferences (ADR 0001 and ADR 0002).");
    }

    [Test]
    public void Kernel_uses_no_package_other_than_extension_abstractions()
    {
        var packages = PackageReferences(RepositoryLayout.ProjectFile(Kernel));

        Assert.That(packages.Where(package => !AbstractionsPackage().IsMatch(package)), Is.Empty,
            "The kernel only takes Microsoft.Extensions.*.Abstractions packages (ADR 0002, section 4).");
    }

    [Test]
    public void No_product_or_test_project_references_a_spike()
    {
        var offenders = new[] { "src", "tests" }
            .SelectMany(folder => RepositoryLayout.SourceFiles(Path.Combine(RepositoryLayout.Root, folder), "*.csproj"))
            .SelectMany(file => RawProjectReferences(file).Select(reference => (file, reference)))
            .Where(item => item.reference.Replace('\\', '/').Contains("/spikes/", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{RepositoryLayout.Relative(item.file)} -> {item.reference}");

        Assert.That(offenders, Is.Empty, "Spike code is never referenced by product code (AGENTS.md, I.4).");
    }

    private static IEnumerable<string> ProductProjects() => AllowedReferences.Keys;

    private static IEnumerable<string> RawProjectReferences(string projectFile) =>
        XDocument.Load(projectFile).Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);

    private static string[] ProjectReferences(string projectFile) =>
        RawProjectReferences(projectFile)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToArray();

    private static string[] PackageReferences(string projectFile) =>
        XDocument.Load(projectFile).Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

    [GeneratedRegex(@"^Microsoft\.Extensions\.[A-Za-z.]+\.Abstractions$")]
    private static partial Regex AbstractionsPackage();
}
