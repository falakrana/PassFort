using System;

namespace PasswordManager.Services.AutoLock;

/// <summary>
/// Service contract for managing vault auto-lock inactivity timers, user activity registration,
/// and automatic lock execution.
/// </summary>
public interface IAutoLockService
{
    /// <summary>
    /// Gets or sets the inactivity timeout duration required to trigger an automatic lock.
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether auto-lock functionality is enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Gets a value indicating whether the inactivity timer is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event raised when the vault is automatically locked due to inactivity.
    /// </summary>
    event EventHandler? AutoLocked;

    /// <summary>
    /// Starts monitoring user inactivity. Typically called when the vault is unlocked.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops monitoring user inactivity and cancels any active countdown.
    /// </summary>
    void Stop();

    /// <summary>
    /// Registers user activity (such as mouse movement, key press, or command execution),
    /// resetting the inactivity countdown timer.
    /// </summary>
    void RegisterActivity();
}
