using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;

namespace paprUI.Models;

public sealed record LibrarySettingsState
{
    public string LanguageCode { get; init; } = "en";
    public string ThemeMode { get; init; } = "System";
    public string LibraryPath { get; init; } = string.Empty;
    public int ImageMatrixMaxWidth { get; init; } = 200;
    public int ImageMatrixMaxHeight { get; init; } = 200;
    public int ImageMatrixThreshold { get; init; } = 160;
    public bool ImageMatrixInvert { get; init; }
}

public class LibrarySettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<LibrarySettings> _logger;

    public LibrarySettings(ILogger<LibrarySettings> logger)
    {
        _logger = logger;

        var rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "papr",
            "paprUI");

        var settingsDirectory = Path.Combine(rootDirectory, "settings");
        SettingsFilePath = Path.Combine(settingsDirectory, "paprui-settings.json");

        Current = CreateDefaults();
        Apply(Current);
    }

    public event EventHandler? Changed;

    public LibrarySettingsState Current { get; private set; }
    public bool IsInitialized { get; private set; }
    public DateTimeOffset? LastLoadedAtUtc { get; private set; }
    public DateTimeOffset? LastSavedAtUtc { get; private set; }
    public string SettingsFilePath { get; }

    public string LanguageCode { get; private set; } = "en";
    public string ThemeMode { get; private set; } = "System";
    public string LibraryPath { get; private set; } = string.Empty;
    public int ImageMatrixMaxWidth { get; private set; } = 200;
    public int ImageMatrixMaxHeight { get; private set; } = 200;
    public int ImageMatrixThreshold { get; private set; } = 160;
    public bool ImageMatrixInvert { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
            return;

        await ReloadCoreAsync(cancellationToken);
        IsInitialized = true;
        _logger.LogInformation("Settings initialized from {SettingsPath}", SettingsFilePath);
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        return ReloadCoreAsync(cancellationToken);
    }

    public async Task SaveAsync(LibrarySettingsState settings, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(settings);
        var settingsDirectory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
            Directory.CreateDirectory(settingsDirectory);

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
        LastSavedAtUtc = DateTimeOffset.UtcNow;

        Apply(normalized);
        _logger.LogInformation(
            "Settings saved. Language={Language} Theme={Theme} LibraryPath={LibraryPath} Matrix={Width}x{Height} Threshold={Threshold} Invert={Invert}",
            normalized.LanguageCode,
            normalized.ThemeMode,
            normalized.LibraryPath,
            normalized.ImageMatrixMaxWidth,
            normalized.ImageMatrixMaxHeight,
            normalized.ImageMatrixThreshold,
            normalized.ImageMatrixInvert);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = CreateDefaults();
        await SaveAsync(defaults, cancellationToken);
        _logger.LogInformation("Settings reset to defaults and persisted.");
    }

    public Task DeletePersistedAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (File.Exists(SettingsFilePath))
            File.Delete(SettingsFilePath);

        LastSavedAtUtc = null;
        Apply(CreateDefaults());
        _logger.LogInformation("Persisted settings deleted. Defaults loaded in runtime.");
        return Task.CompletedTask;
    }

    public LibrarySettingsState CreateSnapshot()
    {
        return Current;
    }

    public void SetLibraryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var updated = Current with { LibraryPath = path.Trim() };
        Apply(Normalize(updated));
    }

    public void EnsureLibraryDirectory()
    {
        Directory.CreateDirectory(LibraryPath);
    }

    public void SetImageMatrixOptions(int maxWidth, int maxHeight, int threshold, bool invert)
    {
        var updated = Current with
        {
            ImageMatrixMaxWidth = maxWidth,
            ImageMatrixMaxHeight = maxHeight,
            ImageMatrixThreshold = threshold,
            ImageMatrixInvert = invert
        };

        Apply(Normalize(updated));
    }

    private async Task ReloadCoreAsync(CancellationToken cancellationToken)
    {
        LibrarySettingsState loaded;
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken);
                loaded = JsonSerializer.Deserialize<LibrarySettingsState>(json, JsonOptions) ?? CreateDefaults();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings from {SettingsPath}. Using defaults.", SettingsFilePath);
                loaded = CreateDefaults();
            }
        }
        else
        {
            loaded = CreateDefaults();
        }

        LastLoadedAtUtc = DateTimeOffset.UtcNow;
        Apply(Normalize(loaded));
    }

    private void Apply(LibrarySettingsState settings)
    {
        Current = settings;
        LanguageCode = settings.LanguageCode;
        ThemeMode = settings.ThemeMode;
        LibraryPath = settings.LibraryPath;
        ImageMatrixMaxWidth = settings.ImageMatrixMaxWidth;
        ImageMatrixMaxHeight = settings.ImageMatrixMaxHeight;
        ImageMatrixThreshold = settings.ImageMatrixThreshold;
        ImageMatrixInvert = settings.ImageMatrixInvert;

        EnsureLibraryDirectory();
        ApplyTheme(ThemeMode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static LibrarySettingsState Normalize(LibrarySettingsState settings)
    {
        var envPath = Environment.GetEnvironmentVariable("PAPR_LIBRARY_PATH");
        var defaultPath = string.IsNullOrWhiteSpace(envPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "papr", "library")
            : envPath;

        var normalizedPath = string.IsNullOrWhiteSpace(settings.LibraryPath)
            ? defaultPath
            : settings.LibraryPath.Trim();

        var languageCode = string.IsNullOrWhiteSpace(settings.LanguageCode)
            ? "en"
            : settings.LanguageCode.Trim();

        var themeMode = string.IsNullOrWhiteSpace(settings.ThemeMode)
            ? "System"
            : settings.ThemeMode.Trim();

        return settings with
        {
            LanguageCode = languageCode,
            ThemeMode = themeMode,
            LibraryPath = normalizedPath,
            ImageMatrixMaxWidth = Math.Clamp(settings.ImageMatrixMaxWidth, 8, 960),
            ImageMatrixMaxHeight = Math.Clamp(settings.ImageMatrixMaxHeight, 8, 540),
            ImageMatrixThreshold = Math.Clamp(settings.ImageMatrixThreshold, 0, 255)
        };
    }

    private static LibrarySettingsState CreateDefaults()
    {
        return Normalize(new LibrarySettingsState());
    }

    private static void ApplyTheme(string themeMode)
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
}
