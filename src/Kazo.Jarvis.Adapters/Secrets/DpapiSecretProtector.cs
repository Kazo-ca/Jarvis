using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Kazo.Jarvis.Adapters.Secrets;

/// <summary>
/// DPAPI for the current Windows user (ADR 0004, section 4): only this user, on this machine, can decrypt.
/// A fixed entropy keeps another program of the same user from decrypting the file by mistake.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kazo.Jarvis.secrets.v1");

    /// <inheritdoc />
    public byte[] Protect(byte[] plain) => ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

    /// <inheritdoc />
    public byte[] Unprotect(byte[] encrypted) => ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
}
