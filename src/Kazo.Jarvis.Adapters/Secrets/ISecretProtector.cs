namespace Kazo.Jarvis.Adapters.Secrets;

/// <summary>
/// Encrypts the values of the secrets file. DPAPI on Windows (<see cref="DpapiSecretProtector"/>); the tests
/// give a simulated one so the file format is tested on Linux too.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts <paramref name="plain"/>.</summary>
    /// <param name="plain">The bytes to protect.</param>
    /// <returns>The encrypted bytes.</returns>
    byte[] Protect(byte[] plain);

    /// <summary>Decrypts what <see cref="Protect"/> gave.</summary>
    /// <param name="encrypted">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">They cannot be decrypted (other user or machine, damaged file).</exception>
    byte[] Unprotect(byte[] encrypted);
}
