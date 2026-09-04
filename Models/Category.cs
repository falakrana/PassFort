using System.Collections.Generic;

namespace PasswordManager.Models;

/// <summary>
/// Represents a password category with display metadata and standard system categories.
/// </summary>
public class Category
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";

    /// <summary>
    /// Predefined list of standard category names.
    /// </summary>
    public static readonly List<string> StandardCategories = new()
    {
        "General",
        "Social",
        "Work",
        "Development",
        "Finance",
        "Personal"
    };

    /// <summary>
    /// Category filter options including "All".
    /// </summary>
    public static readonly List<string> FilterCategories = new()
    {
        "All",
        "General",
        "Social",
        "Work",
        "Development",
        "Finance",
        "Personal"
    };
}
