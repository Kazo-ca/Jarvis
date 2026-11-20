using Kazo.Jarvis.Kernel.Models;

namespace Kazo.Jarvis.Features.Setup.Models;

/// <summary>Where a model stands.</summary>
public enum ModelState
{
    /// <summary>Not in the models folder.</summary>
    Missing,

    /// <summary>Waiting for its turn in the downloads.</summary>
    Queued,

    /// <summary>Being downloaded.</summary>
    Downloading,

    /// <summary>In place, checked.</summary>
    Installed,

    /// <summary>The last download failed; see <see cref="ModelStatus.Failure"/>.</summary>
    Failed,
}

/// <summary>The state of one line of the models page.</summary>
/// <param name="State">Where it stands.</param>
/// <param name="Received">Bytes received for it, while it downloads.</param>
/// <param name="Failure">The cause of the last failure.</param>
public sealed record ModelStatus(ModelState State, long Received = 0, ModelDownloadFailure? Failure = null);
