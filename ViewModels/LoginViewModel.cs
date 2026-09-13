using System;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Recovery;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// ViewModel managing vault setup (first-run) and vault login/unlock functionality.
/// Also handles the password hint display and recovery key unlock panel.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authService;
    private readonly IRecoveryService _recoveryService;

    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private string? _validationMessage;
    private bool _isBusy;
    private string _busyMessage = "Processing...";
    private bool _isPasswordVisible;

    // Hint
    private bool _isHintVisible;

    // Recovery panel
    private bool _isRecoveryPanelOpen;
    private string _recoveryKey = string.Empty;
    private string _newPasswordAfterRecovery = string.Empty;
    private string _confirmNewPasswordAfterRecovery = string.Empty;
    private string? _recoveryErrorMessage;
    private string? _recoverySuccessMessage;

    // First-run: hint entry
    private string _hintInput = string.Empty;

    public event Action? Authenticated;

    public LoginViewModel(IAuthenticationService authService, IRecoveryService? recoveryService = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        var storage = new FileVaultStorage();
        _recoveryService = recoveryService ?? new RecoveryService(storage, new AesGcmEncryptionService(), authService);

        SetupCommand = new RelayCommand(ExecuteSetup, CanExecuteSetup);
        UnlockCommand = new RelayCommand(ExecuteUnlock, CanExecuteUnlock);
        TogglePasswordVisibilityCommand = new RelayCommand(ExecuteTogglePasswordVisibility);
        ShowHintCommand = new RelayCommand(ExecuteShowHint);
        ToggleRecoveryPanelCommand = new RelayCommand(ExecuteToggleRecoveryPanel);
        RecoverVaultCommand = new RelayCommand(ExecuteRecoverVault, CanExecuteRecoverVault);
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

    public bool HasRecoveryKey => _recoveryService.RecoveryKeyExists();

    // â”€â”€ Login fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ Hint â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Optional hint text input during first-run setup.</summary>
    public string HintInput
    {
        get => _hintInput;
        set => SetProperty(ref _hintInput, value);
    }

    /// <summary>Whether the stored hint is currently displayed on the login screen.</summary>
    public bool IsHintVisible
    {
        get => _isHintVisible;
        set => SetProperty(ref _isHintVisible, value);
    }

    /// <summary>The stored hint text (loaded on demand).</summary>
    public string? StoredHint => _authService.GetHint();

    public bool HasHint => !string.IsNullOrWhiteSpace(_authService.GetHint());

    // â”€â”€ Recovery panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public bool IsRecoveryPanelOpen
    {
        get => _isRecoveryPanelOpen;
        set => SetProperty(ref _isRecoveryPanelOpen, value);
    }

    public string RecoveryKey
    {
        get => _recoveryKey;
        set
        {
            if (SetProperty(ref _recoveryKey, value)) RecoveryErrorMessage = null;
        }
    }

    public string NewPasswordAfterRecovery
    {
        get => _newPasswordAfterRecovery;
        set
        {
            if (SetProperty(ref _newPasswordAfterRecovery, value)) RecoveryErrorMessage = null;
        }
    }

    public string ConfirmNewPasswordAfterRecovery
    {
        get => _confirmNewPasswordAfterRecovery;
        set
        {
            if (SetProperty(ref _confirmNewPasswordAfterRecovery, value)) RecoveryErrorMessage = null;
        }
    }

    public string? RecoveryErrorMessage
    {
        get => _recoveryErrorMessage;
        set => SetProperty(ref _recoveryErrorMessage, value);
    }

    public string? RecoverySuccessMessage
    {
        get => _recoverySuccessMessage;
        set => SetProperty(ref _recoverySuccessMessage, value);
    }

    // â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public ICommand SetupCommand { get; }
    public ICommand UnlockCommand { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }
    public ICommand ShowHintCommand { get; }
    public ICommand ToggleRecoveryPanelCommand { get; }
    public ICommand RecoverVaultCommand { get; }

    public void RefreshState()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        HintInput = string.Empty;
        ErrorMessage = null;
        ValidationMessage = null;
        IsBusy = false;
        IsPasswordVisible = false;
        IsHintVisible = false;
        IsRecoveryPanelOpen = false;
        RecoveryKey = string.Empty;
        NewPasswordAfterRecovery = string.Empty;
        ConfirmNewPasswordAfterRecovery = string.Empty;
        RecoveryErrorMessage = null;
        RecoverySuccessMessage = null;
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(HasHint));
        OnPropertyChanged(nameof(HasRecoveryKey));
    }

    // â”€â”€ Setup (first run) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void ExecuteSetup()
    {
        IsBusy = true;
        BusyMessage = "Setting up vault...";
        try
        {
            if (_authService.InitializeMasterPassword(Password, ConfirmPassword, out var error))
            {
                // Save optional hint
                if (!string.IsNullOrWhiteSpace(HintInput))
                    _authService.SetHint(HintInput.Trim());

                Password = string.Empty;
                ConfirmPassword = string.Empty;
                HintInput = string.Empty;
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

    // â”€â”€ Unlock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
                IsHintVisible = false;
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

    // â”€â”€ Hint â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void ExecuteShowHint()
    {
        IsHintVisible = !IsHintVisible;
        OnPropertyChanged(nameof(StoredHint));
    }

    // â”€â”€ Recovery â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void ExecuteTogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    private void ExecuteToggleRecoveryPanel()
    {
        IsRecoveryPanelOpen = !IsRecoveryPanelOpen;
        RecoveryErrorMessage = null;
        RecoverySuccessMessage = null;
    }

    private bool CanExecuteRecoverVault()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(RecoveryKey)
            && !string.IsNullOrWhiteSpace(NewPasswordAfterRecovery)
            && !string.IsNullOrWhiteSpace(ConfirmNewPasswordAfterRecovery);
    }

    private void ExecuteRecoverVault()
    {
        IsBusy = true;
        BusyMessage = "Recovering vault...";
        RecoveryErrorMessage = null;
        RecoverySuccessMessage = null;

        try
        {
            if (_recoveryService.RecoverVault(RecoveryKey, NewPasswordAfterRecovery, ConfirmNewPasswordAfterRecovery, out var error))
            {
                RecoverySuccessMessage = "Vault recovered! Please log in with your new master password.";
                IsRecoveryPanelOpen = false;
                RecoveryKey = string.Empty;
                NewPasswordAfterRecovery = string.Empty;
                ConfirmNewPasswordAfterRecovery = string.Empty;

                // Trigger fresh login flow
                RefreshState();
                OnPropertyChanged(nameof(IsFirstRun));
            }
            else
            {
                RecoveryErrorMessage = error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
