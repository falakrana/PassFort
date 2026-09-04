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
/// Automated unit test suite for Phase 5 Encryption, AES-256-GCM, PBKDF2, Vault Binary File format, and Persistence.
/// </summary>
public static class Phase5EncryptionTests
{
    public static void RunAllTests()
    {
        Test_KeyDerivation();
        Test_AesGcm_EncryptionDecryption_Roundtrip();
        Test_AesGcm_WrongKey_Fails();
        Test_AesGcm_TamperedCiphertext_Fails();
        Test_AesGcm_TamperedTag_Fails();
        Test_FileVaultStorage_BinaryRoundtrip();
        Test_FileVaultStorage_InvalidHeader_Fails();
        Test_EncryptedVault_Persistence_EndToEnd();

        Console.WriteLine("[PASS] All Phase5EncryptionTests completed successfully!");
    }

    private static void Test_KeyDerivation()
    {
        var service = new AesGcmEncryptionService();
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        byte[] key1 = service.DeriveKey("MasterPassword123!", salt);
        byte[] key2 = service.DeriveKey("MasterPassword123!", salt);

        Assert(key1.Length == 32, "Derived key must be 32 bytes (256 bits).");
        Assert(key1.SequenceEqual(key2), "Key derivation with identical password and salt must yield identical keys.");

        byte[] differentSalt = RandomNumberGenerator.GetBytes(16);
        byte[] key3 = service.DeriveKey("MasterPassword123!", differentSalt);
        Assert(!key1.SequenceEqual(key3), "Key derivation with different salt must yield different keys.");
    }

    private static void Test_AesGcm_EncryptionDecryption_Roundtrip()
    {
        var service = new AesGcmEncryptionService();
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] key = service.DeriveKey("MySecretPassphrase", salt);

        string originalText = "Sensitive Plaintext Secret Credentials 12345!@#$%";
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(originalText);

        var payload = service.Encrypt(plaintextBytes, key, salt);

        Assert(payload.Nonce.Length == 12, "AES-GCM nonce must be 12 bytes.");
        Assert(payload.Tag.Length == 16, "AES-GCM authentication tag must be 16 bytes.");
        Assert(!payload.Ciphertext.SequenceEqual(plaintextBytes), "Ciphertext must not equal plaintext.");

        byte[] decryptedBytes = service.Decrypt(payload, key);
        string decryptedText = Encoding.UTF8.GetString(decryptedBytes);

        Assert(decryptedText == originalText, "Decrypted text must match original plaintext.");
    }

    private static void Test_AesGcm_WrongKey_Fails()
    {
        var service = new AesGcmEncryptionService();
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] correctKey = service.DeriveKey("CorrectPassword123!", salt);
        byte[] wrongKey = service.DeriveKey("WrongPassword999!", salt);

        byte[] plaintext = Encoding.UTF8.GetBytes("Secret Data");
        var payload = service.Encrypt(plaintext, correctKey, salt);

        bool caughtException = false;
        try
        {
            service.Decrypt(payload, wrongKey);
        }
        catch (CryptographicException)
        {
            caughtException = true;
        }

        Assert(caughtException, "Decrypting with wrong key must throw CryptographicException.");
    }

    private static void Test_AesGcm_TamperedCiphertext_Fails()
    {
        var service = new AesGcmEncryptionService();
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] key = service.DeriveKey("MasterPassword123!", salt);

        byte[] plaintext = Encoding.UTF8.GetBytes("Integrity Verification Payload");
        var payload = service.Encrypt(plaintext, key, salt);

        // Tamper with one byte of ciphertext
        payload.Ciphertext[0] ^= 0xFF;

        bool caughtException = false;
        try
        {
            service.Decrypt(payload, key);
        }
        catch (CryptographicException)
        {
            caughtException = true;
        }

        Assert(caughtException, "Decrypting tampered ciphertext must fail authentication tag validation.");
    }

    private static void Test_AesGcm_TamperedTag_Fails()
    {
        var service = new AesGcmEncryptionService();
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] key = service.DeriveKey("MasterPassword123!", salt);

        byte[] plaintext = Encoding.UTF8.GetBytes("Auth Tag Test Payload");
        var payload = service.Encrypt(plaintext, key, salt);

        // Tamper with auth tag
        payload.Tag[0] ^= 0xAA;

        bool caughtException = false;
        try
        {
            service.Decrypt(payload, key);
        }
        catch (CryptographicException)
        {
            caughtException = true;
        }

        Assert(caughtException, "Decrypting with tampered authentication tag must fail.");
    }

    private static void Test_FileVaultStorage_BinaryRoundtrip()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_vault_{Guid.NewGuid():N}.dat");
        try
        {
            var storage = new FileVaultStorage(tempFile);
            Assert(!storage.VaultExists(), "Temp vault should not exist initially.");

            var originalPayload = new EncryptedPayload
            {
                Salt = RandomNumberGenerator.GetBytes(16),
                Nonce = RandomNumberGenerator.GetBytes(12),
                Tag = RandomNumberGenerator.GetBytes(16),
                Ciphertext = Encoding.UTF8.GetBytes("Encrypted Binary Payload Content")
            };

            storage.WriteVault(originalPayload);
            Assert(storage.VaultExists(), "Vault file should exist after WriteVault.");

            var readPayload = storage.ReadVault();
            Assert(readPayload.Salt.SequenceEqual(originalPayload.Salt), "Read salt must match original.");
            Assert(readPayload.Nonce.SequenceEqual(originalPayload.Nonce), "Read nonce must match original.");
            Assert(readPayload.Tag.SequenceEqual(originalPayload.Tag), "Read tag must match original.");
            Assert(readPayload.Ciphertext.SequenceEqual(originalPayload.Ciphertext), "Read ciphertext must match original.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static void Test_FileVaultStorage_InvalidHeader_Fails()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_corrupt_vault_{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllText(tempFile, "INVALID_HEADER_DATA_STREAM");
            var storage = new FileVaultStorage(tempFile);

            bool caught = false;
            try
            {
                storage.ReadVault();
            }
            catch (InvalidDataException)
            {
                caught = true;
            }

            Assert(caught, "Reading invalid vault header must throw InvalidDataException.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static void Test_EncryptedVault_Persistence_EndToEnd()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_e2e_vault_{Guid.NewGuid():N}.dat");
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            var authService = new AuthenticationService(storage, encryptionService);

            // Step 1: Initialize vault
            bool setupOk = authService.InitializeMasterPassword("StrongPassword123!", "StrongPassword123!", out var setupErr);
            Assert(setupOk && setupErr == null, "Master password setup must succeed.");

            var passwordService = new EncryptedPasswordService(authService, encryptionService, storage);
            int initialCount = passwordService.GetAll().Count();

            // Step 2: Add custom entry
            var entry = new PasswordEntry
            {
                Title = "Persistent Entry",
                Username = "user@test.com",
                Password = "SecretPassWord789!",
                Category = "Personal"
            };
            passwordService.Add(entry);

            Assert(passwordService.GetAll().Count() == initialCount + 1, "Count should increase after Add.");
            Assert(File.Exists(tempFile), "Vault file must be created on disk.");

            // Step 3: Lock vault
            authService.Lock();
            Assert(!authService.IsUnlocked, "Vault should be locked.");

            bool lockExceptionThrown = false;
            try
            {
                passwordService.GetAll();
            }
            catch (InvalidOperationException)
            {
                lockExceptionThrown = true;
            }
            Assert(lockExceptionThrown, "Accessing entries while locked must throw InvalidOperationException.");

            // Step 4: Unlock with correct password
            var newAuthService = new AuthenticationService(storage, encryptionService);
            bool unlockOk = newAuthService.Unlock("StrongPassword123!", out var unlockErr);
            Assert(unlockOk && unlockErr == null, "Unlock with correct password must succeed.");

            var reloadedPasswordService = new EncryptedPasswordService(newAuthService, encryptionService, storage);
            var retrieved = reloadedPasswordService.GetAll().FirstOrDefault(e => e.Title == "Persistent Entry");

            Assert(retrieved != null, "Added entry must persist across lock/reload cycle.");
            Assert(retrieved!.Password == "SecretPassWord789!", "Retrieved entry password must match original.");
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
            throw new Exception($"[TEST FAILURE] {message}");
        }
    }
}
