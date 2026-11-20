namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>
/// Thrown by the chat clients of the adapters when a provider does not answer, with the cause (ADR 0007,
/// section 8), so that the conversation names it and its corrective action (US1.17).
/// </summary>
public sealed class LlmException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="failure">The cause.</param>
    /// <param name="message">Technical detail for the log, in English. Never holds a key.</param>
    /// <param name="innerException">The original error, if any.</param>
    public LlmException(LlmFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>The cause.</summary>
    public LlmFailure Failure { get; }
}
