using System;
using PasswordManager.Services.Authentication;
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

        Console.WriteLine("[PASS] All MasterPasswordTests completed successfully!");
    }

    private static void Test_UninitializedVaultState()
    {
        var authService = new AuthenticationService();
        Assert(!authService.IsVaultInitialized, "Vault should not be initialized initially.");
        Assert(!authService.IsUnlocked, "Vault should be locked initially.");
    }

    private static void Test_MasterPasswordValidation_ShortLength()
    {
        var authService = new AuthenticationService();
        bool success = authService.InitializeMasterPassword("pass", "pass", out var error);

        Assert(!success, "Setup with short password should fail.");
        Assert(!authService.IsVaultInitialized, "Vault should remain uninitialized.");
        Assert(error != null && error.Contains("at least 8 characters"), "Error message should mention minimum length.");
    }

    private static void Test_MasterPasswordValidation_Mismatch()
    {
        var authService = new AuthenticationService();
        bool success = authService.InitializeMasterPassword("MySecurePass123", "DifferentPass123", out var error);

        Assert(!success, "Setup with mismatched passwords should fail.");
        Assert(!authService.IsVaultInitialized, "Vault should remain uninitialized.");
        Assert(error == "Passwords do not match.", "Error message should report password mismatch.");
    }

    private static void Test_MasterPasswordSetup_Success()
    {
        var authService = new AuthenticationService();
        bool success = authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out var error);

        Assert(success, "Setup with valid matching password should succeed.");
        Assert(error == null, "Error message should be null on success.");
        Assert(authService.IsVaultInitialized, "Vault should now be initialized.");
        Assert(authService.IsUnlocked, "Vault should be unlocked after setup.");
    }

    private static void Test_Unlock_WrongPassword()
    {
        var authService = new AuthenticationService();
        authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out _);
        authService.Lock();

        Assert(!authService.IsUnlocked, "Vault should be locked.");

        bool unlockResult = authService.Unlock("WrongSecret999", out var error);

        Assert(!unlockResult, "Unlock with wrong password must fail.");
        Assert(!authService.IsUnlocked, "Vault must remain locked on wrong password.");
        Assert(error == "Incorrect master password. Please try again.", "Error should state incorrect password.");
    }

    private static void Test_Unlock_CorrectPassword()
    {
        var authService = new AuthenticationService();
        authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out _);
        authService.Lock();

        bool unlockResult = authService.Unlock("MasterSecret123!", out var error);

        Assert(unlockResult, "Unlock with correct password should succeed.");
        Assert(error == null, "Error should be null on successful unlock.");
        Assert(authService.IsUnlocked, "Vault must be unlocked.");
    }

    private static void Test_ManualLock()
    {
        var authService = new AuthenticationService();
        authService.InitializeMasterPassword("MasterSecret123!", "MasterSecret123!", out _);
        Assert(authService.IsUnlocked, "Vault should be unlocked after setup.");

        authService.Lock();
        Assert(!authService.IsUnlocked, "Vault should be locked after calling Lock().");
    }

    private static void Test_LoginViewModel_Integration()
    {
        var authService = new AuthenticationService();
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[TEST FAILURE] {message}");
        }
    }
}
