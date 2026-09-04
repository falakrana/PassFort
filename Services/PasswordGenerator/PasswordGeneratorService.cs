using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Services.PasswordGenerator;

/// <summary>
/// Cryptographically secure password generator using System.Security.Cryptography.RandomNumberGenerator.
/// </summary>
public class PasswordGeneratorService : IPasswordGeneratorService
{
    public const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    public const string NumberChars = "0123456789";
    public const string SymbolChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    public const int MinLength = 4;
    public const int MaxLength = 128;

    public bool ValidateOptions(PasswordGeneratorOptions options, out string? errorMessage)
    {
        if (options == null)
        {
            errorMessage = "Options cannot be null.";
            return false;
        }

        if (options.Length < MinLength || options.Length > MaxLength)
        {
            errorMessage = $"Password length must be between {MinLength} and {MaxLength} characters.";
            return false;
        }

        if (!options.IncludeUppercase &&
            !options.IncludeLowercase &&
            !options.IncludeNumbers &&
            !options.IncludeSymbols)
        {
            errorMessage = "At least one character set (Uppercase, Lowercase, Numbers, or Symbols) must be selected.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public string GeneratePassword(PasswordGeneratorOptions options)
    {
        if (!ValidateOptions(options, out var errorMessage))
        {
            throw new ArgumentException(errorMessage ?? "Invalid generator options.", nameof(options));
        }

        List<string> selectedSets = new();
        StringBuilder poolBuilder = new();

        if (options.IncludeUppercase)
        {
            selectedSets.Add(UppercaseChars);
            poolBuilder.Append(UppercaseChars);
        }

        if (options.IncludeLowercase)
        {
            selectedSets.Add(LowercaseChars);
            poolBuilder.Append(LowercaseChars);
        }

        if (options.IncludeNumbers)
        {
            selectedSets.Add(NumberChars);
            poolBuilder.Append(NumberChars);
        }

        if (options.IncludeSymbols)
        {
            selectedSets.Add(SymbolChars);
            poolBuilder.Append(SymbolChars);
        }

        string fullPool = poolBuilder.ToString();
        char[] passwordChars = new char[options.Length];
        int charIndex = 0;

        // Step 1: Ensure at least one character from each selected character set is included
        foreach (var charSet in selectedSets)
        {
            if (charIndex < options.Length)
            {
                int randomIdx = RandomNumberGenerator.GetInt32(0, charSet.Length);
                passwordChars[charIndex++] = charSet[randomIdx];
            }
        }

        // Step 2: Fill remaining slots with random characters from the combined pool
        while (charIndex < options.Length)
        {
            int randomIdx = RandomNumberGenerator.GetInt32(0, fullPool.Length);
            passwordChars[charIndex++] = fullPool[randomIdx];
        }

        // Step 3: Secure Fisher-Yates shuffle using RandomNumberGenerator
        for (int i = passwordChars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(0, i + 1);
            (passwordChars[i], passwordChars[j]) = (passwordChars[j], passwordChars[i]);
        }

        return new string(passwordChars);
    }
}
