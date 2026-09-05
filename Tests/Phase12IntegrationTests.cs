using System;
using System.IO;
using System.Linq;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;

namespace PasswordManager.Tests;

/// <summary>
/// Phase 12 Integration Test suite validating full end-to-end vault lifecycles:
/// Create vault, Save encrypted vault, Load vault, Unlock vault, Modify vault, Lock vault,
/// Reopen application simulation, and Recover from invalid/corrupted vault.
/// </summary>
public static class Phase12IntegrationTests
{
    public static void RunAllTests()
    {
        Test_EndToEnd_VaultLifecycle_Integration();
        Test_ReopenApplication_Simulation_Integration();
        Test_VaultRecovery_InvalidOrCorruptedVault_Integration();

        Console.WriteLine("[PASS] All Phase12IntegrationTests completed successfully!");
    }

    private static void Test_EndToEnd_VaultLifecycle_Integration()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"phase12_lifecycle_{Guid.NewGuid():N}.dat");
        try
        {
            var storage = new FileVaultStorage(tempFile);
            var encryptionService = new AesGcmEncryptionService();
            var authService = new AuthenticationService(storage, encryptionService);

            // 1. Create vault
            Assert(!storage.VaultExists(), "Vault file should not exist initially.");
            bool initOk = authService.InitializeMasterPassword("Phase12MasterPass!2026", "Phase12MasterPass!2026", out var initErr);
            Assert(initOk && initErr == null, "Vault initialization must succeed.");
            Assert(storage.VaultExists(), "Vault file must be created on disk.");

            var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);

            // 2. Modify vault (Add)
            var entry1 = new PasswordEntry
            {
                Title = "GitHub Enterprise",
                Username = "dev_user",
                Password = "GitSecurePassword123!",
                WebsiteUrl = "https://github.com",
                Category = "Development"
            };
            passwordService.Add(entry1);
            Assert(passwordService.GetAll().Count() == 1, "Password entry count should be 1 after Add.");

            // Modify vault (Edit)
            var createdEntry = passwordService.GetAll().First();
            createdEntry.Password = "UpdatedGitPassword456!";
            passwordService.Update(createdEntry);

            var updatedEntry = passwordService.GetById(createdEntry.Id);
            Assert(updatedEntry != null && updatedEntry.Password == "UpdatedGitPassword456!", "Updated password must persist in memory and file.");

            // 3. Lock vault
            authService.Lock();
            Assert(!authService.IsUnlocked, "Vault must be locked.");

            // 4. Unlock vault
            bool unlockOk = authService.Unlock("Phase12MasterPass!2026", out var unlockErr);
            Assert(unlockOk && unlockErr == null, "Unlocking with correct password must succeed.");

            // Modify vault (Delete)
            passwordService.Delete(createdEntry.Id);
            Assert(passwordService.GetAll().Count() == 0, "Password entry count should be 0 after Delete.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static void Test_ReopenApplication_Simulation_Integration()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"phase12_reopen_{Guid.NewGuid():N}.dat");
        string masterPassword = "ApplicationReopenPass!99";

        try
        {
            // First Application Run Session
            {
                var storage = new FileVaultStorage(tempFile);
                var encryptionService = new AesGcmEncryptionService();
                var authService = new AuthenticationService(storage, encryptionService);

                authService.InitializeMasterPassword(masterPassword, masterPassword, out _);
                var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);

                passwordService.Add(new PasswordEntry
                {
                    Title = "AWS Console",
                    Username = "admin_aws",
                    Password = "CloudPassword777!",
                    Category = "Work"
                });

                authService.Lock();
            }

            // Second Application Run Session (Reopen Application)
            {
                var storage = new FileVaultStorage(tempFile);
                var encryptionService = new AesGcmEncryptionService();
                var authService = new AuthenticationService(storage, encryptionService);

                Assert(authService.IsVaultInitialized, "Vault storage must detect existing initialized vault.");
                Assert(!authService.IsUnlocked, "Newly launched auth service must start locked.");

                bool unlockOk = authService.Unlock(masterPassword, out var unlockErr);
                Assert(unlockOk && unlockErr == null, "Unlocking reopened application vault must succeed.");

                var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);
                var entries = passwordService.GetAll().ToList();

                Assert(entries.Count == 1, "Persisted entry count must be 1.");
                Assert(entries[0].Title == "AWS Console", "Persisted entry title must match 'AWS Console'.");
                Assert(entries[0].Password == "CloudPassword777!", "Persisted entry password must match original.");
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static void Test_VaultRecovery_InvalidOrCorruptedVault_Integration()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"phase12_corrupt_{Guid.NewGuid():N}.dat");
        try
        {
            // Create valid vault file
            var storage = new FileVaultStorage(tempFile);
            var encryptionService = new AesGcmEncryptionService();
            var authService = new AuthenticationService(storage, encryptionService);

            authService.InitializeMasterPassword("ValidPass123!", "ValidPass123!", out _);
            authService.Lock();

            // Corrupt file header on disk
            byte[] fileBytes = File.ReadAllBytes(tempFile);
            fileBytes[0] = (byte)'X'; // Corrupt 'S' in SPMV header
            File.WriteAllBytes(tempFile, fileBytes);

            // Attempt to unlock corrupted vault
            bool unlockOk = authService.Unlock("ValidPass123!", out var errorMsg);
            Assert(!unlockOk, "Unlocking corrupted vault must fail.");
            Assert(errorMsg != null, "Error message must be returned when vault header is corrupted.");
            Assert(!authService.IsUnlocked, "AuthService must remain locked on corrupted vault attempt.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[INTEGRATION TEST FAILURE] {message}");
        }
    }
}
