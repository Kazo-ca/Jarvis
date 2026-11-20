namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>A vault refused an operation. Its message names the reference, never the value.</summary>
public sealed class SecretVaultException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SecretVaultException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed, without the value.</param>
    public SecretVaultException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed, without the value.</param>
    /// <param name="innerException">The cause; it must not hold the value either.</param>
    public SecretVaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
