using System;
using System.Security.Cryptography;

namespace PasswordManager.Services.Encryption;

/// <summary>
/// Implements IEncryptionService using PBKDF2 (HMAC-SHA256) for key derivation
/// and AES-256-GCM for authenticated encryption and decryption.
/// </summary>
public class AesGcmEncryptionService : IEncryptionService
{
    public const int KeySizeBytes = 32;      // 256-bit key
    public const int NonceSizeBytes = 12;    // 96-bit nonce (standard for AES-GCM)
    public const int TagSizeBytes = 16;      // 128-bit authentication tag
    public const int DefaultIterations = 100000;

    public byte[] DeriveKey(string password, byte[] salt, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        if (salt == null || salt.Length == 0) throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));

        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    public EncryptedPayload Encrypt(byte[] plaintext, byte[] key, byte[] salt)
    {
        if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
        if (key == null || key.Length != KeySizeBytes) throw new ArgumentException($"Key must be exactly {KeySizeBytes} bytes.", nameof(key));
        if (salt == null) throw new ArgumentNullException(nameof(salt));

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] tag = new byte[TagSizeBytes];
        byte[] ciphertext = new byte[plaintext.Length];

        using (var aesGcm = new AesGcm(key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        return new EncryptedPayload
        {
            Salt = salt,
            Nonce = nonce,
            Tag = tag,
            Ciphertext = ciphertext
        };
    }

    public byte[] Decrypt(EncryptedPayload payload, byte[] key)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (key == null || key.Length != KeySizeBytes) throw new ArgumentException($"Key must be exactly {KeySizeBytes} bytes.", nameof(key));
        if (payload.Nonce == null || payload.Nonce.Length != NonceSizeBytes) throw new ArgumentException($"Invalid nonce length. Expected {NonceSizeBytes} bytes.", nameof(payload));
        if (payload.Tag == null || payload.Tag.Length != TagSizeBytes) throw new ArgumentException($"Invalid authentication tag length. Expected {TagSizeBytes} bytes.", nameof(payload));
        if (payload.Ciphertext == null) throw new ArgumentNullException(nameof(payload.Ciphertext));

        byte[] plaintext = new byte[payload.Ciphertext.Length];

        using (var aesGcm = new AesGcm(key, TagSizeBytes))
        {
            aesGcm.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext);
        }

        return plaintext;
    }
}
