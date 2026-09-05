using System;
using System.Diagnostics;
using System.Threading;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Clipboard;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.PasswordGenerator;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels;

namespace PasswordManager.Tests;

/// <summary>
/// Unit test suite verifying Clipboard Security functionality (Phase 8).
/// </summary>
public static class Phase8ClipboardTests
{
    private class InMemoryClipboardService : IClipboardService
    {
        private string? _currentText;
        private Timer? _timer;
        private string? _lastCopiedSensitive;

        public InMemoryClipboardService()
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(200);
        }

        public TimeSpan DefaultTimeout { get; set; }
        public event Action<string>? ClipboardCleared;

        public void CopyToClipboard(string text)
        {
            _timer?.Dispose();
            _timer = null;
            _lastCopiedSensitive = null;
            _currentText = text;
        }

        public void CopySensitiveToClipboard(string text, TimeSpan? timeout = null)
        {
            _timer?.Dispose();
            _lastCopiedSensitive = text;
            _currentText = text;
            var effectiveTimeout = timeout ?? DefaultTimeout;

            if (effectiveTimeout > TimeSpan.Zero)
            {
                _timer = new Timer(_ => ClearIfMatches(text), null, (int)effectiveTimeout.TotalMilliseconds, Timeout.Infinite);
            }
        }

        public string? GetText() => _currentText;

        public bool ClearIfMatches(string expectedText)
        {
            if (_currentText == expectedText)
            {
                _currentText = null;
                _timer?.Dispose();
                _timer = null;
                if (_lastCopiedSensitive == expectedText)
                {
                    _lastCopiedSensitive = null;
                }
                ClipboardCleared?.Invoke(expectedText);
                return true;
            }
            return false;
        }

        public void ClearClipboard()
        {
            var prev = _lastCopiedSensitive;
            _currentText = null;
            _timer?.Dispose();
            _timer = null;
            _lastCopiedSensitive = null;
            if (prev != null)
            {
                ClipboardCleared?.Invoke(prev);
            }
        }

        public void SetTextExternally(string text)
        {
            _currentText = text;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    private class InMemoryVaultStorage : IVaultStorage
    {
        private EncryptedPayload? _storedPayload;

        public bool VaultExists() => _storedPayload != null;

        public EncryptedPayload ReadVault()
        {
            return _storedPayload ?? throw new InvalidOperationException("Vault file does not exist.");
        }

        public void WriteVault(EncryptedPayload payload)
        {
            _storedPayload = payload;
        }

        public void DeleteVault()
        {
            _storedPayload = null;
        }
    }

    public static void RunAllTests()
    {
        Debug.WriteLine("=== Running Phase 8 Clipboard Security Tests ===");

        TestStandardCopy();
        TestSensitiveCopyAutoClear();
        TestExternalOverwriteProtection();
        TestMainViewModelCopyCommands();
        TestVaultLockClearsClipboard();
        TestPasswordGeneratorViewModelCopy();

        Debug.WriteLine("=== All Phase 8 Clipboard Security Tests Passed Successfully ===");
    }

    private static void TestStandardCopy()
    {
        using var clipboard = new InMemoryClipboardService();
        clipboard.CopyToClipboard("john_doe");

        Debug.Assert(clipboard.GetText() == "john_doe", "Standard copy should set clipboard text.");
    }

    private static void TestSensitiveCopyAutoClear()
    {
        using var clipboard = new InMemoryClipboardService();
        bool eventFired = false;
        clipboard.ClipboardCleared += _ => eventFired = true;

        clipboard.CopySensitiveToClipboard("SuperSecretPassword123!", TimeSpan.FromMilliseconds(100));
        Debug.Assert(clipboard.GetText() == "SuperSecretPassword123!", "Sensitive copy should immediately place text on clipboard.");

        Thread.Sleep(250);

        Debug.Assert(clipboard.GetText() == null, "Clipboard should automatically clear sensitive text after timeout.");
        Debug.Assert(eventFired, "ClipboardCleared event should fire upon automatic clearing.");
    }

    private static void TestExternalOverwriteProtection()
    {
        using var clipboard = new InMemoryClipboardService();
        clipboard.CopySensitiveToClipboard("AppPassword", TimeSpan.FromMilliseconds(150));

        // Simulate user copying something else externally in another app before timeout
        clipboard.SetTextExternally("ExternalTextFromBrowser");

        Thread.Sleep(250);

        // The auto-clear timer should NOT clear the new external text!
        Debug.Assert(clipboard.GetText() == "ExternalTextFromBrowser", "Auto-clear timer must NOT wipe external clipboard content if changed.");
    }

    private static void TestMainViewModelCopyCommands()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        var passwordService = new EncryptedPasswordService(authService, crypto, storage);
        using var clipboardService = new InMemoryClipboardService();

        authService.InitializeMasterPassword("Master123!", "Master123!", out _);
        passwordService.Add(new PasswordEntry
        {
            Title = "GitHub",
            Username = "octocat",
            Password = "SecretGitHubPassword"
        });

        var vm = new MainViewModel(passwordService, authService, new PasswordGeneratorService(), clipboardService);
        vm.SelectedEntry = vm.PasswordEntries[0];

        Debug.Assert(vm.CopyUsernameCommand.CanExecute(null), "CopyUsernameCommand should be executable for selected entry.");
        vm.CopyUsernameCommand.Execute(null);
        Debug.Assert(clipboardService.GetText() == "octocat", "CopyUsernameCommand should copy username to clipboard.");
        Debug.Assert(vm.StatusMessage.Contains("Username copied"), "StatusMessage should report username copied.");

        Debug.Assert(vm.CopyPasswordCommand.CanExecute(null), "CopyPasswordCommand should be executable for selected entry.");
        vm.CopyPasswordCommand.Execute(null);
        Debug.Assert(clipboardService.GetText() == "SecretGitHubPassword", "CopyPasswordCommand should copy password to clipboard.");
        Debug.Assert(vm.StatusMessage.Contains("auto-clears"), "StatusMessage should report auto-clear timeout.");
    }

    private static void TestVaultLockClearsClipboard()
    {
        var storage = new InMemoryVaultStorage();
        var crypto = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, crypto);
        var passwordService = new EncryptedPasswordService(authService, crypto, storage);
        using var clipboardService = new InMemoryClipboardService();

        authService.InitializeMasterPassword("Master123!", "Master123!", out _);
        passwordService.Add(new PasswordEntry { Title = "Mail", Username = "user", Password = "MailPassword" });

        var vm = new MainViewModel(passwordService, authService, new PasswordGeneratorService(), clipboardService);
        vm.SelectedEntry = vm.PasswordEntries[0];
        vm.CopyPasswordCommand.Execute(null);

        Debug.Assert(clipboardService.GetText() == "MailPassword", "Clipboard should hold password.");

        // Lock vault
        vm.LockCommand.Execute(null);

        Debug.Assert(clipboardService.GetText() == null, "Locking vault must immediately clear sensitive clipboard data.");
    }

    private static void TestPasswordGeneratorViewModelCopy()
    {
        using var clipboardService = new InMemoryClipboardService();
        var genVm = new PasswordGeneratorViewModel(new PasswordGeneratorService(), clipboardService);

        genVm.GeneratePassword();
        var genPass = genVm.GeneratedPassword;
        Debug.Assert(!string.IsNullOrEmpty(genPass), "Password should be generated.");

        Debug.Assert(genVm.CopyCommand.CanExecute(null), "CopyCommand should be executable.");
        genVm.CopyCommand.Execute(null);

        Debug.Assert(clipboardService.GetText() == genPass, "PasswordGeneratorViewModel CopyCommand should copy generated password to clipboard.");
    }
}
