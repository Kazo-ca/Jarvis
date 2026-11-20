namespace Kazo.Jarvis.Features.Setup.FirstRun;

/// <summary>
/// The step shown by the first-run wizard. Kept outside the component because a language change builds the window
/// again (ADR 0008, section 1): the user stays on the step where the language was changed.
/// </summary>
public sealed class FirstRunWizardState
{
    /// <summary>Index of the step shown, from 0; the number of steps is the closing screen.</summary>
    public int Step { get; set; }
}
