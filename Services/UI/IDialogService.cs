using System.Windows;

namespace PasswordManager.Services.UI;

/// <summary>
/// Service interface for displaying user confirmation and information dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Displays a confirmation dialog returning true if user accepts, false otherwise.
    /// </summary>
    bool ShowConfirmation(string title, string message);

    /// <summary>
    /// Displays an informational message dialog.
    /// </summary>
    void ShowMessage(string title, string message);
}
