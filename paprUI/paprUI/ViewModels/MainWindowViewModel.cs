using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using paprUI.Models;
using PhosphorIconsAvalonia;
using rUI.Avalonia.Desktop;
using rUI.Avalonia.Desktop.Controls.Navigation;
using rUI.Avalonia.Desktop.Services;

namespace paprUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private bool _isInitialized;
    private readonly LibrarySettings _settings;
    private readonly ILogger<MainWindowViewModel> _logger;

    public MainWindowViewModel(
        INavigationService navigation,
        IContentDialogService dialogService,
        IOverlayService overlayService,
        IInfoBarService infoBarService,
        LibrarySettings settings,
        ILogger<MainWindowViewModel> logger)
    {
        _settings = settings;
        _logger = logger;
        Navigation = navigation;
        DialogService = dialogService;
        OverlayService = overlayService;
        InfoBarService = infoBarService;
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

        var items = new[]
        {
            new NavigationItemControl
            {
                Header = "Canvas",
                IconData = IconService.CreateGeometry(Icon.app_window, IconType.regular),
                PageViewModelType = typeof(DrawPageViewModel)
            },
            new NavigationItemControl
            {
                Header = "Library",
                IconData = IconService.CreateGeometry(Icon.squares_four, IconType.regular),
                PageViewModelType = typeof(LibraryPageViewModel)
            }
        };

        var footerItems = new[]
        {
            new NavigationItemControl
            {
                Header = "Settings",
                IconData = IconService.CreateGeometry(Icon.gear, IconType.regular),
                PageViewModelType = typeof(SettingsPageViewModel)
            }
        };

        Logo = new PathIcon
        {
            Data = Geometry.Parse("M4 4h16v16H4z M8 8h8v8H8z"),
            Width = 24,
            Height = 24,
            Foreground = new SolidColorBrush(Color.FromRgb(53, 116, 240))
        };

        Navigation.Initialize(items, footerItems);
    }

    public INavigationService Navigation { get; }
    public IContentDialogService DialogService { get; }
    public IOverlayService OverlayService { get; }
    public IInfoBarService InfoBarService { get; }
    public object Logo { get; }

    public IRelayCommand ToggleThemeCommand { get; }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _settings.InitializeAsync();
        _isInitialized = true;
        await Navigation.NavigateToAsync<DrawPageViewModel>();
        _logger.LogInformation(
            "Main window initialized. Settings loaded from {Path}. Theme={Theme} Language={Language}",
            _settings.SettingsFilePath,
            _settings.ThemeMode,
            _settings.LanguageCode);
    }

    private static void ToggleTheme()
    {
        var app = Application.Current;
        if (app is null)
            return;

        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
}
