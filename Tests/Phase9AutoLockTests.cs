using System;
using System.Linq;
using System.Threading;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.AutoLock;
using PasswordManager.Services.Clipboard;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.PasswordGenerator;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels;

namespace PasswordManager.Tests;

/// <summary>
/// Unit test suite verifying Auto-Lock functionality, inactivity timing, activity resets,
/// and sensitive state clearance (Phase 9).
/// </summary>
public static class Phase9AutoLockTests
{
    private class InMemoryVaultStorage : IVaultStorage
    {
        private EncryptedPayload? _payload;

        public bool VaultExists() => _payload != null;

        public EncryptedPayload ReadVault()
        {
            if (_payload == null) throw new InvalidOperationException("Vault does not exist.");
            return _payload;
        }

        public void WriteVault(EncryptedPayload payload)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public void DeleteVault() => _payload = null;
    }

    private class InMemoryClipboardService : IClipboardService
    {
        private string? _currentText;
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public event Action<string>? ClipboardCleared;

        public void CopyToClipboard(string text) => _currentText = text;
        public void CopySensitiveToClipboard(string text, TimeSpan? timeout = null) => _currentText = text;
        public string? GetText() => _currentText;
        public bool ClearIfMatches(string expectedText)
        {
            if (_currentText == expectedText)
            {
                _currentText = null;
                ClipboardCleared?.Invoke(expectedText);
                return true;
            }
            return false;
        }
        public void ClearClipboard() => _currentText = null;
        public void Dispose() => ClearClipboard();
    }

    private class TestDialogService : Services.UI.IDialogService
    {
        public bool ShowConfirmation(string title, string message) => true;
        public void ShowMessage(string title, string message) { }
    }

    public static void RunAllTests()
    {
        TestAutoLockService_CountdownAndLock();
        TestAutoLockService_ActivityReset();
        TestAutoLockService_ManualLockStopsTimer();
        TestAutoLockService_UnlockRestartsTimer();
        TestAutoLockService_DisabledToggle();
        TestAutoLockService_MainViewModelIntegrationAndStateClearance();
    }

    private static void TestAutoLockService_CountdownAndLock()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        using var autoLockService = new AutoLockService(authService);

        autoLockService.Timeout = TimeSpan.FromMilliseconds(150);
        bool autoLockedEventRaised = false;
        autoLockService.AutoLocked += (s, e) => autoLockedEventRaised = true;

        authService.InitializeMasterPassword("MasterPass123!", "MasterPass123!", out _);
        Assert(authService.IsUnlocked, "Vault should be unlocked after initialization.");
        Assert(autoLockService.IsRunning, "AutoLockService should be running when vault is unlocked.");

        // Wait for timeout to elapse
        Thread.Sleep(350);

        Assert(!authService.IsUnlocked, "Vault must automatically lock after inactivity timeout.");
        Assert(!autoLockService.IsRunning, "AutoLockService timer must stop after auto-locking.");
        Assert(autoLockedEventRaised, "AutoLocked event must be raised when auto-lock triggers.");
        Assert(authService.ActiveKey == null, "Active key must be cleared when auto-locked.");
    }

    private static void TestAutoLockService_ActivityReset()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        using var autoLockService = new AutoLockService(authService);

        autoLockService.Timeout = TimeSpan.FromMilliseconds(250);
        authService.InitializeMasterPassword("MasterPass123!", "MasterPass123!", out _);

        Assert(authService.IsUnlocked, "Vault should be unlocked.");

        // Wait 100ms, then register user activity (resets 250ms countdown)
        Thread.Sleep(100);
        autoLockService.RegisterActivity();

        // Wait another 180ms (total 280ms since start, but only 180ms since activity reset)
        Thread.Sleep(180);
        Assert(authService.IsUnlocked, "Vault should STILL be unlocked because activity reset timer.");

        // Wait remaining time for timer to expire after reset
        Thread.Sleep(300);
        Assert(!authService.IsUnlocked, "Vault should lock after updated countdown elapses.");
    }

    private static void TestAutoLockService_ManualLockStopsTimer()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        using var autoLockService = new AutoLockService(authService);

        autoLockService.Timeout = TimeSpan.FromMilliseconds(500);
        authService.InitializeMasterPassword("MasterPass123!", "MasterPass123!", out _);

        Assert(autoLockService.IsRunning, "AutoLockService should be running.");

        // Manually lock vault
        authService.Lock();

        Assert(!authService.IsUnlocked, "Vault should be locked.");
        Assert(!autoLockService.IsRunning, "Manual lock must stop auto-lock timer.");
    }

    private static void TestAutoLockService_UnlockRestartsTimer()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        var passwordService = new EncryptedPasswordService(authService, crypto, storage);
        using var autoLockService = new AutoLockService(authService);

        autoLockService.Timeout = TimeSpan.FromMilliseconds(500);
        authService.InitializeMasterPassword("MasterPass123!", "MasterPass123!", out _);
        passwordService.Add(new PasswordEntry { Title = "Test", Username = "user", Password = "password" });

        authService.Lock();
        Assert(!autoLockService.IsRunning, "Timer should be stopped when locked.");

        bool unlocked = authService.Unlock("MasterPass123!", out _);
        Assert(unlocked, "Vault should unlock.");
        Assert(autoLockService.IsRunning, "Unlocking vault must automatically restart auto-lock timer.");
    }

    private static void TestAutoLockService_DisabledToggle()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        using var autoLockService = new AutoLockService(authService);

        autoLockService.Timeout = TimeSpan.FromMilliseconds(150);
        authService.InitializeMasterPassword("MasterPass123!", "MasterPass123!", out _);

        // Disable auto lock
        autoLockService.IsEnabled = false;
        Assert(!autoLockService.IsRunning, "Disabling auto-lock must stop timer.");

        Thread.Sleep(250);
        Assert(authService.IsUnlocked, "Vault must remain unlocked when auto-lock is disabled.");

        // Re-enable auto lock
        autoLockService.IsEnabled = true;
        Assert(autoLockService.IsRunning, "Re-enabling auto-lock must start timer.");
    }

    private static void TestAutoLockService_MainViewModelIntegrationAndStateClearance()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        var passwordService = new EncryptedPasswordService(authService, crypto, storage);
        using var clipboardService = new InMemoryClipboardService();
        using var autoLockService = new AutoLockService(authService);

        autoLockService.Timeout = TimeSpan.FromMilliseconds(250);

        authService.InitializeMasterPassword("MasterPass123!", "MasterPass123!", out _);
        var targetEntry = new PasswordEntry { Title = "Banking", Username = "bankuser", Password = "BankPassword123" };
        passwordService.Add(targetEntry);

        var vm = new MainViewModel(passwordService, authService, new PasswordGeneratorService(), clipboardService, autoLockService, new TestDialogService());
        vm.SelectedEntry = vm.PasswordEntries.FirstOrDefault(e => e.Title == targetEntry.Title);
        vm.CopyPasswordCommand.Execute(null);

        Assert(clipboardService.GetText() == targetEntry.Password, "Clipboard should hold password.");
        Assert(vm.SelectedEntry != null, "Selected entry should be active.");
        Assert(vm.IsVaultUnlocked, "MainViewModel should reflect unlocked vault.");

        // Allow auto-lock to fire
        Thread.Sleep(400);
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }

        Assert(!vm.IsVaultUnlocked, "MainViewModel.IsVaultUnlocked must be false after auto-lock.");
        Assert(vm.SelectedEntry == null, "MainViewModel.SelectedEntry must be cleared after auto-lock.");
        Assert(vm.PasswordEntries.Count == 0, "MainViewModel.PasswordEntries must be cleared after auto-lock.");
        Assert(clipboardService.GetText() == null, "Clipboard must be cleared after auto-lock.");
        Assert(vm.StatusMessage.Contains("locked"), "StatusMessage should inform user vault was locked.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[TEST FAILURE] {message}");
        }
    }
}
