using System.Collections.ObjectModel;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Models;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// Main window ViewModel coordinating password entries CRUD, selection, draft editing, and vault state.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IPasswordService _passwordService;

    private string _title = "Secure Password Manager — Vault";
    private string _statusMessage = "Ready";
    private PasswordEntry? _selectedEntry;
    private PasswordEntry? _editingEntry;
    private bool _isEditing;
    private bool _isAdding;
    private bool _isPasswordVisible;
    private string? _validationMessage;

    public MainViewModel(IPasswordService passwordService)
    {
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));

        PasswordEntries = new ObservableCollection<PasswordEntry>();

        AddNewCommand = new RelayCommand(ExecuteAddNew, CanExecuteAddNew);
        EditCommand = new RelayCommand(ExecuteEdit, CanExecuteEdit);
        SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
        CancelCommand = new RelayCommand(ExecuteCancel, CanExecuteCancel);
        DeleteCommand = new RelayCommand(ExecuteDelete, CanExecuteDelete);
        TogglePasswordVisibilityCommand = new RelayCommand(ExecuteTogglePasswordVisibility, CanExecuteTogglePasswordVisibility);

        LoadEntries();
    }

    /// <summary>
    /// Parameterless constructor for XAML designer support.
    /// </summary>
    public MainViewModel() : this(new InMemoryPasswordService())
    {
    }

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

    public void LoadEntries()
    {
        PasswordEntries.Clear();
        var entries = _passwordService.GetAll();
        foreach (var entry in entries)
        {
            PasswordEntries.Add(entry);
        }

        if (PasswordEntries.Any() && SelectedEntry == null)
        {
            SelectedEntry = PasswordEntries.First();
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
        IsAdding = true;
        IsEditing = false;
        StatusMessage = "Adding new password entry...";
    }

    private bool CanExecuteAddNew() => !IsAdding && !IsEditing;

    private void ExecuteEdit()
    {
        if (SelectedEntry == null) return;

        EditingEntry = SelectedEntry.Clone();
        ValidationMessage = null;
        IsEditing = true;
        IsAdding = false;
        StatusMessage = $"Editing '{SelectedEntry.Title}'...";
    }

    private bool CanExecuteEdit() => SelectedEntry != null && !IsAdding && !IsEditing;

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

    private bool CanExecuteSave() => (IsAdding || IsEditing) && EditingEntry != null;

    private void ExecuteCancel()
    {
        IsAdding = false;
        IsEditing = false;
        EditingEntry = null;
        ValidationMessage = null;
        StatusMessage = SelectedEntry != null ? $"Selected: {SelectedEntry.Title}" : "Ready";
    }

    private bool CanExecuteCancel() => IsAdding || IsEditing;

    private void ExecuteDelete()
    {
        if (SelectedEntry == null) return;

        var deletedTitle = SelectedEntry.Title;
        _passwordService.Delete(SelectedEntry.Id);

        StatusMessage = $"Deleted entry: '{deletedTitle}'";
        SelectedEntry = null;

        LoadEntries();
    }

    private bool CanExecuteDelete() => SelectedEntry != null && !IsAdding && !IsEditing;

    private void ExecuteTogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    private bool CanExecuteTogglePasswordVisibility() => SelectedEntry != null || EditingEntry != null;

    private void InvalidateCommandStates()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
