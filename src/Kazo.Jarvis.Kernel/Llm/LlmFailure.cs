namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>Why a provider did not answer (ADR 0007, section 8). Each cause has its own corrective action.</summary>
public enum LlmFailure
{
    /// <summary>No default provider is configured: add one in the settings.</summary>
    NotConfigured,

    /// <summary>The address is not a valid <c>http</c> or <c>https</c> address.</summary>
    InvalidAddress,

    /// <summary>Nothing answers at the address: check the address and the port, and that the server is started.</summary>
    Unreachable,

    /// <summary>The server answers but does not have the model: download it, or pick one it lists.</summary>
    ModelMissing,

    /// <summary>A cloud provider has no key in the vault.</summary>
    KeyMissing,

    /// <summary>The server refused the key (HTTP 401 or 403).</summary>
    KeyRejected,

    /// <summary>The server took too long to answer.</summary>
    TimedOut,

    /// <summary>The server answered with an error that names none of the causes above (rate limit, server fault...).</summary>
    ServerError,
}
