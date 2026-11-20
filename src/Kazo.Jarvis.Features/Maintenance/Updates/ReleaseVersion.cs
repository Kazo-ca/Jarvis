namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>
/// A release version such as <c>v0.2.0</c>, <c>0.2.0-beta.1</c> or <c>0.2.0+abc123</c> (build metadata
/// ignored). A stable version is newer than any pre-release of the same numbers. Copied from Kazo Bar
/// (ADR 0014); it evolves here on its own.
/// </summary>
/// <param name="Numbers">Major, minor and patch numbers.</param>
/// <param name="PreRelease">The pre-release label (<c>beta.1</c>), or <see langword="null"/> for a stable version.</param>
public sealed record ReleaseVersion(Version Numbers, string? PreRelease) : IComparable<ReleaseVersion>
{
    /// <summary>Whether this is a pre-release.</summary>
    public bool IsPreRelease => PreRelease is not null;

    /// <summary>Reads a version or a tag.</summary>
    /// <param name="text">The text, with or without a leading <c>v</c>.</param>
    /// <param name="version">The version read.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a version.</returns>
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var plus = value.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string? preRelease = null;
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            preRelease = value[(dash + 1)..];
            value = value[..dash];
            if (preRelease.Length == 0)
            {
                return false;
            }
        }

        if (!Version.TryParse(value.Contains('.', StringComparison.Ordinal) ? value : value + ".0", out var numbers))
        {
            return false;
        }

        version = new ReleaseVersion(Normalize(numbers), preRelease);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byNumbers = Numbers.CompareTo(other.Numbers);
        if (byNumbers != 0)
        {
            return byNumbers;
        }

        if (PreRelease == other.PreRelease)
        {
            return 0;
        }

        if (PreRelease is null)
        {
            return 1;
        }

        return other.PreRelease is null ? -1 : ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>Whether <paramref name="left"/> is newer than <paramref name="right"/>.</summary>
    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is older than <paramref name="right"/>.</summary>
    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    /// <inheritdoc />
    public override string ToString() => PreRelease is null ? Numbers.ToString() : $"{Numbers}-{PreRelease}";

    /// <summary>1.2 becomes 1.2.0 and 1.2.3.0 becomes 1.2.3, so assembly versions compare equal to tags.</summary>
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    /// <summary>SemVer rule: dot-separated identifiers, numeric ones compared as numbers.</summary>
    private static int ComparePreRelease(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aNumeric = int.TryParse(a[i], out var an);
            var bNumeric = int.TryParse(b[i], out var bn);
            var result = (aNumeric, bNumeric) switch
            {
                (true, true) => an.CompareTo(bn),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };
            if (result != 0)
            {
                return result;
            }
        }

        return a.Length.CompareTo(b.Length);
    }
}
