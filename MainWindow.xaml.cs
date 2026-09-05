using System.Windows;
using System.Windows.Input;
using PasswordManager.ViewModels;

namespace PasswordManager;

/// <summary>
/// Interaction logic for MainWindow.xaml with inactivity tracking input events.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        PreviewMouseMove += OnUserActivity;
        PreviewKeyDown += OnUserActivity;
        PreviewMouseDown += OnUserActivity;
    }

    private void OnUserActivity(object sender, InputEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RegisterUserActivity();
        }
    }
}