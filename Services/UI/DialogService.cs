using System.Windows;

namespace PasswordManager.Services.UI;

/// <summary>
/// Standard WPF implementation of IDialogService using MessageBox.
/// </summary>
public class DialogService : IDialogService
{
    public bool ShowConfirmation(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public void ShowMessage(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
