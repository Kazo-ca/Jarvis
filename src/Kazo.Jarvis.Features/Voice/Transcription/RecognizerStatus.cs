using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>Where the loading of the Whisper model stands.</summary>
public enum RecognizerState
{
    /// <summary>Not loaded yet.</summary>
    NotLoaded,

    /// <summary>Being loaded.</summary>
    Loading,

    /// <summary>In memory, ready to transcribe.</summary>
    Ready,

    /// <summary>The model file is not in the models folder.</summary>
    Missing,

    /// <summary>The file is there but could not be loaded.</summary>
    Failed,
}

/// <summary>What the settings page shows about the Whisper model.</summary>
/// <param name="State">Where the loading stands.</param>
/// <param name="Spec">The model and device asked for.</param>
/// <param name="Device">Where it actually runs, once ready.</param>
/// <param name="ModelPath">The file expected or loaded, when known.</param>
/// <param name="Detail">Technical detail of a failure, for the log and the page.</param>
public sealed record RecognizerStatus(RecognizerState State, RecognizerSpec Spec, ComputeDevice? Device = null, string? ModelPath = null, string? Detail = null);
