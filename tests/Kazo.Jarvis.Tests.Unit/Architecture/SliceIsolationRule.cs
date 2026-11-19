using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// Finds the types of a slice that use a type of another slice (ADR 0002, section 3, rule 1).
/// A slice is a namespace <c>&lt;root&gt;.&lt;Domain&gt;.&lt;Slice&gt;</c>; <c>&lt;root&gt;.Shared</c> is not a slice
/// and every slice may use it. Method bodies are inspected too (NetArchTest reads the IL).
/// </summary>
internal static class SliceIsolationRule
{
    /// <summary>Name of the folder of presentation components shared by every slice.</summary>
    public const string SharedFolder = "Shared";

    /// <summary>The slices found under <paramref name="root"/>, e.g. <c>Kazo.Jarvis.Features.Voice.WakeWord</c>.</summary>
    public static string[] Slices(Assembly assembly, string root) =>
        NamespacesUnder(assembly, root)
            .Select(ns => ns[(root.Length + 1)..].Split('.'))
            .Where(segments => segments.Length >= 2 && segments[0] != SharedFolder)
            .Select(segments => $"{root}.{segments[0]}.{segments[1]}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>One line per type that depends on another slice.</summary>
    public static string[] Violations(Assembly assembly, string root)
    {
        var slices = Slices(assembly, root);
        var violations = new List<string>();

        foreach (var slice in slices)
        {
            // NetArchTest matches dependencies by whole namespace segments: "Voice.Wake" does not
            // match "Voice.WakeWord" (proven by the Alpha.OneMore sample).
            var others = slices.Where(other => other != slice).ToArray();
            if (others.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(assembly)
                .That().ResideInNamespaceMatching($"^{Regex.Escape(slice)}(\\..*)?$")
                .ShouldNot().HaveDependencyOnAny(others)
                .GetResult();

            violations.AddRange((result.FailingTypeNames ?? []).Select(type => $"{type} uses another slice"));
        }

        return [.. violations];
    }

    /// <summary>
    /// Types placed directly in a domain folder. A domain is only a folder and holds no code
    /// shared by its slices (ADR 0002, section 2).
    /// </summary>
    public static string[] TypesInDomainFolders(Assembly assembly, string root) =>
        assembly.GetTypes()
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith(root + ".", StringComparison.Ordinal))
            .Where(type => !IsCompilerGenerated(type))
            .Where(type =>
            {
                var segments = type.Namespace![(root.Length + 1)..].Split('.');
                return segments.Length == 1 && segments[0] != SharedFolder;
            })
            .Select(type => type.FullName!)
            .ToArray();

    private static IEnumerable<string> NamespacesUnder(Assembly assembly, string root) =>
        assembly.GetTypes()
            .Select(type => type.Namespace)
            .OfType<string>()
            .Where(ns => ns.StartsWith(root + ".", StringComparison.Ordinal));

    private static bool IsCompilerGenerated(Type type) =>
        type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false)
        || type.Name.StartsWith('<');
}
