// Samples for SliceIsolationTests: a fake Features tree inside the test assembly.
// Alpha.One breaks the rule twice; Alpha.OneMore (name prefix of Alpha.One) and Shared are allowed.
#pragma warning disable CA1822, IDE0051

namespace Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Shared
{
    /// <summary>Shared presentation helper: every slice may use it.</summary>
    internal sealed class SecretField;
}

namespace Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Beta.Two
{
    /// <summary>A type of slice Beta.Two.</summary>
    internal sealed class BetaThing
    {
        public static int Count() => 2;
    }
}

namespace Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Beta
{
    /// <summary>Wrong: code shared by a domain's slices.</summary>
    internal static class DomainLevelHelper;
}

namespace Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Alpha.OneMore
{
    /// <summary>A slice whose name starts like Alpha.One: must not be mistaken for it.</summary>
    internal sealed class OneMoreThing;

    /// <summary>Allowed: uses its own slice, whose name starts with "Alpha.One".</summary>
    internal sealed class UsesOwnSlice
    {
        public OneMoreThing Create() => new();
    }
}

namespace Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Alpha.One
{
    using Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Beta.Two;
    using Kazo.Jarvis.Tests.Unit.Architecture.Samples.Features.Shared;

    /// <summary>Wrong: a field typed with another slice's type.</summary>
    internal sealed class UsesBetaInSignature
    {
        private readonly BetaThing? _thing = null;

        public BetaThing? Thing => _thing;
    }

    /// <summary>Wrong: a call to another slice hidden in a method body.</summary>
    internal sealed class UsesBetaInBody
    {
        public int Run() => BetaThing.Count();
    }

    /// <summary>Allowed: uses only Shared.</summary>
    internal sealed class UsesShared
    {
        public SecretField Field { get; } = new();
    }
}
