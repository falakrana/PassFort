using System.ComponentModel;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.ViewModels;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.Tests;

/// <summary>
/// Verifies MVVM primitives, bindings, and commands.
/// </summary>
public static class MVVMTests
{
    public static void RunAllTests()
    {
        TestViewModelBasePropertyChanged();
        TestRelayCommandExecution();
        TestRelayCommandCanExecute();
        TestMainViewModelBindings();
        Console.WriteLine("[Tests] All Phase 2 MVVM tests passed successfully!");
    }

    private static void TestViewModelBasePropertyChanged()
    {
        var vm = new TestViewModel();
        bool eventFired = false;
        string? changedPropertyName = null;

        vm.PropertyChanged += (s, e) =>
        {
            eventFired = true;
            changedPropertyName = e.PropertyName;
        };

        vm.Name = "New Name";

        if (!eventFired || changedPropertyName != nameof(TestViewModel.Name))
        {
            throw new Exception("ViewModelBase PropertyChanged event failed to fire correctly.");
        }
    }

    private static void TestRelayCommandExecution()
    {
        bool executed = false;
        ICommand command = new RelayCommand(() => executed = true);

        command.Execute(null);

        if (!executed)
        {
            throw new Exception("RelayCommand failed to execute.");
        }
    }

    private static void TestRelayCommandCanExecute()
    {
        bool canExecuteState = false;
        ICommand command = new RelayCommand(() => { }, () => canExecuteState);

        if (command.CanExecute(null) != false)
        {
            throw new Exception("RelayCommand CanExecute should return false.");
        }

        canExecuteState = true;
        if (command.CanExecute(null) != true)
        {
            throw new Exception("RelayCommand CanExecute should return true.");
        }
    }

    private static void TestMainViewModelBindings()
    {
        var vm = new MainViewModel();

        if (vm.Counter != 0)
        {
            throw new Exception("MainViewModel Initial Counter should be 0.");
        }

        if (vm.ResetCounterCommand.CanExecute(null) != false)
        {
            throw new Exception("ResetCounterCommand should not be executable when Counter is 0.");
        }

        vm.IncrementCounterCommand.Execute(null);

        if (vm.Counter != 1)
        {
            throw new Exception("IncrementCounterCommand failed to increment Counter.");
        }

        if (vm.ResetCounterCommand.CanExecute(null) != true)
        {
            throw new Exception("ResetCounterCommand should be executable when Counter > 0.");
        }

        vm.ResetCounterCommand.Execute(null);

        if (vm.Counter != 0)
        {
            throw new Exception("ResetCounterCommand failed to reset Counter to 0.");
        }
    }

    private class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }
}
