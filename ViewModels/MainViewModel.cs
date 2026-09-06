using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.AutoLock;
using PasswordManager.Services.Clipboard;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.PasswordGenerator;
using PasswordManager.Services.UI;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// Main window ViewModel coordinating password entries CRUD, selection, search & category filtering, password generation, clipboard operations, and vault state.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IPasswordService _passwordService;
    private readonly IAuthenticationService _authService;
    private readonly IPasswordGeneratorService _generatorService;
    private readonly IClipboardService _clipboardService;
    private readonly IAutoLockService _autoLockService;
    private readonly IDialogService _dialogService;

    private string _title = "Secure Password Manager — Vault";
    private string _statusMessage = "Ready";
    private PasswordEntry? _selectedEntry;
    private PasswordEntry? _editingEntry;
    private bool _isEditing;
    private bool _isAdding;
    private bool _isPasswordVisible;
    private bool _isEditingPasswordVisible;
    private string? _validationMessage;
    private bool _isBusy;
    private string _busyMessage = "Processing...";

    private string _searchText = string.Empty;
    private string _selectedCategoryFilter = "All";
    private readonly ICollectionView _filteredEntries;
    private readonly object _entriesLock = new();

    private bool _isSettingsOpen;
    private string _currentMasterPassword = string.Empty;
    private string _newMasterPassword = string.Empty;
    private string _confirmNewMasterPassword = string.Empty;
    private string? _changePasswordErrorMessage;
    private string? _changePasswordSuccessMessage;

    public MainViewModel(
        IPasswordService passwordService,
        IAuthenticationService authService,
        IPasswordGeneratorService? generatorService = null,
        IClipboardService? clipboardService = null,
        IAutoLockService? autoLockService = null,
        IDialogService? dialogService = null)
    {
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _generatorService = generatorService ?? new PasswordGeneratorService();
        _clipboardService = clipboardService ?? new ClipboardService();
        _autoLockService = autoLockService ?? new AutoLockService(_authService);
        _dialogService = dialogService ?? new DialogService();

        LoginViewModel = new LoginViewModel(_authService);
        LoginViewModel.Authenticated += OnAuthenticated;

        PasswordGeneratorViewModel = new PasswordGeneratorViewModel(_generatorService, _clipboardService);

        _authService.LockStateChanged += OnLockStateChanged;
        _clipboardService.ClipboardCleared += OnClipboardCleared;
        _autoLockService.AutoLocked += OnAutoLocked;

        PasswordEntries = new ObservableCollection<PasswordEntry>();
        BindingOperations.EnableCollectionSynchronization(PasswordEntries, _entriesLock);
        _filteredEntries = CollectionViewSource.GetDefaultView(PasswordEntries);
        _filteredEntries.Filter = FilterPasswordEntry;

        AddNewCommand = new RelayCommand(ExecuteAddNew, CanExecuteAddNew);
        EditCommand = new RelayCommand(ExecuteEdit, CanExecuteEdit);
        SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
        CancelCommand = new RelayCommand(ExecuteCancel, CanExecuteCancel);
        DeleteCommand = new RelayCommand(ExecuteDelete, CanExecuteDelete);
        TogglePasswordVisibilityCommand = new RelayCommand(ExecuteTogglePasswordVisibility, CanExecuteTogglePasswordVisibility);
        ToggleEditingPasswordVisibilityCommand = new RelayCommand(ExecuteToggleEditingPasswordVisibility);
        LockCommand = new RelayCommand(ExecuteLock, CanExecuteLock);
        ClearSearchCommand = new RelayCommand(ExecuteClearSearch, CanExecuteClearSearch);
        GeneratePasswordForEntryCommand = new RelayCommand(ExecuteGeneratePasswordForEntry, CanExecuteGeneratePasswordForEntry);
        CopyUsernameCommand = new RelayCommand(ExecuteCopyUsername, CanExecuteCopyUsername);
        CopyPasswordCommand = new RelayCommand(ExecuteCopyPassword, CanExecuteCopyPassword);
        OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings, CanExecuteOpenSettings);
        CloseSettingsCommand = new RelayCommand(ExecuteCloseSettings);
        ChangeMasterPasswordCommand = new RelayCommand(ExecuteChangeMasterPassword, CanExecuteChangeMasterPassword);

        if (IsVaultUnlocked)
        {
            LoadEntries();
        }
    }

    /// <summary>
    /// Parameterless constructor for XAML designer support.
    /// </summary>
    public MainViewModel() : this(
        new EncryptedPasswordService(
            new AuthenticationService(new FileVaultStorage(), new AesGcmEncryptionService()),
            new AesGcmEncryptionService(),
            new FileVaultStorage()),
        new AuthenticationService(new FileVaultStorage(), new AesGcmEncryptionService()),
        new PasswordGeneratorService(),
        new ClipboardService(),
        new AutoLockService(new AuthenticationService(new FileVaultStorage(), new AesGcmEncryptionService())),
        new DialogService())
    {
    }

    public LoginViewModel LoginViewModel { get; }

    public PasswordGeneratorViewModel PasswordGeneratorViewModel { get; }

    public bool IsVaultUnlocked => _authService.IsUnlocked;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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

    public ObservableCollection<PasswordEntry> PasswordEntries { get; }

    public ICollectionView FilteredEntries => _filteredEntries;

    public List<string> CategoriesFilterList => Category.FilterCategories;

    public List<string> CategoriesList => Category.StandardCategories;

    public int TotalEntriesCount => PasswordEntries.Count;

    public int FilteredEntriesCount => FilteredEntries.Cast<PasswordEntry>().Count();

    public bool IsEmptyVault => IsVaultUnlocked && PasswordEntries.Count == 0;

    public bool IsSearchEmpty => IsVaultUnlocked && PasswordEntries.Count > 0 && FilteredEntriesCount == 0;

    public bool HasEntries => IsVaultUnlocked && FilteredEntriesCount > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _filteredEntries.Refresh();
                NotifyEmptyStateProperties();
                InvalidateCommandStates();
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                _filteredEntries.Refresh();
                NotifyEmptyStateProperties();
                InvalidateCommandStates();
            }
        }
    }

    public PasswordEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                IsPasswordVisible = false;
                if (!IsEditing && !IsAdding)
                {
                    StatusMessage = value != null ? $"Selected: {value.Title}" : "Ready";
                }
                InvalidateCommandStates();
            }
        }
    }

    public PasswordEntry? EditingEntry
    {
        get => _editingEntry;
        set => SetProperty(ref _editingEntry, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                InvalidateCommandStates();
            }
        }
    }

    public bool IsAdding
    {
        get => _isAdding;
        set
        {
            if (SetProperty(ref _isAdding, value))
            {
                InvalidateCommandStates();
            }
        }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public bool IsEditingPasswordVisible
    {
        get => _isEditingPasswordVisible;
        set => SetProperty(ref _isEditingPasswordVisible, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public string CurrentMasterPassword
    {
        get => _currentMasterPassword;
        set => SetProperty(ref _currentMasterPassword, value);
    }

    public string NewMasterPassword
    {
        get => _newMasterPassword;
        set => SetProperty(ref _newMasterPassword, value);
    }

    public string ConfirmNewMasterPassword
    {
        get => _confirmNewMasterPassword;
        set => SetProperty(ref _confirmNewMasterPassword, value);
    }

    public string? ChangePasswordErrorMessage
    {
        get => _changePasswordErrorMessage;
        set => SetProperty(ref _changePasswordErrorMessage, value);
    }

    public string? ChangePasswordSuccessMessage
    {
        get => _changePasswordSuccessMessage;
        set => SetProperty(ref _changePasswordSuccessMessage, value);
    }

    public ICommand AddNewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }
    public ICommand ToggleEditingPasswordVisibilityCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand GeneratePasswordForEntryCommand { get; }
    public ICommand CopyUsernameCommand { get; }
    public ICommand CopyPasswordCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand ChangeMasterPasswordCommand { get; }

    public void LoadEntries()
    {
        IsBusy = true;
        BusyMessage = "Loading vault entries...";
        try
        {
            PasswordEntries.Clear();
            var entries = _passwordService.GetAll();
            foreach (var entry in entries)
            {
                PasswordEntries.Add(entry);
            }

            _filteredEntries.Refresh();
            NotifyEmptyStateProperties();

            if (_filteredEntries.Cast<PasswordEntry>().Any() && SelectedEntry == null)
            {
                SelectedEntry = _filteredEntries.Cast<PasswordEntry>().First();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyEmptyStateProperties()
    {
        OnPropertyChanged(nameof(TotalEntriesCount));
        OnPropertyChanged(nameof(FilteredEntriesCount));
        OnPropertyChanged(nameof(IsEmptyVault));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(HasEntries));
    }

    private bool FilterPasswordEntry(object item)
    {
        if (item is not PasswordEntry entry) return false;

        // Category filter check
        if (!string.IsNullOrEmpty(SelectedCategoryFilter) && !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(entry.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Search text check (title, username, website url, category)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            bool matchesTitle = entry.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
            bool matchesUsername = entry.Username?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
            bool matchesWebsite = entry.WebsiteUrl?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
            bool matchesCategory = entry.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;

            if (!matchesTitle && !matchesUsername && !matchesWebsite && !matchesCategory)
            {
                return false;
            }
        }

        return true;
    }

    private void ExecuteClearSearch()
    {
        SearchText = string.Empty;
        SelectedCategoryFilter = "All";
    }

    private bool CanExecuteClearSearch()
    {
        return !string.IsNullOrEmpty(SearchText) || (!string.IsNullOrEmpty(SelectedCategoryFilter) && !SelectedCategoryFilter.Equals("All", StringComparison.OrdinalIgnoreCase));
    }

    private void OnAuthenticated()
    {
        OnPropertyChanged(nameof(IsVaultUnlocked));
        LoadEntries();
        StatusMessage = "Vault unlocked successfully.";
    }

    private void OnLockStateChanged()
    {
        void ClearState()
        {
            OnPropertyChanged(nameof(IsVaultUnlocked));
            if (!IsVaultUnlocked)
            {
                _clipboardService.ClearClipboard();
                _selectedEntry = null;
                OnPropertyChanged(nameof(SelectedEntry));
                EditingEntry = null;
                IsAdding = false;
                IsEditing = false;
                IsPasswordVisible = false;
                IsEditingPasswordVisible = false;
                IsSettingsOpen = false;
                CurrentMasterPassword = string.Empty;
                NewMasterPassword = string.Empty;
                ConfirmNewMasterPassword = string.Empty;
                ChangePasswordErrorMessage = null;
                ChangePasswordSuccessMessage = null;
                SearchText = string.Empty;
                SelectedCategoryFilter = "All";
                lock (_entriesLock)
                {
                    PasswordEntries.Clear();
                }
                _filteredEntries.Refresh();
                NotifyEmptyStateProperties();
                LoginViewModel.RefreshState();
                StatusMessage = "Vault locked.";
            }
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ClearState);
        }
        else
        {
            ClearState();
        }
    }

    private bool CanExecuteOpenSettings() => IsVaultUnlocked;

    private void ExecuteOpenSettings()
    {
        CurrentMasterPassword = string.Empty;
        NewMasterPassword = string.Empty;
        ConfirmNewMasterPassword = string.Empty;
        ChangePasswordErrorMessage = null;
        ChangePasswordSuccessMessage = null;
        IsSettingsOpen = true;
    }

    private void ExecuteCloseSettings()
    {
        CurrentMasterPassword = string.Empty;
        NewMasterPassword = string.Empty;
        ConfirmNewMasterPassword = string.Empty;
        ChangePasswordErrorMessage = null;
        ChangePasswordSuccessMessage = null;
        IsSettingsOpen = false;
    }

    private bool CanExecuteChangeMasterPassword() => IsVaultUnlocked && IsSettingsOpen;

    private void ExecuteChangeMasterPassword()
    {
        ChangePasswordErrorMessage = null;
        ChangePasswordSuccessMessage = null;

        if (_authService.ChangeMasterPassword(CurrentMasterPassword, NewMasterPassword, ConfirmNewMasterPassword, out string? error))
        {
            CurrentMasterPassword = string.Empty;
            NewMasterPassword = string.Empty;
            ConfirmNewMasterPassword = string.Empty;
            ChangePasswordSuccessMessage = "Master password changed successfully!";
            StatusMessage = "Master password changed successfully.";
        }
        else
        {
            ChangePasswordErrorMessage = error;
        }
    }

    private void OnClipboardCleared(string clearedText)
    {
        if (IsVaultUnlocked)
        {
            StatusMessage = "Copied password automatically cleared from clipboard.";
        }
    }

    private void OnAutoLocked(object? sender, EventArgs e)
    {
        if (!IsVaultUnlocked)
        {
            StatusMessage = "Vault automatically locked due to inactivity.";
        }
    }

    /// <summary>
    /// Registers user activity with the auto-lock service to reset the inactivity timer.
    /// </summary>
    public void RegisterUserActivity()
    {
        _autoLockService.RegisterActivity();
    }

    private void ExecuteLock()
    {
        _authService.Lock();
    }

    private bool CanExecuteLock() => IsVaultUnlocked;

    private bool CanExecuteCopyUsername() => IsVaultUnlocked && SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.Username);

    private void ExecuteCopyUsername()
    {
        if (SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.Username))
        {
            _clipboardService.CopyToClipboard(SelectedEntry.Username);
            StatusMessage = "Username copied to clipboard.";
        }
    }

    private bool CanExecuteCopyPassword() => IsVaultUnlocked && SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.Password);

    private void ExecuteCopyPassword()
    {
        if (SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.Password))
        {
            var timeoutSeconds = (int)_clipboardService.DefaultTimeout.TotalSeconds;
            _clipboardService.CopySensitiveToClipboard(SelectedEntry.Password);
            StatusMessage = $"Password copied to clipboard (auto-clears in {timeoutSeconds}s).";
        }
    }

    private void ExecuteAddNew()
    {
        EditingEntry = new PasswordEntry
        {
            Title = string.Empty,
            Username = string.Empty,
            Password = string.Empty,
            Category = "General"
        };
        ValidationMessage = null;
        IsEditingPasswordVisible = false;
        IsAdding = true;
        IsEditing = false;
        StatusMessage = "Adding new password entry...";
    }

    private bool CanExecuteAddNew() => IsVaultUnlocked && !IsAdding && !IsEditing;

    private void ExecuteEdit()
    {
        if (SelectedEntry == null) return;

        EditingEntry = SelectedEntry.Clone();
        ValidationMessage = null;
        IsEditingPasswordVisible = false;
        IsEditing = true;
        IsAdding = false;
        StatusMessage = $"Editing '{SelectedEntry.Title}'...";
    }

    private bool CanExecuteEdit() => IsVaultUnlocked && SelectedEntry != null && !IsAdding && !IsEditing;

    private bool CanExecuteGeneratePasswordForEntry() => IsVaultUnlocked && (IsAdding || IsEditing) && EditingEntry != null;

    private void ExecuteGeneratePasswordForEntry()
    {
        if (EditingEntry == null) return;

        if (PasswordGeneratorViewModel.GeneratePassword())
        {
            EditingEntry.Password = PasswordGeneratorViewModel.GeneratedPassword;
            StatusMessage = "Generated new secure password.";
        }
        else
        {
            StatusMessage = PasswordGeneratorViewModel.ValidationMessage ?? "Failed to generate password.";
        }
    }

    private void ExecuteSave()
    {
        if (EditingEntry == null) return;

        // Validation
        if (string.IsNullOrWhiteSpace(EditingEntry.Title))
        {
            ValidationMessage = "Title is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditingEntry.Password))
        {
            ValidationMessage = "Password is required.";
            return;
        }

        ValidationMessage = null;
        IsBusy = true;
        BusyMessage = "Saving entry...";

        try
        {
            if (IsAdding)
            {
                _passwordService.Add(EditingEntry);
                StatusMessage = $"Added new entry: '{EditingEntry.Title}'";
            }
            else if (IsEditing)
            {
                _passwordService.Update(EditingEntry);
                StatusMessage = $"Updated entry: '{EditingEntry.Title}'";
            }

            var savedId = EditingEntry.Id;
            IsAdding = false;
            IsEditing = false;
            EditingEntry = null;

            LoadEntries();

            SelectedEntry = PasswordEntries.FirstOrDefault(e => e.Id == savedId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecuteSave() => IsVaultUnlocked && (IsAdding || IsEditing) && EditingEntry != null;

    private void ExecuteCancel()
    {
        IsAdding = false;
        IsEditing = false;
        EditingEntry = null;
        ValidationMessage = null;
        IsEditingPasswordVisible = false;
        StatusMessage = SelectedEntry != null ? $"Selected: {SelectedEntry.Title}" : "Ready";
    }

    private bool CanExecuteCancel() => IsVaultUnlocked && (IsAdding || IsEditing);

    private void ExecuteDelete()
    {
        if (SelectedEntry == null) return;

        var confirmed = _dialogService.ShowConfirmation(
            "Delete Password Entry",
            $"Are you sure you want to permanently delete '{SelectedEntry.Title}'?");

        if (!confirmed) return;

        IsBusy = true;
        BusyMessage = "Deleting entry...";
        try
        {
            var deletedTitle = SelectedEntry.Title;
            _passwordService.Delete(SelectedEntry.Id);

            StatusMessage = $"Deleted entry: '{deletedTitle}'";
            SelectedEntry = null;

            LoadEntries();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecuteDelete() => IsVaultUnlocked && SelectedEntry != null && !IsAdding && !IsEditing;

    private void ExecuteTogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    private bool CanExecuteTogglePasswordVisibility() => IsVaultUnlocked && (SelectedEntry != null || EditingEntry != null);

    private void ExecuteToggleEditingPasswordVisibility()
    {
        IsEditingPasswordVisible = !IsEditingPasswordVisible;
    }

    private void InvalidateCommandStates()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
