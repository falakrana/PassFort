using System;

namespace PasswordManager.Tests;

/// <summary>
/// Master Phase 12 Unit Testing Suite.
/// Runs and validates all domain-specific unit test suites across encryption, security,
/// password CRUD, search, categories, generator, auto-lock, and clipboard security.
/// </summary>
public static class Phase12TestingTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("[Tests] Beginning Phase 12 Comprehensive Unit Test Verification...");

        // 1. Encryption & Cryptography Unit Tests
        Phase5EncryptionTests.RunAllTests();

        // 2. Security Hardening Unit Tests
        Phase11SecurityHardeningTests.RunAllTests();

        // 3. Password CRUD Unit Tests
        PasswordCRUDTests.RunAllTests();

        // 4. Search & Categories Unit Tests
        Phase6SearchAndCategoryTests.RunAllTests();

        // 5. Password Generator Unit Tests
        Phase7PasswordGeneratorTests.RunAllTests();

        // 6. Auto-Lock Service Unit Tests
        Phase9AutoLockTests.RunAllTests();

        // 7. Clipboard Security Unit Tests
        Phase8ClipboardTests.RunAllTests();

        // 8. MVVM Primitives Unit Tests
        MVVMTests.RunAllTests();

        Console.WriteLine("[PASS] All Phase 12 Unit Test suites executed and verified successfully!");
    }
}
