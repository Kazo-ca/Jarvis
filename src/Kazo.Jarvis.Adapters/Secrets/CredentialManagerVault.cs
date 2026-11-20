using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Adapters.Secrets;

/// <summary>
/// Secrets in the Windows Credential Manager (ADR 0004, section 4), the default vault: one generic
/// credential per secret, named <c>Kazo.Jarvis/&lt;reference&gt;</c>, kept on this machine for this Windows user.
/// A value is limited to 2,560 bytes in UTF-8 (the Credential Manager's limit).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialManagerVault : ISecretVault
{
    /// <summary>Maximum size of a value, in bytes (<c>CRED_MAX_CREDENTIAL_BLOB_SIZE</c>).</summary>
    public const int MaximumValueBytes = 5 * 512;

    private const int GenericType = 1;
    private const int PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    private readonly string prefix;

    /// <summary>Creates the vault with the product's prefix, <c>Kazo.Jarvis/</c>.</summary>
    public CredentialManagerVault()
        : this("Kazo.Jarvis/")
    {
    }

    /// <summary>Same, with another prefix: the tests use their own, then delete it.</summary>
    internal CredentialManagerVault(string prefix) => this.prefix = prefix;

    /// <inheritdoc />
    public ValueTask SetAsync(SecretRef reference, SecretValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        var blob = Encoding.UTF8.GetBytes(value.Reveal());
        if (blob.Length > MaximumValueBytes)
        {
            Array.Clear(blob);
            throw new SecretVaultException(
                $"Secret {reference} is longer than the {MaximumValueBytes} bytes the Windows Credential Manager accepts. Use the encrypted file vault.");
        }

        var target = Marshal.StringToCoTaskMemUni(prefix + reference.Value);
        var userName = Marshal.StringToCoTaskMemUni("Kazo.Jarvis");
        var buffer = Marshal.AllocCoTaskMem(Math.Max(blob.Length, 1));
        try
        {
            Marshal.Copy(blob, 0, buffer, blob.Length);
            var credential = new Credential
            {
                Type = GenericType,
                TargetName = target,
                UserName = userName,
                CredentialBlobSize = blob.Length,
                CredentialBlob = buffer,
                Persist = PersistLocalMachine,
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw Failure("store", reference);
            }
        }
        finally
        {
            // The value does not stay in memory longer than needed.
            Array.Clear(blob);
            Marshal.Copy(new byte[blob.Length], 0, buffer, blob.Length);
            Marshal.FreeCoTaskMem(buffer);
            Marshal.FreeCoTaskMem(userName);
            Marshal.FreeCoTaskMem(target);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<SecretValue?> GetAsync(SecretRef reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!NativeMethods.CredRead(prefix + reference.Value, GenericType, 0, out var pointer))
        {
            if (Marshal.GetLastPInvokeError() == ErrorNotFound)
            {
                return ValueTask.FromResult<SecretValue?>(null);
            }

            throw Failure("read", reference);
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            var text = Encoding.UTF8.GetString(blob);
            Array.Clear(blob);
            return ValueTask.FromResult<SecretValue?>(text.Length == 0 ? null : new SecretValue(text));
        }
        finally
        {
            NativeMethods.CredFree(pointer);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(SecretRef reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (NativeMethods.CredDelete(prefix + reference.Value, GenericType, 0))
        {
            return ValueTask.FromResult(true);
        }

        return Marshal.GetLastPInvokeError() == ErrorNotFound
            ? ValueTask.FromResult(false)
            : throw Failure("delete", reference);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SecretRef>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!NativeMethods.CredEnumerate(prefix + "*", 0, out var count, out var list))
        {
            return Marshal.GetLastPInvokeError() == ErrorNotFound
                ? ValueTask.FromResult<IReadOnlyList<SecretRef>>([])
                : throw new SecretVaultException("The Windows Credential Manager cannot be listed.", new Win32Exception());
        }

        try
        {
            var references = new List<SecretRef>(count);
            for (var index = 0; index < count; index++)
            {
                var credential = Marshal.PtrToStructure<Credential>(Marshal.ReadIntPtr(list, index * IntPtr.Size));
                var name = Marshal.PtrToStringUni(credential.TargetName) ?? string.Empty;

                // Other entries that happen to share the prefix are ignored rather than failing the list.
                if (credential.Type == GenericType
                    && name.StartsWith(prefix, StringComparison.Ordinal)
                    && SecretRef.TryParse(name[prefix.Length..], out var reference))
                {
                    references.Add(reference);
                }
            }

            references.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
            return ValueTask.FromResult<IReadOnlyList<SecretRef>>(references);
        }
        finally
        {
            NativeMethods.CredFree(list);
        }
    }

    private static SecretVaultException Failure(string operation, SecretRef reference) =>
        new($"The Windows Credential Manager could not {operation} secret {reference}.", new Win32Exception());

    /// <summary><c>CREDENTIALW</c>. Strings are passed as pointers so the structure has a single layout both ways.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    private static class NativeMethods
    {
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredWrite(ref Credential credential, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredEnumerate(string filter, int flags, out int count, out IntPtr credentials);

        [DllImport("advapi32.dll", SetLastError = false)]
        public static extern void CredFree(IntPtr buffer);
    }
}
