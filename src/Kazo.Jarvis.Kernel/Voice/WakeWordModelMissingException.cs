namespace Kazo.Jarvis.Kernel.Voice;

/// <summary>A wake word model file is missing from the models folder (ADR 0004, section 5).</summary>
public sealed class WakeWordModelMissingException : Exception
{
    /// <summary>Creates the exception.</summary>
    public WakeWordModelMissingException()
        : this([])
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is missing.</param>
    public WakeWordModelMissingException(string message)
        : base(message)
    {
        MissingFiles = [];
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is missing.</param>
    /// <param name="innerException">The error of the file system.</param>
    public WakeWordModelMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
        MissingFiles = [];
    }

    /// <summary>Creates the exception for the files not found.</summary>
    /// <param name="missingFiles">Full paths of the missing files.</param>
    public WakeWordModelMissingException(IReadOnlyList<string> missingFiles)
        : base("Wake word model missing: " + string.Join(", ", missingFiles ?? []))
    {
        MissingFiles = missingFiles ?? [];
    }

    /// <summary>Full paths of the missing files.</summary>
    public IReadOnlyList<string> MissingFiles { get; }
}
