using System;
using System.Linq;
using PasswordManager.Services.PasswordGenerator;
using PasswordManager.ViewModels;

namespace PasswordManager.Tests;

/// <summary>
/// Automated unit tests for Phase 7 — Password Generator service and ViewModel.
/// </summary>
public static class Phase7PasswordGeneratorTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("[Tests] Running Phase 7 Password Generator tests...");

        Test_GeneratePassword_LengthCorrectness();
        Test_GeneratePassword_CharacterSetsIncluded();
        Test_GeneratePassword_InvalidOptions_FailsValidationAndThrows();
        Test_GeneratePassword_CryptographicallySecureRandomDistribution();
        Test_PasswordGeneratorViewModel_CommandsAndBindings();

        Console.WriteLine("[Tests] ✅ All Phase 7 Password Generator tests passed!");
    }

    private static void Test_GeneratePassword_LengthCorrectness()
    {
        var service = new PasswordGeneratorService();
        int[] lengthsToTest = new[] { 4, 8, 16, 32, 64, 128 };

        foreach (var len in lengthsToTest)
        {
            var options = new PasswordGeneratorOptions { Length = len };
            var password = service.GeneratePassword(options);
            Assert(password.Length == len, $"Expected password length {len}, but got {password.Length}");
        }

        Console.WriteLine("  - Test_GeneratePassword_LengthCorrectness passed.");
    }

    private static void Test_GeneratePassword_CharacterSetsIncluded()
    {
        var service = new PasswordGeneratorService();

        // 1. All character sets enabled
        var optionsAll = new PasswordGeneratorOptions
        {
            Length = 20,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeNumbers = true,
            IncludeSymbols = true
        };
        var pwdAll = service.GeneratePassword(optionsAll);
        Assert(pwdAll.Any(char.IsUpper), "Expected password to contain uppercase characters.");
        Assert(pwdAll.Any(char.IsLower), "Expected password to contain lowercase characters.");
        Assert(pwdAll.Any(char.IsDigit), "Expected password to contain numeric digits.");
        Assert(pwdAll.Any(c => PasswordGeneratorService.SymbolChars.Contains(c)), "Expected password to contain symbols.");

        // 2. Only Numbers enabled
        var optionsNumbersOnly = new PasswordGeneratorOptions
        {
            Length = 16,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeNumbers = true,
            IncludeSymbols = false
        };
        var pwdNumbers = service.GeneratePassword(optionsNumbersOnly);
        Assert(pwdNumbers.All(char.IsDigit), "Expected numbers-only password to contain only digits.");

        // 3. Only Uppercase enabled
        var optionsUpperOnly = new PasswordGeneratorOptions
        {
            Length = 16,
            IncludeUppercase = true,
            IncludeLowercase = false,
            IncludeNumbers = false,
            IncludeSymbols = false
        };
        var pwdUpper = service.GeneratePassword(optionsUpperOnly);
        Assert(pwdUpper.All(char.IsUpper), "Expected uppercase-only password to contain only uppercase letters.");

        Console.WriteLine("  - Test_GeneratePassword_CharacterSetsIncluded passed.");
    }

    private static void Test_GeneratePassword_InvalidOptions_FailsValidationAndThrows()
    {
        var service = new PasswordGeneratorService();

        // Length < 4
        var tooShort = new PasswordGeneratorOptions { Length = 3 };
        Assert(!service.ValidateOptions(tooShort, out var err1), "Expected validation failure for length < 4.");
        AssertThrows<ArgumentException>(() => service.GeneratePassword(tooShort));

        // Length > 128
        var tooLong = new PasswordGeneratorOptions { Length = 200 };
        Assert(!service.ValidateOptions(tooLong, out var err2), "Expected validation failure for length > 128.");
        AssertThrows<ArgumentException>(() => service.GeneratePassword(tooLong));

        // No character set selected
        var noneSelected = new PasswordGeneratorOptions
        {
            Length = 16,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeNumbers = false,
            IncludeSymbols = false
        };
        Assert(!service.ValidateOptions(noneSelected, out var err3), "Expected validation failure when no character sets selected.");
        AssertThrows<ArgumentException>(() => service.GeneratePassword(noneSelected));

        Console.WriteLine("  - Test_GeneratePassword_InvalidOptions_FailsValidationAndThrows passed.");
    }

    private static void Test_GeneratePassword_CryptographicallySecureRandomDistribution()
    {
        var service = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptions { Length = 16 };

        const int sampleCount = 50;
        var generatedSet = new System.Collections.Generic.HashSet<string>();

        for (int i = 0; i < sampleCount; i++)
        {
            var pwd = service.GeneratePassword(options);
            Assert(!generatedSet.Contains(pwd), $"Duplicate password detected: '{pwd}'. Secure RNG output should be unique.");
            generatedSet.Add(pwd);
        }

        Console.WriteLine("  - Test_GeneratePassword_CryptographicallySecureRandomDistribution passed.");
    }

    private static void Test_PasswordGeneratorViewModel_CommandsAndBindings()
    {
        var service = new PasswordGeneratorService();
        var vm = new PasswordGeneratorViewModel(service);

        Assert(!string.IsNullOrEmpty(vm.GeneratedPassword), "ViewModel should generate initial password upon construction.");
        Assert(!vm.HasError, "Initial ViewModel state should be error-free.");

        // Modify length
        vm.Length = 24;
        Assert(vm.GeneratedPassword.Length == 24, $"Expected GeneratedPassword length 24, got {vm.GeneratedPassword.Length}");

        // Disable all sets -> expect error
        vm.IncludeUppercase = false;
        vm.IncludeLowercase = false;
        vm.IncludeNumbers = false;
        vm.IncludeSymbols = false;

        Assert(vm.HasError, "ViewModel should report error when all character sets are disabled.");
        Assert(vm.GeneratedPassword == string.Empty, "GeneratedPassword should be empty when in error state.");

        // Re-enable numbers
        vm.IncludeNumbers = true;
        Assert(!vm.HasError, "ViewModel should clear error when valid options are restored.");
        Assert(vm.GeneratedPassword.Length == 24, "GeneratedPassword should be restored after valid options set.");

        Console.WriteLine("  - Test_PasswordGeneratorViewModel_CommandsAndBindings passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"[Assertion Failed] {message}");
        }
    }

    private static void AssertThrows<TEx>(Action action) where TEx : Exception
    {
        try
        {
            action();
            throw new Exception($"[Assertion Failed] Expected exception of type {typeof(TEx).Name}, but no exception was thrown.");
        }
        catch (TEx)
        {
            // Expected
        }
        catch (Exception ex)
        {
            throw new Exception($"[Assertion Failed] Expected exception of type {typeof(TEx).Name}, but got {ex.GetType().Name}: {ex.Message}");
        }
    }
}
