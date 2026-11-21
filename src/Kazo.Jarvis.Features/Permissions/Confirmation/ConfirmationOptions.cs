using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Permissions.Confirmation;

/// <summary>Settings of the confirmation requests (ADR 0023, section 4), section <c>Permissions.Confirmation</c> of <c>config.json</c>.</summary>
public sealed record ConfirmationOptions
{
    /// <summary>Shortest timeout of a request, in seconds.</summary>
    public const int MinimumTimeout = 10;

    /// <summary>Longest timeout of a request, in seconds.</summary>
    public const int MaximumTimeout = 120;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<ConfirmationOptions> Section { get; } = new(
        "Permissions.Confirmation",
        () => new ConfirmationOptions(),
        value => value.TimeoutSeconds is < MinimumTimeout or > MaximumTimeout
            ? [$"The timeout of a confirmation request must be from {MinimumTimeout} to {MaximumTimeout} s, not {value.TimeoutSeconds}."]
            : []);

    /// <summary>How long a request waits for an answer before it is denied, in seconds: 30 by default.</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
