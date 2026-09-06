using System;
using System.IO;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels;

namespace PasswordManager.Tests;

/// <summary>
/// Automated unit test suite for Phase 4 Master Password security, authentication, and vault locking.
/// </summary>
public static class MasterPasswordTests
{
    public static void RunAllTests()
    {
        Test_UninitializedVaultState();
        Test_MasterPasswordValidation_ShortLength();
        Test_MasterPasswordValidation_Mismatch();
        Test_MasterPasswordSetup_Success();
        Test_Unlock_WrongPassword();
        Test_Unlock_CorrectPassword();
        Test_ManualLock();
        Test_LoginViewModel_Integration();
        Test_ChangeMasterPassword_ValidationAndReencryption();

        Console.WriteLine("[PASS] All MasterPasswordTests completed successfully!");
    }

    private static (IAuthenticationService authService, string tempFile) CreateAuthService()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"master_pass_test_{Guid.NewGuid():N}.dat");
        var storage = new FileVaultStorage(tempFile);
        var encryptionService = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, encryptionService);
        return (authService, tempFile);
    }

    private static void CleanupTempFile(string tempFile)
    {
        if (File.Exists(tempFile))
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static void Test_UninitializedVaultState()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            Assert(!authService.IsVaultInitialized, "Vault should not be initialized initially.");
            Assert(!authService.IsUnlocked, "Vault should be locked initially.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_MasterPasswordValidation_ShortLength()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            bool success = authService.InitializeMasterPassword("pass", "pass", out var error);

            Assert(!success, "Setup with short password should fail.");
            Assert(!authService.IsVaultInitialized, "Vault should remain uninitialized.");
            Assert(error != null && error.Contains("at least 8 characters"), "Error message should mention minimum length.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_MasterPasswordValidation_Mismatch()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            bool success = authService.InitializeMasterPassword("MySecurePass123", "DifferentPass123", out var error);

            Assert(!success, "Setup with mismatched passwords should fail.");
            Assert(!authService.IsVaultInitialized, "Vault should remain uninitialized.");
            Assert(error == "Passwords do not match.", "Error message should report password mismatch.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_MasterPasswordSetup_Success()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            bool success = authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out var error);

            Assert(success, "Setup with valid matching password should succeed.");
            Assert(error == null, "Error message should be null on success.");
            Assert(authService.IsVaultInitialized, "Vault should now be initialized after setting master password.");
            Assert(authService.IsUnlocked, "Vault should be unlocked after setup.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_Unlock_WrongPassword()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out _);
            // Save initial vault payload so unlock can read salt
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);

            authService.Lock();

            Assert(!authService.IsUnlocked, "Vault should be locked.");

            bool unlockResult = authService.Unlock("WrongSecret999", out var error);

            Assert(!unlockResult, "Unlock with wrong password must fail.");
            Assert(!authService.IsUnlocked, "Vault must remain locked on wrong password.");
            Assert(error != null && error.Contains("Incorrect master password"), "Error should state incorrect password.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_Unlock_CorrectPassword()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out _);
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);

            authService.Lock();

            bool unlockResult = authService.Unlock("MasterSecret123!", out var error);

            Assert(unlockResult, "Unlock with correct password should succeed.");
            Assert(error == null, "Error should be null on successful unlock.");
            Assert(authService.IsUnlocked, "Vault must be unlocked.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_ManualLock()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out _);
            Assert(authService.IsUnlocked, "Vault should be unlocked after setup.");

            authService.Lock();
            Assert(!authService.IsUnlocked, "Vault should be locked after calling Lock().");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_LoginViewModel_Integration()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            var vm = new LoginViewModel(authService);

            Assert(vm.IsFirstRun, "LoginViewModel should identify first run when vault uninitialized.");

            vm.Password = "Short1";
            vm.ConfirmPassword = "Short1";
            vm.SetupCommand.Execute(null);

            Assert(vm.ErrorMessage != null, "Setup command should record error message on validation failure.");

            vm.Password = "StrongMasterPass123!";
            vm.ConfirmPassword = "StrongMasterPass123!";
            
            bool authenticatedFired = false;
            vm.Authenticated += () => authenticatedFired = true;

            vm.SetupCommand.Execute(null);

            Assert(authenticatedFired, "Authenticated event should fire upon successful setup.");
            Assert(!vm.IsFirstRun, "IsFirstRun should be false after setup.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Test_ChangeMasterPassword_ValidationAndReencryption()
    {
        var (authService, tempFile) = CreateAuthService();
        try
        {
            authService.InitializeMasterPassword("OldSecret123!", "OldSecret123!", out _);
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);

            // 1. Wrong current password fails
            bool result1 = authService.ChangeMasterPassword("WrongOldPass!", "NewSecret123!", "NewSecret123!", out var err1);
            Assert(!result1, "ChangeMasterPassword with wrong current password should fail.");
            Assert(err1 == "Current master password is incorrect.", "Error message should indicate incorrect current password.");

            // 2. Mismatched new passwords fail
            bool result2 = authService.ChangeMasterPassword("OldSecret123!", "NewSecret123!", "MismatchPass123!", out var err2);
            Assert(!result2, "ChangeMasterPassword with mismatched new passwords should fail.");
            Assert(err2 == "New passwords do not match.", "Error message should indicate password mismatch.");

            // 3. Successful change
            bool result3 = authService.ChangeMasterPassword("OldSecret123!", "NewSecret123!", "NewSecret123!", out var err3);
            Assert(result3, "ChangeMasterPassword with correct credentials should succeed.");
            Assert(err3 == null, "Error message should be null on success.");

            // 4. Lock & verify unlock works with new password and fails with old password
            authService.Lock();
            Assert(!authService.Unlock("OldSecret123!", out _), "Unlock with old password after change must fail.");
            Assert(authService.Unlock("NewSecret123!", out _), "Unlock with new password after change must succeed.");
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[TEST FAILURE] {message}");
        }
    }
}
