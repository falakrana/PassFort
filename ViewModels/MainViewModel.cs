using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// Main window ViewModel coordinating password entries CRUD, selection, search & category filtering, and vault state.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IPasswordService _passwordService;
    private readonly IAuthenticationService _authService;

    private string _title = "Secure Password Manager — Vault";
    private string _statusMessage = "Ready";
    private PasswordEntry? _selectedEntry;
    private PasswordEntry? _editingEntry;
    private bool _isEditing;
    private bool _isAdding;
    private bool _isPasswordVisible;
    private string? _validationMessage;

    private string _searchText = string.Empty;
    private string _selectedCategoryFilter = "All";
    private readonly ICollectionView _filteredEntries;

    public MainViewModel(IPasswordService passwordService, IAuthenticationService authService)
    {
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        LoginViewModel = new LoginViewModel(_authService);
        LoginViewModel.Authenticated += OnAuthenticated;

        _authService.LockStateChanged += OnLockStateChanged;

        PasswordEntries = new ObservableCollection<PasswordEntry>();
        _filteredEntries = CollectionViewSource.GetDefaultView(PasswordEntries);
        _filteredEntries.Filter = FilterPasswordEntry;

        AddNewCommand = new RelayCommand(ExecuteAddNew, CanExecuteAddNew);
        EditCommand = new RelayCommand(ExecuteEdit, CanExecuteEdit);
        SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
        CancelCommand = new RelayCommand(ExecuteCancel, CanExecuteCancel);
        DeleteCommand = new RelayCommand(ExecuteDelete, CanExecuteDelete);
        TogglePasswordVisibilityCommand = new RelayCommand(ExecuteTogglePasswordVisibility, CanExecuteTogglePasswordVisibility);
        LockCommand = new RelayCommand(ExecuteLock, CanExecuteLock);
        ClearSearchCommand = new RelayCommand(ExecuteClearSearch, CanExecuteClearSearch);

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
        new AuthenticationService(new FileVaultStorage(), new AesGcmEncryptionService()))
    {
    }

    public LoginViewModel LoginViewModel { get; }

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

    public ObservableCollection<PasswordEntry> PasswordEntries { get; }

    public ICollectionView FilteredEntries => _filteredEntries;

    public List<string> CategoriesFilterList => Category.FilterCategories;

    public List<string> CategoriesList => Category.StandardCategories;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _filteredEntries.Refresh();
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

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ICommand AddNewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public void LoadEntries()
    {
        PasswordEntries.Clear();
        var entries = _passwordService.GetAll();
        foreach (var entry in entries)
        {
            PasswordEntries.Add(entry);
        }

        _filteredEntries.Refresh();

        if (_filteredEntries.Cast<PasswordEntry>().Any() && SelectedEntry == null)
        {
            SelectedEntry = _filteredEntries.Cast<PasswordEntry>().First();
        }
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
        OnPropertyChanged(nameof(IsVaultUnlocked));
        if (!IsVaultUnlocked)
        {
            SelectedEntry = null;
            EditingEntry = null;
            IsAdding = false;
            IsEditing = false;
            IsPasswordVisible = false;
            SearchText = string.Empty;
            SelectedCategoryFilter = "All";
            PasswordEntries.Clear();
            _filteredEntries.Refresh();
            LoginViewModel.RefreshState();
            StatusMessage = "Vault locked.";
        }
    }

    private void ExecuteLock()
    {
        _authService.Lock();
    }

    private bool CanExecuteLock() => IsVaultUnlocked;

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
        IsEditing = true;
        IsAdding = false;
        StatusMessage = $"Editing '{SelectedEntry.Title}'...";
    }

    private bool CanExecuteEdit() => IsVaultUnlocked && SelectedEntry != null && !IsAdding && !IsEditing;

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

    private bool CanExecuteSave() => IsVaultUnlocked && (IsAdding || IsEditing) && EditingEntry != null;

    private void ExecuteCancel()
    {
        IsAdding = false;
        IsEditing = false;
        EditingEntry = null;
        ValidationMessage = null;
        StatusMessage = SelectedEntry != null ? $"Selected: {SelectedEntry.Title}" : "Ready";
    }

    private bool CanExecuteCancel() => IsVaultUnlocked && (IsAdding || IsEditing);

    private void ExecuteDelete()
    {
        if (SelectedEntry == null) return;

        var deletedTitle = SelectedEntry.Title;
        _passwordService.Delete(SelectedEntry.Id);

        StatusMessage = $"Deleted entry: '{deletedTitle}'";
        SelectedEntry = null;

        LoadEntries();
    }

    private bool CanExecuteDelete() => IsVaultUnlocked && SelectedEntry != null && !IsAdding && !IsEditing;

    private void ExecuteTogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    private bool CanExecuteTogglePasswordVisibility() => IsVaultUnlocked && (SelectedEntry != null || EditingEntry != null);

    private void InvalidateCommandStates()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

