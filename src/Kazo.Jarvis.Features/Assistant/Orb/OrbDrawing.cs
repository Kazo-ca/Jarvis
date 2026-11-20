using Kazo.Jarvis.Kernel.Assistant;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// Base of the two drawings of the orb (<see cref="OrbLight"/>, <see cref="OrbHighQuality"/>): loads the
/// drawing's script, hands it the motion decided in C# (<see cref="OrbMotion"/>) and the sound level, and
/// detaches it. The script only draws (ADR 0005, section 5).
/// </summary>
public abstract class OrbDrawing : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? module;
    private (OrbMotion Motion, bool Reduced, AssistantLook Look)? applied;
    private bool disposed;

    /// <summary>The root element handed to the script.</summary>
    protected ElementReference Root;

    /// <summary>The look shown; the colors and light come from the style sheet of the <see cref="Orb"/>.</summary>
    [Parameter]
    public AssistantLook Look { get; set; }

    /// <summary>The motion of the look (<see cref="OrbMotion.For(AssistantLook, OrbQuality, bool, bool)"/>).</summary>
    [Parameter]
    public OrbMotion Motion { get; set; }

    /// <summary>The "reduced motion" option. The system setting of the same name applies as well.</summary>
    [Parameter]
    public bool ReducedMotion { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>Path of the drawing's script module.</summary>
    protected abstract string ModulePath { get; }

    /// <summary>
    /// Whether a change of look alone, with the same motion, must reach the script. The light version fades
    /// its colors in CSS and needs no call; the canvas of the high-quality version must redraw.
    /// </summary>
    protected virtual bool RedrawsOnLook => false;

    /// <summary>Passes the sound level (0 to 1) to the drawing: it raises the light, never the size.</summary>
    /// <param name="level">The level, from 0 to 1.</param>
    /// <returns>A task that completes once the script received it.</returns>
    public async ValueTask SetLevelAsync(float level)
    {
        if (module is not null && !disposed)
        {
            try
            {
                await module.InvokeVoidAsync("setLevel", Root, Math.Clamp(level, 0f, 1f));
            }
            catch (JSDisconnectedException)
            {
                // The page went away: nothing left to draw.
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            var wanted = (Motion, ReducedMotion, RedrawsOnLook ? Look : AssistantLook.Idle);
            if (firstRender)
            {
                module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
                applied = wanted;
                await module.InvokeVoidAsync("attach", Root, Motion.Rate, Motion.Pulses, ReducedMotion);
            }
            else if (module is not null && applied != wanted)
            {
                // Only on a change: a render for the badge or the progress does not touch the drawing.
                applied = wanted;
                await module.InvokeVoidAsync("update", Root, Motion.Rate, Motion.Pulses, ReducedMotion);
            }
        }
        catch (JSDisconnectedException)
        {
            // Prerendering ended or the circuit closed.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        GC.SuppressFinalize(this);
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("detach", Root);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone, and the script with it.
        }
    }
}
