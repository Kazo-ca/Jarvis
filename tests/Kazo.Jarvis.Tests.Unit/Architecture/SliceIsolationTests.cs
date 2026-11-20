using System.Reflection;
using Kazo.Jarvis.Features;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>A slice never uses another slice (ADR 0002, section 3, rule 1).</summary>
[TestFixture]
public sealed class SliceIsolationTests
{
    private const string FeaturesRoot = "Kazo.Jarvis.Features";
    private const string SamplesRoot = "Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features";

    private static readonly Assembly FeaturesAssembly = typeof(FeaturesServiceCollectionExtensions).Assembly;
    private static readonly Assembly SamplesAssembly = typeof(SliceIsolationTests).Assembly;

    [Test]
    public void No_slice_uses_another_slice()
    {
        Assert.That(SliceIsolationRule.Violations(FeaturesAssembly, FeaturesRoot), Is.Empty);
    }

    [Test]
    public void No_type_sits_directly_in_a_domain_folder()
    {
        Assert.That(SliceIsolationRule.TypesInDomainFolders(FeaturesAssembly, FeaturesRoot), Is.Empty,
            "Put the type in a slice folder, or in Features/Shared/ if it is a presentation component used by three slices or more.");
    }

    // The tests below run the rule on samples, so that it is proven to fail while Features holds no slice yet.

    [Test]
    public void Rule_finds_the_sample_slices_but_not_shared()
    {
        Assert.That(SliceIsolationRule.Slices(SamplesAssembly, SamplesRoot), Is.EqualTo(new[]
        {
            $"{SamplesRoot}.Alpha.One",
            $"{SamplesRoot}.Alpha.OneMore",
            $"{SamplesRoot}.Beta.Two",
        }));
    }

    [Test]
    public void Rule_reports_a_slice_that_uses_another_slice()
    {
        var violations = SliceIsolationRule.Violations(SamplesAssembly, SamplesRoot);

        Assert.That(violations, Has.Length.EqualTo(2));
        Assert.That(violations, Has.Some.StartsWith($"{SamplesRoot}.Alpha.One.UsesBetaInSignature"));
        Assert.That(violations, Has.Some.StartsWith($"{SamplesRoot}.Alpha.One.UsesBetaInBody"));
    }

    [Test]
    public void Rule_reports_a_type_placed_directly_in_a_domain_folder()
    {
        Assert.That(SliceIsolationRule.TypesInDomainFolders(SamplesAssembly, SamplesRoot),
            Is.EqualTo(new[] { $"{SamplesRoot}.Beta.DomainLevelHelper" }));
    }
}
