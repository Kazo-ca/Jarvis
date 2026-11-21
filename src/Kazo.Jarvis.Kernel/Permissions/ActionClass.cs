namespace Kazo.Jarvis.Kernel.Permissions;

/// <summary>
/// The class of an action a model asks for (ADR 0009, section 2), from the least to the most restricted. The
/// order matters: the decision point keeps the highest of the classes it is given.
/// </summary>
public enum ActionClass
{
    /// <summary>Reads without any effect: the time, a calculation, a file of an allowed folder. No agreement asked.</summary>
    Read = 0,

    /// <summary>An everyday effect: a light, the volume. No agreement asked.</summary>
    EverydayEffect = 1,

    /// <summary>A write, a command, a generated script: a confirmation, by voice or by a click.</summary>
    Sensitive = 2,

    /// <summary>Security home automation: a confirmation by a strong channel, the click on the PC. Voice is not enough.</summary>
    Critical = 3,

    /// <summary>Never done: denied without a question, and logged.</summary>
    Forbidden = 4,
}
