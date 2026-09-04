using System;

namespace PasswordManager.Services.Authentication;

/// <summary>
/// Service contract for master password creation, validation, authentication, and vault lock state management.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Gets a value indicating whether a master password has been configured for the vault.
    /// </summary>
    bool IsVaultInitialized { get; }

    /// <summary>
    /// Gets a value indicating whether the vault is currently unlocked.
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Gets the currently active derived 256-bit encryption key when vault is unlocked.
    /// Returns null when vault is locked.
    /// </summary>
    byte[]? ActiveKey { get; }

    /// <summary>
    /// Gets the active salt associated with the current vault.
    /// </summary>
    byte[]? ActiveSalt { get; }

    /// <summary>
    /// Event raised when the vault lock state changes (unlocked or locked).
    /// </summary>
    event Action? LockStateChanged;

    /// <summary>
    /// Sets up the initial master password for first-run configuration.
    /// </summary>
    /// <param name="password">The master password to set.</param>
    /// <param name="confirmPassword">Confirmation of the master password.</param>
    /// <param name="errorMessage">Output error message if setup fails validation.</param>
    /// <returns>True if master password was successfully set up and vault unlocked; false otherwise.</returns>
    bool InitializeMasterPassword(string password, string confirmPassword, out string? errorMessage);

    /// <summary>
    /// Attempts to unlock the vault using the provided master password.
    /// </summary>
    /// <param name="password">The master password supplied by the user.</param>
    /// <param name="errorMessage">Output error message if authentication fails.</param>
    /// <returns>True if authentication succeeds and vault is unlocked; false otherwise.</returns>
    bool Unlock(string password, out string? errorMessage);

    /// <summary>
    /// Manually locks the vault, revoking authenticated access.
    /// </summary>
    void Lock();
}
