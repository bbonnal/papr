using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using paprUI.ViewModels;
using paprUI.Views;

namespace paprUI;

public partial class App : Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _logger;
    public static IServiceProvider? Services => (Current as App)?._services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var servicesCollection = new ServiceCollection();
        servicesCollection.AddCommonServices();
        _services = servicesCollection.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();
        RegisterGlobalExceptionLogging();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) =>
            {
                _logger?.LogInformation("Application exiting.");
                _services?.Dispose();
                _services = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterGlobalExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            _logger?.LogCritical(exception, "Unhandled exception. IsTerminating={IsTerminating}", args.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }
}
