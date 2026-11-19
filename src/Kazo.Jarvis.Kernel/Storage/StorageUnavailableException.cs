namespace Kazo.Jarvis.Kernel.Storage;

/// <summary>
/// The document store cannot be used: the database was written by a newer version, or its migration
/// failed. The file is left untouched.
/// </summary>
public sealed class StorageUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public StorageUnavailableException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, without any document content.</param>
    public StorageUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, without any document content.</param>
    /// <param name="innerException">The cause.</param>
    public StorageUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
