using System;

namespace PasswordManager.Services.Encryption;

/// <summary>
/// Container holding encrypted data along with cryptographic metadata required for authenticated decryption.
/// </summary>
public class EncryptedPayload
{
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public byte[] Tag { get; set; } = Array.Empty<byte>();
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Core contract for cryptographic operations including PBKDF2 key derivation and AES-256-GCM authenticated encryption/decryption.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Derives a 256-bit (32-byte) cryptographic key from a master password using PBKDF2 (HMAC-SHA256).
    /// </summary>
    /// <param name="password">The master password string.</param>
    /// <param name="salt">Cryptographically random salt bytes.</param>
    /// <param name="iterations">Number of PBKDF2 iterations (default 100,000).</param>
    /// <returns>A 32-byte derived key array.</returns>
    byte[] DeriveKey(string password, byte[] salt, int iterations = 100000);

    /// <summary>
    /// Encrypts plaintext bytes using AES-256-GCM authenticated encryption.
    /// Generates a unique 12-byte nonce and 16-byte authentication tag per call.
    /// </summary>
    /// <param name="plaintext">The unencrypted payload bytes.</param>
    /// <param name="key">The 32-byte derived encryption key.</param>
    /// <param name="salt">The salt associated with key derivation.</param>
    /// <returns>An EncryptedPayload structure containing salt, nonce, authentication tag, and ciphertext.</returns>
    EncryptedPayload Encrypt(byte[] plaintext, byte[] key, byte[] salt);

    /// <summary>
    /// Decrypts AES-256-GCM ciphertext using the derived key and validates the authentication tag.
    /// </summary>
    /// <param name="payload">The encrypted payload containing ciphertext, nonce, and auth tag.</param>
    /// <param name="key">The 32-byte derived encryption key.</param>
    /// <returns>The decrypted original plaintext bytes.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown if decryption or authentication tag validation fails (tampering/wrong key).</exception>
    byte[] Decrypt(EncryptedPayload payload, byte[] key);
}
