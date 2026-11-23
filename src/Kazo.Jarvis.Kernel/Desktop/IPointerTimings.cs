namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// The double-click delay of the system (ADR 0027, section 3): the orb waits that long before it treats a single
/// click, so that a double click is never also a single click. Given by the host: Windows reads its own setting
/// (half a second by default), the Web host gives a short one.
/// </summary>
public interface IPointerTimings
{
    /// <summary>The longest time between the two clicks of a double click. Zero: no double click, a click acts at once.</summary>
    TimeSpan DoubleClickTime { get; }
}
