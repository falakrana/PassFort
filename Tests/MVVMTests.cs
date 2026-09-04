using System.ComponentModel;
using System.Windows.Input;
using PasswordManager.Commands;
using PasswordManager.ViewModels.Base;

namespace PasswordManager.Tests;

/// <summary>
/// Main test runner verifying MVVM primitives, bindings, commands, and Phase 3 Password CRUD.
/// </summary>
public static class MVVMTests
{
    public static void RunAllTests()
    {
        TestViewModelBasePropertyChanged();
        TestRelayCommandExecution();
        TestRelayCommandCanExecute();
        Console.WriteLine("[Tests] All Phase 2 MVVM tests passed successfully!");

        PasswordCRUDTests.RunAllTests();
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
