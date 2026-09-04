using System;
using System.IO;
using System.Text;
using PasswordManager.Services.Encryption;

namespace PasswordManager.Services.Vault;

/// <summary>
/// Implements IVaultStorage using binary FileStream I/O with header magic identification,
/// vault versioning, and length-prefixed payload components.
/// </summary>
public class FileVaultStorage : IVaultStorage
{
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("SPMV"); // Secure Password Manager Vault
    public const int CurrentVersion = 1;

    private readonly string _filePath;

    public FileVaultStorage(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault.dat");
    }

    public bool VaultExists()
    {
        return File.Exists(_filePath);
    }

    public EncryptedPayload ReadVault()
    {
        if (!VaultExists())
        {
            throw new FileNotFoundException("Vault file does not exist.", _filePath);
        }

        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        // Validate Magic Header
        byte[] magic = reader.ReadBytes(4);
        if (magic.Length < 4 || !EqualBytes(magic, MagicBytes))
        {
            throw new InvalidDataException("Invalid vault file format or corrupted header.");
        }

        // Validate Version
        int version = reader.ReadInt32();
        if (version > CurrentVersion || version <= 0)
        {
            throw new InvalidDataException($"Unsupported vault format version: {version}");
        }

        // Read Salt
        int saltLen = reader.ReadInt32();
        byte[] salt = reader.ReadBytes(saltLen);
        if (salt.Length != saltLen) throw new InvalidDataException("Corrupted vault file: truncated salt.");

        // Read Nonce
        int nonceLen = reader.ReadInt32();
        byte[] nonce = reader.ReadBytes(nonceLen);
        if (nonce.Length != nonceLen) throw new InvalidDataException("Corrupted vault file: truncated nonce.");

        // Read Tag
        int tagLen = reader.ReadInt32();
        byte[] tag = reader.ReadBytes(tagLen);
        if (tag.Length != tagLen) throw new InvalidDataException("Corrupted vault file: truncated tag.");

        // Read Ciphertext
        int ciphertextLen = reader.ReadInt32();
        byte[] ciphertext = reader.ReadBytes(ciphertextLen);
        if (ciphertext.Length != ciphertextLen) throw new InvalidDataException("Corrupted vault file: truncated ciphertext.");

        return new EncryptedPayload
        {
            Salt = salt,
            Nonce = nonce,
            Tag = tag,
            Ciphertext = ciphertext
        };
    }

    public void WriteVault(EncryptedPayload payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        // Header: Magic + Version
        writer.Write(MagicBytes);
        writer.Write(CurrentVersion);

        // Salt
        writer.Write(payload.Salt.Length);
        writer.Write(payload.Salt);

        // Nonce
        writer.Write(payload.Nonce.Length);
        writer.Write(payload.Nonce);

        // Tag
        writer.Write(payload.Tag.Length);
        writer.Write(payload.Tag);

        // Ciphertext
        writer.Write(payload.Ciphertext.Length);
        writer.Write(payload.Ciphertext);
    }

    public void DeleteVault()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private static bool EqualBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}
