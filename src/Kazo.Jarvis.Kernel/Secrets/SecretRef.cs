using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>
/// Reference to a secret (ADR 0004, section 4): what the settings keep instead of the secret itself.
/// 1 to 100 letters, digits, <c>.</c>, <c>-</c> or <c>_</c>.
/// </summary>
public readonly partial record struct SecretRef
{
    /// <summary>Maximum length of a reference.</summary>
    public const int MaximumLength = 100;

    /// <summary>Creates a reference.</summary>
    /// <param name="value">The reference, for instance <c>llm.anthropic.apikey</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not valid.</exception>
    public SecretRef(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                $"A secret reference has 1 to {MaximumLength} letters, digits, '.', '-' or '_'.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The reference.</summary>
    public string Value { get; }

    /// <summary>Whether <paramref name="value"/> is a valid reference.</summary>
    /// <param name="value">The candidate.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>Reads a reference without throwing.</summary>
    /// <param name="value">The candidate.</param>
    /// <param name="reference">The reference when valid.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool TryParse(string? value, out SecretRef reference)
    {
        reference = IsValid(value) ? new SecretRef(value) : default;
        return reference.Value is not null;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex Pattern();
}
