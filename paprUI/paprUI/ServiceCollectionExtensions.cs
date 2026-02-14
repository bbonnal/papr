using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using paprUI.Models;
using paprUI.Services.Logging;
using paprUI.ViewModels;
using paprUI.Views;
using rUI.Avalonia.Desktop;
using rUI.Avalonia.Desktop.Services;
using rUI.Avalonia.Desktop.Services.Shortcuts;

namespace paprUI;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection services)
    {
        ConfigureLogging(services);

        _ = services.AddSingleton<SerialService>();
        _ = services.AddSingleton<LibrarySettings>();
        _ = services.AddSingleton<SceneImagePipeline>();
        _ = services.AddSingleton<DeviceScenePayloadBuilder>();

        _ = services.AddSingleton<INavigationViewModelResolver, ServiceProviderNavigationViewModelResolver>();
        _ = services.AddSingleton<NavigationService>();
        _ = services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
        _ = services.AddSingleton<IInfoBarService, InfoBarService>();
        _ = services.AddSingleton<IOverlayService, OverlayService>();
        _ = services.AddSingleton<IShortcutService, ShortcutService>();

        _ = services.AddSingleton<MainWindow>();
        _ = services.AddSingleton<MainWindowViewModel>();
        _ = services.AddSingleton<DrawPageViewModel>();

        _ = services.AddTransient<LibraryPageViewModel>();
        _ = services.AddTransient<SettingsPageViewModel>();
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        _ = services.AddSingleton<AppLogWriter>();
        _ = services.AddSingleton<ILoggerFactory>(sp =>
        {
            var writer = sp.GetRequiredService<AppLogWriter>();
            return LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddProvider(new AppFileLoggerProvider(writer, LogLevel.Debug));
            });
        });
        _ = services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
    }
}
