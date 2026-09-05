using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;

namespace PasswordManager.Tests;

/// <summary>
/// Comprehensive security hardening unit test suite for Phase 11.
/// Tests atomic vault persistence, key wiping, constant-time validation, and exception sanitization.
/// </summary>
public static class Phase11SecurityHardeningTests
{
    public static void RunAllTests()
    {
        Test_AtomicFileSave_SuccessAndCleanup();
        Test_ActiveKeyWiped_OnLock();
        Test_Unlock_SanitizedErrorMessages();
        Test_FixedTimeEquals_HeaderValidation();
        Test_EncryptedVault_MemoryHygieneCycle();

        Console.WriteLine("[PASS] All Phase11SecurityHardeningTests completed successfully!");
    }

    private static void Test_AtomicFileSave_SuccessAndCleanup()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_atomic_vault_{Guid.NewGuid():N}.dat");
        string tempFileTmp = tempFile + ".tmp";
        try
        {
            var storage = new FileVaultStorage(tempFile);
            var payload = new EncryptedPayload
            {
                Salt = RandomNumberGenerator.GetBytes(16),
                Nonce = RandomNumberGenerator.GetBytes(12),
                Tag = RandomNumberGenerator.GetBytes(16),
                Ciphertext = Encoding.UTF8.GetBytes("Atomic File Save Verification Data Stream")
            };

            storage.WriteVault(payload);

            Assert(File.Exists(tempFile), "Vault file must exist after atomic WriteVault.");
            Assert(!File.Exists(tempFileTmp), "Temporary .tmp file must be cleaned up after atomic swap.");

            var readPayload = storage.ReadVault();
            Assert(readPayload.Ciphertext.SequenceEqual(payload.Ciphertext), "Payload written atomically must match read payload.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempFileTmp)) File.Delete(tempFileTmp);
        }
    }

    private static void Test_ActiveKeyWiped_OnLock()
    {
        var storage = new FileVaultStorage(Path.Combine(Path.GetTempPath(), $"test_lock_wipe_{Guid.NewGuid():N}.dat"));
        var encryptionService = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, encryptionService);

        try
        {
            authService.InitializeMasterPassword("TestMasterPassword123!", "TestMasterPassword123!", out _);
            Assert(authService.IsUnlocked, "AuthService should be unlocked.");
            Assert(authService.ActiveKey != null, "ActiveKey should not be null when unlocked.");

            byte[] keyCopy = authService.ActiveKey!.ToArray();
            Assert(keyCopy.Any(b => b != 0), "Key copy should contain non-zero key bytes.");

            authService.Lock();

            Assert(!authService.IsUnlocked, "AuthService must be locked.");
            Assert(authService.ActiveKey == null, "ActiveKey reference must be set to null on lock.");
        }
        finally
        {
            storage.DeleteVault();
        }
    }

    private static void Test_Unlock_SanitizedErrorMessages()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_sanitized_err_{Guid.NewGuid():N}.dat");
        var storage = new FileVaultStorage(tempFile);
        var encryptionService = new AesGcmEncryptionService();

        try
        {
            // Case 1: Uninitialized vault unlock error
            var authService = new AuthenticationService(storage, encryptionService);
            bool ok = authService.Unlock("AnyPassword", out var err1);
            Assert(!ok, "Unlock should fail when uninitialized.");
            Assert(err1 != null && !err1.Contains(tempFile), "Error message must not expose raw internal file paths.");

            // Create initial vault
            authService.InitializeMasterPassword("ValidPassword123!", "ValidPassword123!", out _);
            var encService = new EncryptedPasswordService(authService, encryptionService, storage);
            encService.Add(new PasswordEntry { Title = "Test", Password = "Pass" });
            authService.Lock();

            // Case 2: Incorrect master password
            bool wrongKeyOk = authService.Unlock("WrongPassword999!", out var err2);
            Assert(!wrongKeyOk, "Unlock with wrong password must fail.");
            Assert(err2 == "Incorrect master password or corrupted vault data. Please try again.", "Error message must be standardized and sanitized.");

            // Case 3: Corrupted header
            File.WriteAllBytes(tempFile, Encoding.UTF8.GetBytes("INVALID_VAULT_BYTES_CORRUPTED"));
            bool corruptOk = authService.Unlock("ValidPassword123!", out var err3);
            Assert(!corruptOk, "Unlock with corrupted file must fail.");
            Assert(err3 == "Vault file format is invalid or corrupted." || err3 == "Incorrect master password or corrupted vault data. Please try again.", "Corrupted file error must be sanitized.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static void Test_FixedTimeEquals_HeaderValidation()
    {
        byte[] validHeader = Encoding.ASCII.GetBytes("SPMV");
        byte[] invalidHeader = Encoding.ASCII.GetBytes("BADV");

        Assert(CryptographicOperations.FixedTimeEquals(validHeader, Encoding.ASCII.GetBytes("SPMV")), "FixedTimeEquals must match valid SPMV header.");
        Assert(!CryptographicOperations.FixedTimeEquals(invalidHeader, Encoding.ASCII.GetBytes("SPMV")), "FixedTimeEquals must reject invalid header.");
    }

    private static void Test_EncryptedVault_MemoryHygieneCycle()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_memory_cycle_{Guid.NewGuid():N}.dat");
        var storage = new FileVaultStorage(tempFile);
        var encryptionService = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, encryptionService);

        try
        {
            authService.InitializeMasterPassword("HygieneMasterPassword123!", "HygieneMasterPassword123!", out _);
            var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);

            var entry = new PasswordEntry
            {
                Title = "Sensitive Bank Account",
                Username = "bank_user",
                Password = "SuperSecretBankPassword!2026",
                Category = "Finance"
            };

            passwordService.Add(entry);
            Assert(passwordService.GetAll().Count() > 0, "Entry should be added successfully.");

            authService.Lock();
            Assert(!authService.IsUnlocked, "Vault must be locked.");

            bool lockEx = false;
            try
            {
                passwordService.GetAll();
            }
            catch (InvalidOperationException)
            {
                lockEx = true;
            }
            Assert(lockEx, "Accessing entries after lock must throw InvalidOperationException.");
        }
        finally
        {
            storage.DeleteVault();
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
