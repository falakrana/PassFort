namespace PasswordManager.Services.PasswordGenerator;

/// <summary>
/// Service interface for cryptographically secure password generation and validation.
/// </summary>
public interface IPasswordGeneratorService
{
    /// <summary>
    /// Generates a cryptographically secure random password matching the provided options.
    /// </summary>
    /// <param name="options">The generator settings.</param>
    /// <returns>A randomly generated password string.</returns>
    string GeneratePassword(PasswordGeneratorOptions options);

    /// <summary>
    /// Validates whether the provided generator options are valid.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <param name="errorMessage">Out parameter receiving error details if invalid.</param>
    /// <returns>True if options are valid; otherwise false.</returns>
    bool ValidateOptions(PasswordGeneratorOptions options, out string? errorMessage);
}
