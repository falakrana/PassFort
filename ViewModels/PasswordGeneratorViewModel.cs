using System;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.Services.Clipboard;
using PasswordManager.Services.PasswordGenerator;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.ViewModels;

/// <summary>
/// ViewModel managing password generator settings, validation, and generation commands.
/// </summary>
public class PasswordGeneratorViewModel : ViewModelBase
{
    private readonly IPasswordGeneratorService _generatorService;
    private readonly IClipboardService _clipboardService;

    private int _length = 16;
    private bool _includeUppercase = true;
    private bool _includeLowercase = true;
    private bool _includeNumbers = true;
    private bool _includeSymbols = true;
    private string _generatedPassword = string.Empty;
    private string? _validationMessage;

    public PasswordGeneratorViewModel(IPasswordGeneratorService generatorService, IClipboardService? clipboardService = null)
    {
        _generatorService = generatorService ?? throw new ArgumentNullException(nameof(generatorService));
        _clipboardService = clipboardService ?? new ClipboardService();

        GenerateCommand = new RelayCommand(ExecuteGenerate, CanExecuteGenerate);
        CopyCommand = new RelayCommand(ExecuteCopy, CanExecuteCopy);

        GeneratePassword();
    }

    /// <summary>
    /// Parameterless constructor for design-time support.
    /// </summary>
    public PasswordGeneratorViewModel() : this(new PasswordGeneratorService(), new ClipboardService())
    {
    }

    public event Action<string>? PasswordGeneratedAndSelected;

    public int Length
    {
        get => _length;
        set
        {
            if (SetProperty(ref _length, value))
            {
                OnOptionsChanged();
            }
        }
    }

    public bool IncludeUppercase
    {
        get => _includeUppercase;
        set
        {
            if (SetProperty(ref _includeUppercase, value))
            {
                OnOptionsChanged();
            }
        }
    }

    public bool IncludeLowercase
    {
        get => _includeLowercase;
        set
        {
            if (SetProperty(ref _includeLowercase, value))
            {
                OnOptionsChanged();
            }
        }
    }

    public bool IncludeNumbers
    {
        get => _includeNumbers;
        set
        {
            if (SetProperty(ref _includeNumbers, value))
            {
                OnOptionsChanged();
            }
        }
    }

    public bool IncludeSymbols
    {
        get => _includeSymbols;
        set
        {
            if (SetProperty(ref _includeSymbols, value))
            {
                OnOptionsChanged();
            }
        }
    }

    public string GeneratedPassword
    {
        get => _generatedPassword;
        private set => SetProperty(ref _generatedPassword, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ValidationMessage);

    public ICommand GenerateCommand { get; }
    public ICommand CopyCommand { get; }

    public PasswordGeneratorOptions BuildOptions()
    {
        return new PasswordGeneratorOptions
        {
            Length = Length,
            IncludeUppercase = IncludeUppercase,
            IncludeLowercase = IncludeLowercase,
            IncludeNumbers = IncludeNumbers,
            IncludeSymbols = IncludeSymbols
        };
    }

    public bool GeneratePassword()
    {
        var options = BuildOptions();
        if (_generatorService.ValidateOptions(options, out var error))
        {
            ValidationMessage = null;
            GeneratedPassword = _generatorService.GeneratePassword(options);
            PasswordGeneratedAndSelected?.Invoke(GeneratedPassword);
            return true;
        }
        else
        {
            ValidationMessage = error;
            GeneratedPassword = string.Empty;
            return false;
        }
    }

    private void OnOptionsChanged()
    {
        GeneratePassword();
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanExecuteGenerate()
    {
        var options = BuildOptions();
        return _generatorService.ValidateOptions(options, out _);
    }

    private void ExecuteGenerate()
    {
        GeneratePassword();
    }

    private bool CanExecuteCopy()
    {
        return !string.IsNullOrEmpty(GeneratedPassword) && !HasError;
    }

    private void ExecuteCopy()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
        {
            try
            {
                _clipboardService.CopySensitiveToClipboard(GeneratedPassword);
            }
            catch
            {
                // UI clipboard failure fallback
            }
        }
    }
}

