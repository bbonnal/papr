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
using rUI.Drawing.Core.Scene;

namespace paprUI.ViewModels;

public partial class LibraryPageViewModel : ViewModelBase, INavigationViewModel
{
    private readonly LibrarySettings _librarySettings;
    private readonly DrawPageViewModel _drawPageViewModel;
    private readonly INavigationService _navigation;
    private readonly ISceneSerializer _sceneSerializer = new JsonSceneSerializer();

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
                    var scene = TryDeserializeScene(json);
                    var commands = scene is not null
                        ? ToPreviewCommands(scene)
                        : (JsonSerializer.Deserialize<M5PaperPayload>(json)?.commands ?? []);

                    loadedTiles.Add(new LibraryCanvasTileViewModel
                    {
                        FileName = Path.GetFileName(file),
                        FullPath = file,
                        ModifiedAt = File.GetLastWriteTime(file),
                        Commands = commands,
                        Scene = scene,
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
        if (SelectedTile.Scene is not null)
            _drawPageViewModel.AddCanvasFromScene(title, SelectedTile.Scene);
        else
            _drawPageViewModel.AddCanvasFromCommands(title, SelectedTile.Commands);

        await _navigation.NavigateToAsync<DrawPageViewModel>();
    }

    private SceneDocument? TryDeserializeScene(string json)
    {
        try
        {
            var scene = _sceneSerializer.Deserialize(json);
            return scene.Shapes.Count > 0 ? scene : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<DrawingCommand> ToPreviewCommands(SceneDocument scene)
    {
        var commands = new List<DrawingCommand>();

        foreach (var shape in scene.Shapes)
        {
            switch (shape.Kind)
            {
                case "Text":
                    commands.Add(new TextCommand
                    {
                        X = (int)Math.Round(shape.PositionX),
                        Y = (int)Math.Round(shape.PositionY),
                        Content = shape.Text ?? "Text",
                        Size = Math.Max(1, (int)Math.Round((shape.FontSize ?? 16) / 16))
                    });
                    break;
                case "Line":
                {
                    var length = shape.Length ?? 0;
                    var x2 = shape.PositionX + (shape.OrientationX * length);
                    var y2 = shape.PositionY + (shape.OrientationY * length);
                    commands.Add(new LineCommand
                    {
                        X1 = (int)Math.Round(shape.PositionX),
                        Y1 = (int)Math.Round(shape.PositionY),
                        X2 = (int)Math.Round(x2),
                        Y2 = (int)Math.Round(y2)
                    });
                    break;
                }
                case "Rectangle":
                {
                    var w = shape.Width ?? 0;
                    var h = shape.Height ?? 0;
                    commands.Add(new RectCommand
                    {
                        X = (int)Math.Round(shape.PositionX - (w / 2)),
                        Y = (int)Math.Round(shape.PositionY - (h / 2)),
                        W = (int)Math.Round(w),
                        H = (int)Math.Round(h),
                        Fill = false
                    });
                    break;
                }
                case "Circle":
                    commands.Add(new CircleCommand
                    {
                        X = (int)Math.Round(shape.PositionX),
                        Y = (int)Math.Round(shape.PositionY),
                        R = (int)Math.Round(shape.Radius ?? 0),
                        Fill = false
                    });
                    break;
            }
        }

        return commands;
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

    public SceneDocument? Scene { get; set; }
}
