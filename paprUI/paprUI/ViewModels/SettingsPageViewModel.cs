using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using paprUI.Models;
using rUI.Avalonia.Desktop;

namespace paprUI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase, INavigationViewModel
{
    private readonly LibrarySettings _settings;
    private readonly ILogger<SettingsPageViewModel> _logger;
    private string _selectedLanguageCode = "en";
    private string _selectedThemeMode = "System";
    private bool _hasUnsavedChanges;
    private bool _isSubscribed;
    private bool _isApplyingSettings;

    public SettingsPageViewModel(
        LibrarySettings settings,
        ILogger<SettingsPageViewModel> logger)
    {
        _settings = settings;
        _logger = logger;

        SetEnglishCommand = new RelayCommand(() => SetLanguage("en"));
        SetFrenchCommand = new RelayCommand(() => SetLanguage("fr"));
        SetThemeSystemCommand = new RelayCommand(() => SetTheme("System"));
        SetThemeLightCommand = new RelayCommand(() => SetTheme("Light"));
        SetThemeDarkCommand = new RelayCommand(() => SetTheme("Dark"));

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ResetDefaultsCommand = new AsyncRelayCommand(ResetDefaultsAsync);
        DeletePersistedSettingsCommand = new AsyncRelayCommand(DeletePersistedSettingsAsync);

        ApplySettings(_settings.CreateSnapshot());
        RefreshStatus();
    }

    public string SaveStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string CurrentLanguageDisplay
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string CurrentThemeDisplay
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    [ObservableProperty]
    private string _libraryPath = string.Empty;

    [ObservableProperty]
    private string _imageMatrixMaxWidth = "200";

    [ObservableProperty]
    private string _imageMatrixMaxHeight = "200";

    [ObservableProperty]
    private string _imageMatrixThreshold = "160";

    [ObservableProperty]
    private bool _imageMatrixInvert;

    public string? LastAction
    {
        get;
        set => SetProperty(ref field, value);
    }

    public IRelayCommand SetEnglishCommand { get; }
    public IRelayCommand SetFrenchCommand { get; }
    public IRelayCommand SetThemeSystemCommand { get; }
    public IRelayCommand SetThemeLightCommand { get; }
    public IRelayCommand SetThemeDarkCommand { get; }

    public IAsyncRelayCommand SaveSettingsCommand { get; }
    public IAsyncRelayCommand ResetDefaultsCommand { get; }
    public IAsyncRelayCommand DeletePersistedSettingsCommand { get; }

    public Task<bool> OnDisappearingAsync()
    {
        if (_isSubscribed)
        {
            _settings.Changed -= HandleSettingsChanged;
            _isSubscribed = false;
        }

        return Task.FromResult(true);
    }

    public async Task OnAppearingAsync()
    {
        if (!_isSubscribed)
        {
            _settings.Changed += HandleSettingsChanged;
            _isSubscribed = true;
        }

        try
        {
            await _settings.InitializeAsync();
            ApplySettings(_settings.CreateSnapshot());
        }
        catch (Exception ex)
        {
            LastAction = "Failed to load settings. See log file.";
            _logger.LogError(ex, "Failed during settings page appearance initialization.");
        }
    }

    private void HandleSettingsChanged(object? sender, EventArgs e)
    {
        ApplySettings(_settings.CreateSnapshot());
    }

    private void SetLanguage(string languageCode)
    {
        _selectedLanguageCode = languageCode;
        _hasUnsavedChanges = true;

        LastAction = "Language changed in preview. Click Save to persist.";
        RefreshStatus();

        _logger.LogInformation("Language preview set to {Language}", languageCode);
    }

    private void SetTheme(string themeMode)
    {
        _selectedThemeMode = NormalizeTheme(themeMode);
        ApplyThemePreview(_selectedThemeMode);
        _hasUnsavedChanges = true;

        LastAction = "Theme changed in preview. Click Save to persist.";
        RefreshStatus();

        _logger.LogInformation("Theme preview set to {Theme}", _selectedThemeMode);
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new LibrarySettingsState
            {
                LanguageCode = _selectedLanguageCode,
                ThemeMode = _selectedThemeMode,
                LibraryPath = LibraryPath,
                ImageMatrixMaxWidth = ParseOrDefault(ImageMatrixMaxWidth, _settings.ImageMatrixMaxWidth),
                ImageMatrixMaxHeight = ParseOrDefault(ImageMatrixMaxHeight, _settings.ImageMatrixMaxHeight),
                ImageMatrixThreshold = ParseOrDefault(ImageMatrixThreshold, _settings.ImageMatrixThreshold),
                ImageMatrixInvert = ImageMatrixInvert
            };

            await _settings.SaveAsync(settings);
            _hasUnsavedChanges = false;
            LastAction = "Settings saved to local persistence and applied.";
            RefreshStatus();

            _logger.LogInformation("Settings saved from settings page.");
        }
        catch (Exception ex)
        {
            LastAction = "Failed to save settings. See log file.";
            _logger.LogError(ex, "Failed to save settings from settings page.");
        }
    }

    private async Task ResetDefaultsAsync()
    {
        try
        {
            await _settings.ResetToDefaultsAsync();
            _hasUnsavedChanges = false;
            LastAction = "Defaults restored and saved.";
            RefreshStatus();

            _logger.LogInformation("Settings reset to defaults.");
        }
        catch (Exception ex)
        {
            LastAction = "Failed to reset settings. See log file.";
            _logger.LogError(ex, "Failed to reset settings to defaults.");
        }
    }

    private async Task DeletePersistedSettingsAsync()
    {
        try
        {
            await _settings.DeletePersistedAsync();
            _hasUnsavedChanges = false;
            LastAction = "Persisted settings deleted. Runtime set to defaults.";
            RefreshStatus();

            _logger.LogInformation("Persisted settings deleted.");
        }
        catch (Exception ex)
        {
            LastAction = "Failed to delete persisted settings. See log file.";
            _logger.LogError(ex, "Failed to delete persisted settings.");
        }
    }

    private void ApplySettings(LibrarySettingsState settings)
    {
        _isApplyingSettings = true;
        try
        {
            _selectedLanguageCode = NormalizeLanguage(settings.LanguageCode);
            _selectedThemeMode = NormalizeTheme(settings.ThemeMode);

            LibraryPath = settings.LibraryPath;
            ImageMatrixMaxWidth = settings.ImageMatrixMaxWidth.ToString(CultureInfo.InvariantCulture);
            ImageMatrixMaxHeight = settings.ImageMatrixMaxHeight.ToString(CultureInfo.InvariantCulture);
            ImageMatrixThreshold = settings.ImageMatrixThreshold.ToString(CultureInfo.InvariantCulture);
            ImageMatrixInvert = settings.ImageMatrixInvert;

            CurrentLanguageDisplay = $"Current language: {(_selectedLanguageCode == "fr" ? "French" : "English")}";
            CurrentThemeDisplay = $"Current theme: {_selectedThemeMode}";

            ApplyThemePreview(_selectedThemeMode);
        }
        finally
        {
            _isApplyingSettings = false;
        }

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        SaveStatus = _hasUnsavedChanges
            ? "Unsaved changes"
            : "All changes are saved";
    }

    private static string NormalizeLanguage(string value)
    {
        return string.Equals(value, "fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";
    }

    private static string NormalizeTheme(string value)
    {
        if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase))
            return "Light";

        if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase))
            return "Dark";

        return "System";
    }

    private static void ApplyThemePreview(string themeMode)
    {
        var app = Application.Current;
        if (app is null)
            return;

        app.RequestedThemeVariant = themeMode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static int ParseOrDefault(string value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    partial void OnLibraryPathChanged(string value) => MarkUnsaved();
    partial void OnImageMatrixMaxWidthChanged(string value) => MarkUnsaved();
    partial void OnImageMatrixMaxHeightChanged(string value) => MarkUnsaved();
    partial void OnImageMatrixThresholdChanged(string value) => MarkUnsaved();
    partial void OnImageMatrixInvertChanged(bool value) => MarkUnsaved();

    private void MarkUnsaved()
    {
        if (_isApplyingSettings)
            return;

        _hasUnsavedChanges = true;
        RefreshStatus();
    }
}
