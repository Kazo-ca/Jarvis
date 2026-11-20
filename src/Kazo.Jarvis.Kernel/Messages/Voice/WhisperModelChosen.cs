using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Kernel.Messages.Voice;

/// <summary>
/// The user picked the Whisper model to download in the models page or the first-run wizard (stage 1 plan,
/// work item 14), once it is in the models folder. The transcription slice, which owns that setting, switches to it.
/// </summary>
/// <param name="Model">The model now installed.</param>
/// <param name="Device">Where it should run: the graphics card when an NVIDIA card was found, the processor otherwise.</param>
public sealed record WhisperModelChosen(WhisperModel Model, ComputeDevice Device) : IEvent;
