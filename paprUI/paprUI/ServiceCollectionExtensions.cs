using Microsoft.Extensions.DependencyInjection;
using paprUI.Models;
using paprUI.ViewModels;
using paprUI.Views;
using rUI.Avalonia.Desktop;
using rUI.Avalonia.Desktop.Services;

namespace paprUI;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection services)
    {
        _ = services.AddSingleton<SerialService>();
        _ = services.AddSingleton<LibrarySettings>();

        _ = services.AddSingleton<NavigationService>();
        _ = services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
        _ = services.AddSingleton<IInfoBarService, InfoBarService>();
        _ = services.AddSingleton<IOverlayService, OverlayService>();

        _ = services.AddSingleton<MainWindow>();
        _ = services.AddSingleton<MainWindowViewModel>();
        _ = services.AddSingleton<DrawPageViewModel>();

        _ = services.AddTransient<LibraryPageViewModel>();
        _ = services.AddTransient<SettingsPageViewModel>();
    }
}
