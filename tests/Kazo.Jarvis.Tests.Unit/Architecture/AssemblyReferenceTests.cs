using System.Reflection;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// Direction of the dependencies, checked on the compiled assemblies (ADR 0002, sections 1 and 7).
/// Complements <see cref="ProjectReferenceTests"/>: catches a reference that arrives through a package.
/// </summary>
[TestFixture]
public sealed class AssemblyReferenceTests
{
    private static readonly string[] Hosts = ["Kazo.Jarvis.Windows", "Kazo.Jarvis.Web", "Kazo.Jarvis.Mobile"];

    [Test]
    public void Features_references_neither_adapters_nor_a_host()
    {
        var forbidden = Hosts.Append("Kazo.Jarvis.Adapters");

        Assert.That(ReferencedNames("Kazo.Jarvis.Features"), Has.None.AnyOf(forbidden.ToArray()));
    }

    [Test]
    public void Adapters_references_neither_features_nor_a_host()
    {
        var forbidden = Hosts.Append("Kazo.Jarvis.Features");

        Assert.That(ReferencedNames("Kazo.Jarvis.Adapters"), Has.None.AnyOf(forbidden.ToArray()));
    }

    [Test]
    public void Kernel_references_only_the_base_library_and_extension_abstractions()
    {
        var offenders = ReferencedNames("Kazo.Jarvis.Kernel").Where(name => !IsAllowedInKernel(name));

        Assert.That(offenders, Is.Empty,
            "The kernel references no project and no package other than the Microsoft.Extensions abstractions (ADR 0002, section 4).");
    }

    /// <summary>Base class library, plus the abstractions and the primitives they depend on.</summary>
    private static bool IsAllowedInKernel(string name) =>
        name is "netstandard" or "mscorlib" or "Microsoft.Extensions.Primitives"
        || name.StartsWith("System", StringComparison.Ordinal)
        || (name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && name.EndsWith(".Abstractions", StringComparison.Ordinal));

    private static string[] ReferencedNames(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();
}
