namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// Start with Windows (ADR 0003, section 5; design assumption no. 3): whether Kazo.Jarvis starts when the
/// user signs in. The Web host keeps the value in memory.
/// </summary>
public interface ILaunchAtLogin
{
    /// <summary>Whether the start at sign-in is registered.</summary>
    bool IsEnabled { get; }

    /// <summary>Registers or removes the start at sign-in.</summary>
    /// <param name="enabled">The wanted state.</param>
    /// <returns><see langword="true"/> when the wanted state now holds; <see langword="false"/> when the host declined (a development build, for instance).</returns>
    bool SetEnabled(bool enabled);
}
