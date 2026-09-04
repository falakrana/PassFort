namespace PasswordManager.Models;

/// <summary>
/// Represents a password entry stored in the user's password vault.
/// </summary>
public class PasswordEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }

    /// <summary>
    /// Creates a deep clone of the password entry for safe draft editing.
    /// </summary>
    public PasswordEntry Clone()
    {
        return new PasswordEntry
        {
            Id = Id,
            Title = Title,
            Username = Username,
            Password = Password,
            WebsiteUrl = WebsiteUrl,
            Category = Category,
            Notes = Notes,
            CreatedAt = CreatedAt,
            LastModifiedAt = LastModifiedAt
        };
    }
}
