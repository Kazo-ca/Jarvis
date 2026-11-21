using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// <see cref="IPointerTimings"/> of the Windows host: the double-click time the user set in Windows, read once at
/// start-up (ADR 0010, section 3). Half a second by default.
/// </summary>
internal sealed class SystemPointerTimings : IPointerTimings
{
    /// <inheritdoc />
    public TimeSpan DoubleClickTime { get; } = TimeSpan.FromMilliseconds(NativeMethods.GetDoubleClickTime());
}
