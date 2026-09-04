using System;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Services.Authentication;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// ViewModel managing vault setup (first-run) and vault login/unlock functionality.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authService;

    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private string? _validationMessage;

    public event Action? Authenticated;

    public LoginViewModel(IAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        SetupCommand = new RelayCommand(ExecuteSetup, CanExecuteSetup);
        UnlockCommand = new RelayCommand(ExecuteUnlock, CanExecuteUnlock);
    }

    /// <summary>
    /// Parameterless constructor for XAML designer support.
    /// </summary>
    public LoginViewModel() : this(new AuthenticationService())
    {
    }

    public bool IsFirstRun => !_authService.IsVaultInitialized;

    public string Title => IsFirstRun ? "Create Vault Master Password" : "Unlock Password Vault";

    public string Subtitle => IsFirstRun
        ? "Choose a strong master password to secure your vault entries."
        : "Enter your master password to unlock your vault.";

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = null;
                ValidationMessage = null;
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                ErrorMessage = null;
                ValidationMessage = null;
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ICommand SetupCommand { get; }
    public ICommand UnlockCommand { get; }

    public void RefreshState()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        ValidationMessage = null;
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
    }

    private void ExecuteSetup()
    {
        if (_authService.InitializeMasterPassword(Password, ConfirmPassword, out var error))
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            ErrorMessage = null;
            ValidationMessage = null;
            Authenticated?.Invoke();
        }
        else
        {
            ErrorMessage = error;
        }
    }

    private bool CanExecuteSetup()
    {
        return IsFirstRun && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    private void ExecuteUnlock()
    {
        if (_authService.Unlock(Password, out var error))
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            ErrorMessage = null;
            ValidationMessage = null;
            Authenticated?.Invoke();
        }
        else
        {
            ErrorMessage = error;
        }
    }

    private bool CanExecuteUnlock()
    {
        return !IsFirstRun && !string.IsNullOrWhiteSpace(Password);
    }
}
