namespace Kazo.Jarvis.Features.Shared;

/// <summary>
/// Marker of the texts shared by three slices or more (ADR 0008, section 2): <c>SharedTexts.resx</c> and
/// <c>SharedTexts.fr.resx</c>. Inject <c>IStringLocalizer&lt;SharedTexts&gt;</c>.
/// </summary>
public sealed class SharedTexts
{
    private SharedTexts()
    {
    }
}
