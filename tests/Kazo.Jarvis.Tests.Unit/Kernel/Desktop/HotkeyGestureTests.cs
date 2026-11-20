using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Desktop;

/// <summary>The hotkey as written in the settings (US1.6).</summary>
[TestFixture]
public sealed class HotkeyGestureTests
{
    [TestCase("Ctrl+Alt+J", HotkeyModifiers.Control | HotkeyModifiers.Alt, "J", "Ctrl+Alt+J")]
    [TestCase(" control + alt + j ", HotkeyModifiers.Control | HotkeyModifiers.Alt, "J", "Ctrl+Alt+J")]
    [TestCase("Win+Shift+space", HotkeyModifiers.Windows | HotkeyModifiers.Shift, "Space", "Shift+Win+Space")]
    [TestCase("Alt+f12", HotkeyModifiers.Alt, "F12", "Alt+F12")]
    [TestCase("Ctrl+5", HotkeyModifiers.Control, "5", "Ctrl+5")]
    [TestCase("Ctrl+pagedown", HotkeyModifiers.Control, "PageDown", "Ctrl+PageDown")]
    public void A_valid_gesture_is_read_and_written_back_in_canonical_form(string text, HotkeyModifiers modifiers, string key, string canonical)
    {
        var parsed = HotkeyGesture.TryParse(text, out var gesture);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(gesture, Is.EqualTo(new HotkeyGesture(modifiers, key)));
            Assert.That(gesture!.ToString(), Is.EqualTo(canonical));
            Assert.That(HotkeyGesture.Parse(canonical), Is.EqualTo(gesture));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("J")]
    [TestCase("Shift+J")]
    [TestCase("Ctrl+")]
    [TestCase("Ctrl++J")]
    [TestCase("Ctrl+Ctrl+J")]
    [TestCase("Ctrl+Alt")]
    [TestCase("Ctrl+F25")]
    [TestCase("Ctrl+F0")]
    [TestCase("Ctrl+F01")]
    [TestCase("Ctrl+Escape")]
    [TestCase("Hyper+J")]
    [TestCase("Ctrl+é")]
    public void An_invalid_gesture_is_refused(string? text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(HotkeyGesture.TryParse(text, out var gesture), Is.False);
            Assert.That(gesture, Is.Null);
            Assert.That(() => HotkeyGesture.Parse(text!), Throws.InstanceOf<Exception>());
        });
    }
}
