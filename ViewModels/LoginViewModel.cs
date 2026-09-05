using System;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;
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
    private bool _isBusy;
    private string _busyMessage = "Processing...";
    private bool _isPasswordVisible;

    public event Action? Authenticated;

    public LoginViewModel(IAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        SetupCommand = new RelayCommand(ExecuteSetup, CanExecuteSetup);
        UnlockCommand = new RelayCommand(ExecuteUnlock, CanExecuteUnlock);
        TogglePasswordVisibilityCommand = new RelayCommand(ExecuteTogglePasswordVisibility);
    }

    /// <summary>
    /// Parameterless constructor for XAML designer support.
    /// </summary>
    public LoginViewModel() : this(new AuthenticationService(new FileVaultStorage(), new AesGcmEncryptionService()))
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

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public ICommand SetupCommand { get; }
    public ICommand UnlockCommand { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }

    public void RefreshState()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        ValidationMessage = null;
        IsBusy = false;
        IsPasswordVisible = false;
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
    }

    private void ExecuteSetup()
    {
        IsBusy = true;
        BusyMessage = "Setting up vault...";
        try
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
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecuteSetup()
    {
        return IsFirstRun && !IsBusy && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    private void ExecuteUnlock()
    {
        IsBusy = true;
        BusyMessage = "Unlocking vault...";
        try
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
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecuteUnlock()
    {
        return !IsFirstRun && !IsBusy && !string.IsNullOrWhiteSpace(Password);
    }

    private void ExecuteTogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }
}
