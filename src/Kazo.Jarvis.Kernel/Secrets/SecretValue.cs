namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>
/// The value of a secret. Its <see cref="ToString"/> never shows it, so that a secret written by mistake in a
/// log or an error message stays hidden (ADR 0004, section 4). Only <see cref="Reveal"/> gives it.
/// </summary>
public sealed class SecretValue : IEquatable<SecretValue>
{
    private const string Hidden = "(secret)";
    private readonly string value;

    /// <summary>Wraps a secret.</summary>
    /// <param name="value">The secret; not empty.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public SecretValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            throw new ArgumentException("A secret cannot be empty: delete it instead.", nameof(value));
        }

        this.value = value;
    }

    /// <summary>The secret itself, to give to the service that needs it. Never to log or display.</summary>
    /// <returns>The secret.</returns>
    public string Reveal() => value;

    /// <inheritdoc />
    public override string ToString() => Hidden;

    /// <inheritdoc />
    public bool Equals(SecretValue? other) => other is not null && string.Equals(value, other.value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SecretValue);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(value);
}
