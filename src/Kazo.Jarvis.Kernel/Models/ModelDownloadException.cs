namespace Kazo.Jarvis.Kernel.Models;

/// <summary>Why a model could not be put in place.</summary>
public enum ModelDownloadFailure
{
    /// <summary>The server could not be reached, or refused the file.</summary>
    Unreachable,

    /// <summary>The file arrived, but its size or checksum is not the published one. It was deleted.</summary>
    ChecksumMismatch,

    /// <summary>The disk is full, or the models folder cannot be written.</summary>
    DiskError,

    /// <summary>Any other cause, for instance an archive that cannot be unpacked.</summary>
    Failed,
}

/// <summary>A model download failed (ADR 0006, section 7).</summary>
public sealed class ModelDownloadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ModelDownloadException()
        : this(ModelDownloadFailure.Failed, "The model download failed.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened, in English, for the log.</param>
    public ModelDownloadException(string message)
        : this(ModelDownloadFailure.Failed, message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened, in English, for the log.</param>
    /// <param name="innerException">The cause.</param>
    public ModelDownloadException(string message, Exception innerException)
        : this(ModelDownloadFailure.Failed, message, innerException)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="failure">The cause, shown to the user.</param>
    /// <param name="message">What happened, in English, for the log.</param>
    /// <param name="innerException">The cause, if any.</param>
    public ModelDownloadException(ModelDownloadFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>The cause, shown to the user.</summary>
    public ModelDownloadFailure Failure { get; }
}
