using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// Main window ViewModel coordinating top-level application state, navigation, and interaction.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private string _title = "Secure Password Manager — MVVM Foundation";
    private string _statusMessage = "MVVM Foundation Ready";
    private int _counter = 0;

    public MainViewModel()
    {
        IncrementCounterCommand = new RelayCommand(ExecuteIncrementCounter);
        ResetCounterCommand = new RelayCommand(ExecuteResetCounter, CanExecuteResetCounter);
    }

    /// <summary>
    /// Window title bound in MainWindow.xaml.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Current application status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Counter value demonstrating dynamic INotifyPropertyChanged data binding.
    /// </summary>
    public int Counter
    {
        get => _counter;
        set
        {
            if (SetProperty(ref _counter, value))
            {
                StatusMessage = $"Counter updated to: {_counter}";
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Command to increment the counter.
    /// </summary>
    public ICommand IncrementCounterCommand { get; }

    /// <summary>
    /// Command to reset the counter (only executable when Counter > 0).
    /// </summary>
    public ICommand ResetCounterCommand { get; }

    private void ExecuteIncrementCounter()
    {
        Counter++;
    }

    private void ExecuteResetCounter()
    {
        Counter = 0;
        StatusMessage = "Counter reset to 0";
    }

    private bool CanExecuteResetCounter()
    {
        return Counter > 0;
    }
}
