using System;
using System.IO;
using System.Linq;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels;

namespace PasswordManager.Tests;

/// <summary>
/// Unit tests for Phase 6 Search & Categories functionality.
/// </summary>
public static class Phase6SearchAndCategoryTests
{
    public static void RunAllTests()
    {
        TestCategoryModelLists();
        TestSearchByTitle();
        TestSearchByUsername();
        TestSearchByWebsite();
        TestSearchByCategory();
        TestCategoryFilter();
        TestCombinedSearchAndCategoryFilter();
        TestClearSearchCommand();
        Console.WriteLine("[Tests] All Phase 6 Search & Categories tests passed successfully!");
    }

    private static (IAuthenticationService authService, string tempFile) CreateUnlockedAuthService()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"search_test_{Guid.NewGuid():N}.dat");
        var storage = new FileVaultStorage(tempFile);
        var encryptionService = new AesGcmEncryptionService();
        var authService = new AuthenticationService(storage, encryptionService);
        authService.InitializeMasterPassword("TestMasterPassword123!", "TestMasterPassword123!", out _);
        return (authService, tempFile);
    }

    private static void CleanupTempFile(string tempFile)
    {
        if (File.Exists(tempFile))
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static void PopulateTestVault(IPasswordService service)
    {
        // Clear any initial seed sample entries
        foreach (var entry in service.GetAll().ToList())
        {
            service.Delete(entry.Id);
        }

        service.Add(new PasswordEntry { Title = "GitHub Account", Username = "octocat", Password = "Pass1", WebsiteUrl = "https://github.com", Category = "Development" });
        service.Add(new PasswordEntry { Title = "Twitter Profile", Username = "tweetmaster", Password = "Pass2", WebsiteUrl = "https://twitter.com", Category = "Social" });
        service.Add(new PasswordEntry { Title = "Corporate Email", Username = "alex@company.com", Password = "Pass3", WebsiteUrl = "https://mail.company.com", Category = "Work" });
        service.Add(new PasswordEntry { Title = "Chase Online Banking", Username = "alex_finance", Password = "Pass4", WebsiteUrl = "https://chase.com", Category = "Finance" });
        service.Add(new PasswordEntry { Title = "Personal Blog", Username = "blogger_alex", Password = "Pass5", WebsiteUrl = "https://myblog.com", Category = "Personal" });
    }

    private static void TestCategoryModelLists()
    {
        if (!Category.StandardCategories.Contains("General") ||
            !Category.StandardCategories.Contains("Social") ||
            !Category.StandardCategories.Contains("Work") ||
            !Category.StandardCategories.Contains("Development") ||
            !Category.StandardCategories.Contains("Finance") ||
            !Category.StandardCategories.Contains("Personal"))
        {
            throw new Exception("Category.StandardCategories is missing required standard categories.");
        }

        if (!Category.FilterCategories.Contains("All") || Category.FilterCategories.Count != Category.StandardCategories.Count + 1)
        {
            throw new Exception("Category.FilterCategories should include 'All' and all standard categories.");
        }
    }

    private static void TestSearchByTitle()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);
            vm.SearchText = "GitHub";

            var filtered = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (filtered.Count != 1 || filtered.First().Title != "GitHub Account")
            {
                throw new Exception($"Search by Title failed. Expected 'GitHub Account', found {filtered.Count} entries.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void TestSearchByUsername()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);
            vm.SearchText = "octocat";

            var filtered = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (filtered.Count != 1 || filtered.First().Username != "octocat")
            {
                throw new Exception($"Search by Username failed. Expected 1 result for 'octocat', found {filtered.Count}.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void TestSearchByWebsite()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);
            vm.SearchText = "chase.com";

            var filtered = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (filtered.Count != 1 || filtered.First().Title != "Chase Online Banking")
            {
                throw new Exception($"Search by Website URL failed. Expected 'Chase Online Banking', found {filtered.Count}.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void TestSearchByCategory()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);
            vm.SearchText = "Development";

            var filtered = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (filtered.Count != 1 || filtered.First().Category != "Development")
            {
                throw new Exception($"Search by Category keyword failed. Found {filtered.Count} entries.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void TestCategoryFilter()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);

            // Filter Social
            vm.SelectedCategoryFilter = "Social";
            var socialEntries = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (socialEntries.Count != 1 || socialEntries.First().Category != "Social")
            {
                throw new Exception($"Category filter 'Social' failed. Expected 1, found {socialEntries.Count}.");
            }

            // Filter Work
            vm.SelectedCategoryFilter = "Work";
            var workEntries = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (workEntries.Count != 1 || workEntries.First().Category != "Work")
            {
                throw new Exception($"Category filter 'Work' failed. Expected 1, found {workEntries.Count}.");
            }

            // Filter All
            vm.SelectedCategoryFilter = "All";
            var allEntries = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (allEntries.Count != 5)
            {
                throw new Exception($"Category filter 'All' failed. Expected 5, found {allEntries.Count}.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void TestCombinedSearchAndCategoryFilter()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);
            vm.SelectedCategoryFilter = "Work";
            vm.SearchText = "Corporate";

            var filtered = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (filtered.Count != 1 || filtered.First().Title != "Corporate Email")
            {
                throw new Exception("Combined Search + Category filter failed to return 'Corporate Email'.");
            }

            // Non-matching category with matching search query should return 0
            vm.SelectedCategoryFilter = "Finance";
            var emptyResult = vm.FilteredEntries.Cast<PasswordEntry>().ToList();
            if (emptyResult.Count != 0)
            {
                throw new Exception("Combined Search + Category filter should return 0 items when category does not match.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }

    private static void TestClearSearchCommand()
    {
        var (authService, tempFile) = CreateUnlockedAuthService();
        try
        {
            var encryptionService = new AesGcmEncryptionService();
            var storage = new FileVaultStorage(tempFile);
            IPasswordService service = new EncryptedPasswordService(authService, encryptionService, storage);
            PopulateTestVault(service);

            var vm = new MainViewModel(service, authService);
            vm.SearchText = "GitHub";
            vm.SelectedCategoryFilter = "Development";

            if (!vm.ClearSearchCommand.CanExecute(null))
            {
                throw new Exception("ClearSearchCommand should be executable when search filter is active.");
            }

            vm.ClearSearchCommand.Execute(null);

            if (!string.IsNullOrEmpty(vm.SearchText) || vm.SelectedCategoryFilter != "All")
            {
                throw new Exception("ClearSearchCommand failed to reset SearchText and SelectedCategoryFilter.");
            }

            if (vm.FilteredEntries.Cast<PasswordEntry>().Count() != 5)
            {
                throw new Exception("FilteredEntries count should return to 5 after clear search.");
            }
        }
        finally
        {
            CleanupTempFile(tempFile);
        }
    }
}
