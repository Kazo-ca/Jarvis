using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Adapters.Secrets;

/// <summary>
/// Secrets in an encrypted file, <c>secrets.json</c> (ADR 0004, section 4): references in clear, each value
/// encrypted on its own (DPAPI for the Windows user), atomic writes. A file that cannot be read, or that a
/// newer version wrote, is never overwritten: the operation fails and says so, without any value.
/// </summary>
/// <remarks>
/// Format: <c>{ "schemaVersion": 1, "secrets": { "&lt;reference&gt;": "&lt;base64 of the encrypted value&gt;" } }</c>.
/// </remarks>
public sealed class EncryptedFileVault : ISecretVault
{
    private const int CurrentVersion = 1;

    private readonly string file;
    private readonly ISecretProtector protector;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Creates the vault on <paramref name="file"/>; the file is created by the first secret.</summary>
    /// <param name="file">The secrets file, normally <c>%USERPROFILE%\.kazo\jarvis\secrets.json</c>.</param>
    /// <param name="protector">Encrypts the values.</param>
    public EncryptedFileVault(string file, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(protector);
        this.file = file;
        this.protector = protector;
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var secrets = Load();
            var plain = Encoding.UTF8.GetBytes(value.Reveal());
            try
            {
                secrets[reference.Value] = Convert.ToBase64String(protector.Protect(plain));
            }
            catch (CryptographicException exception)
            {
                throw new SecretVaultException($"Secret {reference} could not be encrypted.", exception);
            }
            finally
            {
                Array.Clear(plain);
            }

            Save(secrets);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!Load().TryGetValue(reference.Value, out var encoded))
            {
                return null;
            }

            byte[] plain;
            try
            {
                plain = protector.Unprotect(Convert.FromBase64String(encoded));
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException)
            {
                throw new SecretVaultException(
                    $"Secret {reference} cannot be decrypted: the file belongs to another Windows user or machine, or is damaged.");
            }

            try
            {
                return new SecretValue(Encoding.UTF8.GetString(plain));
            }
            finally
            {
                Array.Clear(plain);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var secrets = Load();
            if (!secrets.Remove(reference.Value))
            {
                return false;
            }

            Save(secrets);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return Load().Keys.Order(StringComparer.Ordinal).Select(key => new SecretRef(key)).ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    private SortedDictionary<string, string> Load()
    {
        var secrets = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(file))
        {
            return secrets;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(file), documentOptions: JarvisJson.DocumentOptions) as JsonObject;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new SecretVaultException("The secrets file cannot be read. It is left untouched.");
        }

        var version = root?["schemaVersion"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
        if (version > CurrentVersion)
        {
            throw new SecretVaultException($"The secrets file was written by a newer version (schema {version}). It is left untouched.");
        }

        if (version < 1 || root!["secrets"] is not JsonObject entries)
        {
            throw new SecretVaultException("The secrets file cannot be read. It is left untouched.");
        }

        foreach (var (key, node) in entries)
        {
            if (SecretRef.IsValid(key) && node is JsonValue encoded && encoded.TryGetValue<string>(out var text))
            {
                secrets[key] = text;
            }
        }

        return secrets;
    }

    private void Save(SortedDictionary<string, string> secrets)
    {
        var entries = new JsonObject();
        foreach (var (key, encoded) in secrets)
        {
            entries[key] = encoded;
        }

        var root = new JsonObject { ["schemaVersion"] = CurrentVersion, ["secrets"] = entries };
        AtomicFile.WriteAllText(file, root.ToJsonString(JarvisJson.Options));
    }
}
