using System;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Services.Authentication;

/// <summary>
/// Handles master password setup, verification using PBKDF2 (HMAC-SHA256), and vault session lock state.
/// Master passwords are never stored in plaintext.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 100000;
    private const int MinPasswordLength = 8;

    private byte[]? _salt;
    private byte[]? _masterPasswordHash;
    private bool _isUnlocked;

    public bool IsVaultInitialized => _masterPasswordHash != null && _salt != null;

    public bool IsUnlocked => _isUnlocked;

    public event Action? LockStateChanged;

    public bool InitializeMasterPassword(string password, string confirmPassword, out string? errorMessage)
    {
        if (IsVaultInitialized)
        {
            errorMessage = "Master password has already been configured.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Master password cannot be empty.";
            return false;
        }

        if (password.Length < MinPasswordLength)
        {
            errorMessage = $"Master password must be at least {MinPasswordLength} characters long.";
            return false;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            errorMessage = "Passwords do not match.";
            return false;
        }

        // Generate cryptographically random salt
        _salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

        // Derive key verifier using PBKDF2 (HMAC-SHA256)
        _masterPasswordHash = HashPassword(password, _salt);

        _isUnlocked = true;
        errorMessage = null;

        LockStateChanged?.Invoke();
        return true;
    }

    public bool Unlock(string password, out string? errorMessage)
    {
        if (!IsVaultInitialized)
        {
            errorMessage = "Vault is not initialized yet. Please set up a master password.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Please enter your master password.";
            return false;
        }

        // Hash provided password with stored salt
        var candidateHash = HashPassword(password, _salt!);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(candidateHash, _masterPasswordHash!))
        {
            errorMessage = "Incorrect master password. Please try again.";
            return false;
        }

        _isUnlocked = true;
        errorMessage = null;

        LockStateChanged?.Invoke();
        return true;
    }

    public void Lock()
    {
        if (_isUnlocked)
        {
            _isUnlocked = false;
            LockStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Computes a PBKDF2 hash (HMAC-SHA256, 100,000 iterations) of the password with salt.
    /// </summary>
    private static byte[] HashPassword(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
    }
}
