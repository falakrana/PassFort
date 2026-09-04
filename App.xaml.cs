using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;
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

        // Domain Services
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IPasswordService, EncryptedPasswordService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>(sp => new MainWindow
        {
            DataContext = sp.GetRequiredService<MainViewModel>()
        });
    }
}
