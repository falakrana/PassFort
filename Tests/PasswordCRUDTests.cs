using PasswordManager.Models;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels;

namespace PasswordManager.Tests;

/// <summary>
/// Unit tests for PasswordEntry CRUD operations and ViewModel interaction.
/// </summary>
public static class PasswordCRUDTests
{
    public static void RunAllTests()
    {
        TestInMemoryPasswordServiceCRUD();
        TestMainViewModelCRUDCommands();
        TestMainViewModelValidation();
        Console.WriteLine("[Tests] All Phase 3 Password CRUD tests passed successfully!");
    }

    private static void TestInMemoryPasswordServiceCRUD()
    {
        IPasswordService service = new InMemoryPasswordService();

        var initialEntries = service.GetAll().ToList();
        if (initialEntries.Count != 3)
        {
            throw new Exception("Expected 3 initial seed entries in InMemoryPasswordService.");
        }

        // Add
        var newEntry = new PasswordEntry
        {
            Title = "Test Portal",
            Username = "test_user",
            Password = "TestPassword123",
            Category = "Personal"
        };
        service.Add(newEntry);

        var updatedList = service.GetAll().ToList();
        if (updatedList.Count != 4)
        {
            throw new Exception("Failed to add new entry to InMemoryPasswordService.");
        }

        var retrieved = service.GetById(newEntry.Id);
        if (retrieved == null || retrieved.Title != "Test Portal")
        {
            throw new Exception("GetById failed to return added entry.");
        }

        // Update
        retrieved.Title = "Updated Test Portal";
        service.Update(retrieved);

        var afterUpdate = service.GetById(retrieved.Id);
        if (afterUpdate == null || afterUpdate.Title != "Updated Test Portal")
        {
            throw new Exception("Update failed to modify entry title.");
        }

        // Delete
        service.Delete(retrieved.Id);
        if (service.GetAll().Count() != 3)
        {
            throw new Exception("Delete failed to remove entry from service.");
        }
    }

    private static void TestMainViewModelCRUDCommands()
    {
        IPasswordService service = new InMemoryPasswordService();
        var vm = new MainViewModel(service);

        // Initial selection
        if (vm.SelectedEntry == null)
        {
            throw new Exception("MainViewModel should automatically select the first entry on load.");
        }

        // Add New
        vm.AddNewCommand.Execute(null);
        if (!vm.IsAdding || vm.EditingEntry == null)
        {
            throw new Exception("AddNewCommand failed to transition into IsAdding mode.");
        }

        vm.EditingEntry.Title = "New Bank Password";
        vm.EditingEntry.Username = "bank_user";
        vm.EditingEntry.Password = "BankPass789!";
        vm.SaveCommand.Execute(null);

        if (vm.IsAdding || vm.PasswordEntries.Count != 4)
        {
            throw new Exception("SaveCommand failed to save new entry and exit adding mode.");
        }

        if (vm.SelectedEntry?.Title != "New Bank Password")
        {
            throw new Exception("SaveCommand should select the newly created entry.");
        }

        // Edit
        vm.EditCommand.Execute(null);
        if (!vm.IsEditing || vm.EditingEntry == null)
        {
            throw new Exception("EditCommand failed to transition into IsEditing mode.");
        }

        vm.EditingEntry.Title = "Updated Bank Password";
        vm.SaveCommand.Execute(null);

        if (vm.SelectedEntry?.Title != "Updated Bank Password")
        {
            throw new Exception("SaveCommand failed to update existing entry title.");
        }

        // Delete
        var entryToDelete = vm.SelectedEntry;
        vm.DeleteCommand.Execute(null);

        if (vm.PasswordEntries.Any(e => e.Id == entryToDelete.Id))
        {
            throw new Exception("DeleteCommand failed to remove selected entry.");
        }
    }

    private static void TestMainViewModelValidation()
    {
        IPasswordService service = new InMemoryPasswordService();
        var vm = new MainViewModel(service);

        vm.AddNewCommand.Execute(null);
        vm.EditingEntry!.Title = ""; // Empty title
        vm.EditingEntry.Password = "valid_password";

        vm.SaveCommand.Execute(null);

        if (vm.ValidationMessage == null || !vm.IsAdding)
        {
            throw new Exception("SaveCommand should prevent saving when Title is empty.");
        }

        vm.CancelCommand.Execute(null);
        if (vm.IsAdding || vm.ValidationMessage != null)
        {
            throw new Exception("CancelCommand should clear edit state and validation messages.");
        }
    }
}
