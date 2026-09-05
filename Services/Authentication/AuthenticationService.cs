using System;
using System.IO;
using System.Security.Cryptography;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;

namespace PasswordManager.Services.Authentication;

/// <summary>
/// Handles master password setup, AES-GCM verification, key derivation via PBKDF2, and vault lock state management.
/// Master passwords and keys are never stored in plaintext on disk.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private const int SaltSizeBytes = 16;
    private const int MinPasswordLength = 8;

    private readonly IVaultStorage _vaultStorage;
    private readonly IEncryptionService _encryptionService;

    private byte[]? _activeSalt;
    private byte[]? _activeKey;
    private bool _isUnlocked;

    public AuthenticationService(IVaultStorage vaultStorage, IEncryptionService encryptionService)
    {
        _vaultStorage = vaultStorage ?? throw new ArgumentNullException(nameof(vaultStorage));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public bool IsVaultInitialized => _vaultStorage.VaultExists() || (_activeSalt != null && _activeKey != null);

    public bool IsUnlocked => _isUnlocked && _activeKey != null;

    public byte[]? ActiveKey => _activeKey;

    public byte[]? ActiveSalt => _activeSalt;

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
        _activeSalt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

        // Derive key using PBKDF2 (HMAC-SHA256, 100,000 iterations)
        _activeKey = _encryptionService.DeriveKey(password, _activeSalt);

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

        byte[]? candidateKey = null;
        try
        {
            // Read vault file header & payload
            var payload = _vaultStorage.ReadVault();

            // Derive key from input password and stored salt
            candidateKey = _encryptionService.DeriveKey(password, payload.Salt);

            // Attempt to decrypt payload to verify authentication tag (AES-GCM integrity check)
            _encryptionService.Decrypt(payload, candidateKey);

            // Decryption succeeded, master password is authentic!
            _activeSalt = payload.Salt;
            _activeKey = candidateKey;
            _isUnlocked = true;
            errorMessage = null;

            LockStateChanged?.Invoke();
            return true;
        }
        catch (CryptographicException)
        {
            if (candidateKey != null) Array.Clear(candidateKey, 0, candidateKey.Length);
            errorMessage = "Incorrect master password or corrupted vault data. Please try again.";
            return false;
        }
        catch (InvalidDataException)
        {
            if (candidateKey != null) Array.Clear(candidateKey, 0, candidateKey.Length);
            errorMessage = "Vault file format is invalid or corrupted.";
            return false;
        }
        catch (FileNotFoundException)
        {
            if (candidateKey != null) Array.Clear(candidateKey, 0, candidateKey.Length);
            errorMessage = "Vault file could not be found.";
            return false;
        }
        catch (Exception)
        {
            if (candidateKey != null) Array.Clear(candidateKey, 0, candidateKey.Length);
            errorMessage = "An unexpected error occurred while accessing the vault.";
            return false;
        }
    }

    public void Lock()
    {
        if (_isUnlocked || _activeKey != null)
        {
            if (_activeKey != null)
            {
                Array.Clear(_activeKey, 0, _activeKey.Length);
                _activeKey = null;
            }
            _activeSalt = null;
            _isUnlocked = false;
            LockStateChanged?.Invoke();
        }
    }
}
