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
        ImageMatrixMaxWidth = _librarySettings.ImageMatrixMaxWidth.ToString();
        ImageMatrixMaxHeight = _librarySettings.ImageMatrixMaxHeight.ToString();
        ImageMatrixThreshold = _librarySettings.ImageMatrixThreshold.ToString();
        ImageMatrixInvert = _librarySettings.ImageMatrixInvert;
        CardClickedCommand = new RelayCommand<string?>(OnCardClicked);
        SetEnglishCommand = new RelayCommand(() => SetLanguage("English"));
        SetFrenchCommand = new RelayCommand(() => SetLanguage("French"));
    }

    [ObservableProperty]
    private string _title = "Settings";

    [ObservableProperty]
    private string _description = "Application-level settings.";

    [ObservableProperty]
    private string _libraryPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _currentLanguageDisplay = "Current language: English";

    [ObservableProperty]
    private string _imageMatrixMaxWidth = "200";

    [ObservableProperty]
    private string _imageMatrixMaxHeight = "200";

    [ObservableProperty]
    private string _imageMatrixThreshold = "160";

    [ObservableProperty]
    private bool _imageMatrixInvert;

    public IRelayCommand CardClickedCommand { get; }
    public IRelayCommand SetEnglishCommand { get; }
    public IRelayCommand SetFrenchCommand { get; }

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
        _librarySettings.SetImageMatrixOptions(
            ParseOrDefault(ImageMatrixMaxWidth, _librarySettings.ImageMatrixMaxWidth),
            ParseOrDefault(ImageMatrixMaxHeight, _librarySettings.ImageMatrixMaxHeight),
            ParseOrDefault(ImageMatrixThreshold, _librarySettings.ImageMatrixThreshold),
            ImageMatrixInvert);

        ImageMatrixMaxWidth = _librarySettings.ImageMatrixMaxWidth.ToString();
        ImageMatrixMaxHeight = _librarySettings.ImageMatrixMaxHeight.ToString();
        ImageMatrixThreshold = _librarySettings.ImageMatrixThreshold.ToString();
        ImageMatrixInvert = _librarySettings.ImageMatrixInvert;

        StatusText = "Settings updated";
    });

    private void OnCardClicked(string? section)
    {
        if (string.Equals(section, "Appearance", System.StringComparison.Ordinal))
        {
            ToggleThemeCommand.Execute(null);
            StatusText = "Theme toggled";
            return;
        }

        StatusText = string.IsNullOrWhiteSpace(section)
            ? "Card clicked"
            : $"{section} card clicked";
    }

    private void SetLanguage(string language)
    {
        CurrentLanguageDisplay = $"Current language: {language}";
        StatusText = $"Language set to {language}";
    }

    private static int ParseOrDefault(string value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
