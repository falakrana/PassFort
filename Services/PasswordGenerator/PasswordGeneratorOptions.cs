namespace PasswordManager.Services.PasswordGenerator;

/// <summary>
/// Options for generating random passwords.
/// </summary>
public class PasswordGeneratorOptions
{
    public int Length { get; set; } = 16;
    public bool IncludeUppercase { get; set; } = true;
    public bool IncludeLowercase { get; set; } = true;
    public bool IncludeNumbers { get; set; } = true;
    public bool IncludeSymbols { get; set; } = true;
}
