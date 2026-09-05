using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.AutoLock;
using PasswordManager.Services.Clipboard;
using PasswordManager.Services.Encryption;
using PasswordManager.Services.PasswordGenerator;
using PasswordManager.Services.Vault;
using PasswordManager.ViewModels;

namespace PasswordManager;

/// <summary>
/// Interaction logic for App.xaml with Dependency Injection support.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the current application IServiceProvider instance.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Run automated unit test suites
        Tests.MVVMTests.RunAllTests();
        Tests.PasswordCRUDTests.RunAllTests();
        Tests.MasterPasswordTests.RunAllTests();
        Tests.Phase5EncryptionTests.RunAllTests();
        Tests.Phase6SearchAndCategoryTests.RunAllTests();
        Tests.Phase7PasswordGeneratorTests.RunAllTests();
        Tests.Phase8ClipboardTests.RunAllTests();
        Tests.Phase9AutoLockTests.RunAllTests();
        Tests.Phase11SecurityHardeningTests.RunAllTests();
        Tests.Phase12TestingTests.RunAllTests();
        Tests.Phase12IntegrationTests.RunAllTests();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        _serviceProvider = serviceCollection.BuildServiceProvider();
        Services = _serviceProvider;

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// Configures Dependency Injection container services, ViewModels, and Views.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure Services
        services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
        services.AddSingleton<IVaultStorage, FileVaultStorage>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IAutoLockService, AutoLockService>();

        // Domain Services
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IPasswordService, EncryptedPasswordService>();
        services.AddSingleton<IPasswordGeneratorService, PasswordGeneratorService>();

        // ViewModels
        services.AddTransient<PasswordGeneratorViewModel>();
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>(sp => new MainWindow
        {
            DataContext = sp.GetRequiredService<MainViewModel>()
        });
    }
}
