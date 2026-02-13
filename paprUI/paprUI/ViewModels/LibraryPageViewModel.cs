using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using paprUI.Models;
using rUI.Avalonia.Desktop;

namespace paprUI.ViewModels;

public partial class LibraryPageViewModel : ViewModelBase, INavigationViewModel
{
    private readonly LibrarySettings _librarySettings;
    private readonly DrawPageViewModel _drawPageViewModel;
    private readonly INavigationService _navigation;

    public LibraryPageViewModel(
        LibrarySettings librarySettings,
        DrawPageViewModel drawPageViewModel,
        INavigationService navigation)
    {
        _librarySettings = librarySettings;
        _drawPageViewModel = drawPageViewModel;
        _navigation = navigation;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LoadSelectedToCanvasCommand = new AsyncRelayCommand(LoadSelectedToCanvasAsync);
    }

    public ObservableCollection<LibraryCanvasTileViewModel> Tiles { get; } = [];

    [ObservableProperty]
    private LibraryCanvasTileViewModel? _selectedTile;

    [ObservableProperty]
    private string _libraryPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    public bool HasSelection => SelectedTile is not null;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand LoadSelectedToCanvasCommand { get; }

    partial void OnSelectedTileChanged(LibraryCanvasTileViewModel? value)
    {
        foreach (var tile in Tiles)
            tile.IsSelected = false;

        if (value is not null)
            value.IsSelected = true;

        OnPropertyChanged(nameof(HasSelection));
    }

    public async Task<bool> OnDisappearingAsync()
    {
        await Task.CompletedTask;
        return true;
    }

    public async Task OnAppearingAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await Task.Run(() =>
        {
            _librarySettings.EnsureLibraryDirectory();
            var path = _librarySettings.LibraryPath;
            var files = Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            var loadedTiles = new List<LibraryCanvasTileViewModel>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    _ = JsonDocument.Parse(json);

                    var payload = JsonSerializer.Deserialize<M5PaperPayload>(json);
                    var commands = payload?.commands ?? [];

                    loadedTiles.Add(new LibraryCanvasTileViewModel
                    {
                        FileName = Path.GetFileName(file),
                        FullPath = file,
                        ModifiedAt = File.GetLastWriteTime(file),
                        Commands = commands,
                        PreviewAvailable = commands.Count > 0
                    });
                }
                catch
                {
                    // Ignore invalid JSON files.
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                LibraryPath = path;
                Tiles.Clear();
                foreach (var tile in loadedTiles)
                    Tiles.Add(tile);

                SelectedTile = null;
                StatusText = $"{Tiles.Count} valid JSON file(s) in library";
            });
        });
    }

    private async Task LoadSelectedToCanvasAsync()
    {
        if (SelectedTile is null)
            return;

        var title = Path.GetFileNameWithoutExtension(SelectedTile.FileName);
        _drawPageViewModel.AddCanvasFromCommands(title, SelectedTile.Commands);
        await _navigation.NavigateToAsync<DrawPageViewModel>();
    }
}

public partial class LibraryCanvasTileViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private DateTime _modifiedAt;

    [ObservableProperty]
    private List<DrawingCommand> _commands = [];

    [ObservableProperty]
    private bool _previewAvailable;

    [ObservableProperty]
    private bool _isSelected;
}
