using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using paprUI.Models;

namespace paprUI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly LibrarySettings _librarySettings;

    public SettingsPageViewModel(LibrarySettings librarySettings)
    {
        _librarySettings = librarySettings;
        LibraryPath = _librarySettings.LibraryPath;
    }

    [ObservableProperty]
    private string _title = "Settings";

    [ObservableProperty]
    private string _description = "Application-level settings.";

    [ObservableProperty]
    private string _libraryPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "";

    public IRelayCommand ToggleThemeCommand { get; } = new RelayCommand(() =>
    {
        var app = Application.Current;
        if (app is null)
            return;

        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    });

    public IRelayCommand SaveSettingsCommand => new RelayCommand(() =>
    {
        _librarySettings.SetLibraryPath(LibraryPath);
        LibraryPath = _librarySettings.LibraryPath;
        StatusText = "Library path updated";
    });
}
