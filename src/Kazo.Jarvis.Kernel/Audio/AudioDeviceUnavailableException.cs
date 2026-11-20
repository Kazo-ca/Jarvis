namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>An audio device is missing or cannot be opened.</summary>
public sealed class AudioDeviceUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public AudioDeviceUnavailableException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed; never a secret.</param>
    public AudioDeviceUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed; never a secret.</param>
    /// <param name="innerException">The error of the audio layer.</param>
    public AudioDeviceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
