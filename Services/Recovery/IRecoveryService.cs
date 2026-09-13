namespace PasswordManager.Services.Recovery;

/// <summary>
/// Contract for recovery key generation, persistence, and vault recovery when the master password is forgotten.
/// </summary>
public interface IRecoveryService
{
    /// <summary>
    /// Generates a new 40-character grouped recovery key (e.g. AAAAA-BBBBB-CCCCC-DDDDD-EEEEE),
    /// encrypts it, and persists it to the recovery sidecar file.
    /// Returns the plain-text recovery key to be shown to the user once.
    /// </summary>
    string GenerateAndSaveRecoveryKey();

    /// <summary>
    /// Returns true if a recovery key file exists on disk.
    /// </summary>
    bool RecoveryKeyExists();

    /// <summary>
    /// Attempts to use the supplied recovery key to re-encrypt the vault with a new master password.
    /// </summary>
    /// <param name="recoveryKey">The recovery key the user saved during first-run.</param>
    /// <param name="newPassword">The new master password to set.</param>
    /// <param name="confirmPassword">Confirmation of the new master password.</param>
    /// <param name="errorMessage">Output error description if the operation fails.</param>
    /// <returns>True if the vault was successfully re-encrypted and unlocked with the new password.</returns>
    bool RecoverVault(string recoveryKey, string newPassword, string confirmPassword, out string? errorMessage);
}
