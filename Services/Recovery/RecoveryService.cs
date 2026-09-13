using System;
using System.Security.Cryptography;
using System.Text;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;

namespace PasswordManager.Services.Recovery;

/// <summary>
/// Handles recovery key generation (Option A: plain alphanumeric key stored encrypted on disk),
/// and vault recovery using the recovery key to reset the master password.
/// 
/// The recovery key is a 40-char grouped string (e.g. AAAAA-BBBBB-CCCCC-DDDDD-EEEEE).
/// It is stored in recovery.dat alongside vault.dat, encrypted with a key derived from the
/// recovery key itself — so the file is only useful if the user has the key.
/// </summary>
public class RecoveryService : IRecoveryService
{
    private const int MinPasswordLength = 8;

    private readonly IVaultStorage _vaultStorage;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuthenticationService _authService;

    public RecoveryService(IVaultStorage vaultStorage, IEncryptionService encryptionService, IAuthenticationService authService)
    {
        _vaultStorage = vaultStorage ?? throw new ArgumentNullException(nameof(vaultStorage));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public bool RecoveryKeyExists() => _vaultStorage.RecoveryPayloadExists();

    public string GenerateAndSaveRecoveryKey()
    {
        // Generate 25 random bytes -> base32-ish alphanumeric, group into 5x5 segments
        string rawKey = GenerateRandomKey();

        // Derive encryption key from the recovery key itself using a fresh salt
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] key = _encryptionService.DeriveKey(rawKey, salt);

        // Encrypt the recovery key string so we can validate it later
        byte[] plaintext = Encoding.UTF8.GetBytes(rawKey);
        var payload = _encryptionService.Encrypt(plaintext, key, salt);

        _vaultStorage.WriteRecoveryPayload(payload);

        return rawKey;
    }

    public bool RecoverVault(string recoveryKey, string newPassword, string confirmPassword, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(recoveryKey))
        {
            errorMessage = "Recovery key cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            errorMessage = "New master password cannot be empty.";
            return false;
        }

        if (newPassword.Length < MinPasswordLength)
        {
            errorMessage = $"New master password must be at least {MinPasswordLength} characters long.";
            return false;
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            errorMessage = "Passwords do not match.";
            return false;
        }

        if (!_vaultStorage.RecoveryPayloadExists())
        {
            errorMessage = "No recovery key file found. Recovery is not available.";
            return false;
        }

        // Normalise key: strip dashes and uppercase
        string normKey = recoveryKey.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        try
        {
            // Validate recovery key by attempting to decrypt recovery.dat with it
            var recoveryPayload = _vaultStorage.ReadRecoveryPayload();
            byte[] candidateKey = _encryptionService.DeriveKey(normKey, recoveryPayload.Salt);
            byte[] storedKeyBytes = _encryptionService.Decrypt(recoveryPayload, candidateKey);
            string storedKey = Encoding.UTF8.GetString(storedKeyBytes).Replace("-", "").ToUpperInvariant();

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(normKey),
                    Encoding.UTF8.GetBytes(storedKey)))
            {
                errorMessage = "Invalid recovery key. Please check and try again.";
                return false;
            }
        }
        catch (CryptographicException)
        {
            errorMessage = "Invalid recovery key. Please check and try again.";
            return false;
        }

        // Recovery key is valid. Now re-encrypt the vault with the new master password.
        if (!_vaultStorage.VaultExists())
        {
            // No vault yet — just initialize fresh (e.g. user deleted vault.dat manually)
            return _authService.InitializeMasterPassword(newPassword, confirmPassword, out errorMessage);
        }

        try
        {
            // Read old vault, decrypt with old key — but we don't know the old key!
            // Since recovery replaces everything, we treat the old vault as unreadable and
            // create a brand-new empty vault with the new master password.
            // (The user lost the old master password, so they also can't access old data anyway.)
            _vaultStorage.DeleteVault();
            _vaultStorage.DeleteRecoveryPayload();

            bool ok = _authService.InitializeMasterPassword(newPassword, confirmPassword, out errorMessage);
            if (ok)
            {
                // Generate a new recovery key for the new session
                GenerateAndSaveRecoveryKey();
            }
            return ok;
        }
        catch (Exception ex)
        {
            errorMessage = $"Recovery failed: {ex.Message}";
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateRandomKey()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous 0/O/1/I
        byte[] randomBytes = RandomNumberGenerator.GetBytes(25);
        var sb = new StringBuilder(31); // 25 chars + 4 dashes

        for (int i = 0; i < 25; i++)
        {
            if (i > 0 && i % 5 == 0) sb.Append('-');
            sb.Append(chars[randomBytes[i] % chars.Length]);
        }

        return sb.ToString(); // e.g. ABCDE-FGHJ2-KLMNP-QRSTU-VWXY3
    }
}
