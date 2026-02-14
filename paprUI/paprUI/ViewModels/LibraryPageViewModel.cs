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
                case "Point":
                    commands.Add(new CircleCommand
                    {
                        X = (int)Math.Round(shape.PositionX),
                        Y = (int)Math.Round(shape.PositionY),
                        R = 4,
                        Fill = true
                    });
                    break;
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
                case "Image":
                {
                    var w = Math.Max(20, (int)Math.Round(shape.Width ?? 120));
                    var h = Math.Max(20, (int)Math.Round(shape.Height ?? 80));
                    var x = (int)Math.Round(shape.PositionX - (w / 2.0));
                    var y = (int)Math.Round(shape.PositionY - (h / 2.0));
                    commands.Add(new RectCommand
                    {
                        X = x,
                        Y = y,
                        W = w,
                        H = h,
                        Fill = false
                    });
                    commands.Add(new TextCommand
                    {
                        X = x + 6,
                        Y = y + 6,
                        Size = 1,
                        Content = "IMG"
                    });
                    break;
                }
                case "TextBox":
                {
                    var w = Math.Max(40, (int)Math.Round(shape.Width ?? 140));
                    var h = Math.Max(24, (int)Math.Round(shape.Height ?? 60));
                    var x = (int)Math.Round(shape.PositionX - (w / 2.0));
                    var y = (int)Math.Round(shape.PositionY - (h / 2.0));
                    commands.Add(new RectCommand
                    {
                        X = x,
                        Y = y,
                        W = w,
                        H = h,
                        Fill = false
                    });
                    commands.Add(new TextCommand
                    {
                        X = x + 6,
                        Y = y + 6,
                        Size = 1,
                        Content = string.IsNullOrWhiteSpace(shape.Text) ? "TextBox" : shape.Text
                    });
                    break;
                }
                case "Arrow":
                    AddArrowPreview(commands, shape);
                    break;
                case "CenterlineRectangle":
                    AddCenterlineRectanglePreview(commands, shape);
                    break;
                case "Referential":
                    AddReferentialPreview(commands, shape);
                    break;
                case "Dimension":
                    AddDimensionPreview(commands, shape);
                    break;
                case "AngleDimension":
                    AddArcPreview(commands, shape, "ANG");
                    break;
                case "MultilineText":
                    commands.Add(new TextCommand
                    {
                        X = (int)Math.Round(shape.PositionX),
                        Y = (int)Math.Round(shape.PositionY),
                        Size = Math.Max(1, (int)Math.Round((shape.FontSize ?? 16) / 16)),
                        Content = string.IsNullOrWhiteSpace(shape.Text)
                            ? "Multiline"
                            : shape.Text.Split('\n')[0]
                    });
                    break;
                case "Icon":
                    commands.Add(new TextCommand
                    {
                        X = (int)Math.Round(shape.PositionX),
                        Y = (int)Math.Round(shape.PositionY),
                        Size = Math.Max(1, (int)Math.Round((shape.Size ?? 24) / 16)),
                        Content = string.IsNullOrWhiteSpace(shape.IconKey) ? "ICON" : shape.IconKey
                    });
                    break;
                case "Arc":
                    AddArcPreview(commands, shape, "ARC");
                    break;
            }
        }

        return commands;
    }

    private static void AddArrowPreview(List<DrawingCommand> commands, SceneShapeDto shape)
    {
        var (sx, sy, ex, ey) = GetSegmentEndpoints(shape);
        commands.Add(new LineCommand
        {
            X1 = sx,
            Y1 = sy,
            X2 = ex,
            Y2 = ey
        });

        var dx = ex - sx;
        var dy = ey - sy;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1)
            return;

        var ux = dx / len;
        var uy = dy / len;
        var headLen = Math.Max(8, shape.HeadLength ?? 18);
        var headAngle = shape.HeadAngleRad ?? (Math.PI / 7);

        var leftX = ex - (ux * headLen * Math.Cos(headAngle)) + (uy * headLen * Math.Sin(headAngle));
        var leftY = ey - (uy * headLen * Math.Cos(headAngle)) - (ux * headLen * Math.Sin(headAngle));
        var rightX = ex - (ux * headLen * Math.Cos(headAngle)) - (uy * headLen * Math.Sin(headAngle));
        var rightY = ey - (uy * headLen * Math.Cos(headAngle)) + (ux * headLen * Math.Sin(headAngle));

        commands.Add(new LineCommand { X1 = ex, Y1 = ey, X2 = (int)Math.Round(leftX), Y2 = (int)Math.Round(leftY) });
        commands.Add(new LineCommand { X1 = ex, Y1 = ey, X2 = (int)Math.Round(rightX), Y2 = (int)Math.Round(rightY) });
    }

    private static void AddCenterlineRectanglePreview(List<DrawingCommand> commands, SceneShapeDto shape)
    {
        var (sx, sy, ex, ey) = GetSegmentEndpoints(shape);
        var ox = shape.OrientationX;
        var oy = shape.OrientationY;
        var nx = -oy;
        var ny = ox;
        var halfW = (shape.Width ?? 20) / 2.0;

        var tlx = sx + (nx * halfW);
        var tly = sy + (ny * halfW);
        var trx = ex + (nx * halfW);
        var tryy = ey + (ny * halfW);
        var blx = sx - (nx * halfW);
        var bly = sy - (ny * halfW);
        var brx = ex - (nx * halfW);
        var bry = ey - (ny * halfW);

        commands.Add(new LineCommand { X1 = (int)Math.Round(tlx), Y1 = (int)Math.Round(tly), X2 = (int)Math.Round(trx), Y2 = (int)Math.Round(tryy) });
        commands.Add(new LineCommand { X1 = (int)Math.Round(trx), Y1 = (int)Math.Round(tryy), X2 = (int)Math.Round(brx), Y2 = (int)Math.Round(bry) });
        commands.Add(new LineCommand { X1 = (int)Math.Round(brx), Y1 = (int)Math.Round(bry), X2 = (int)Math.Round(blx), Y2 = (int)Math.Round(bly) });
        commands.Add(new LineCommand { X1 = (int)Math.Round(blx), Y1 = (int)Math.Round(bly), X2 = (int)Math.Round(tlx), Y2 = (int)Math.Round(tly) });
    }

    private static void AddReferentialPreview(List<DrawingCommand> commands, SceneShapeDto shape)
    {
        var ox = shape.OrientationX;
        var oy = shape.OrientationY;
        var nx = -oy;
        var ny = ox;
        var xLen = shape.XAxisLength ?? 80;
        var yLen = shape.YAxisLength ?? 80;

        var sx = (int)Math.Round(shape.PositionX);
        var sy = (int)Math.Round(shape.PositionY);
        var xEndX = (int)Math.Round(shape.PositionX + (ox * xLen));
        var xEndY = (int)Math.Round(shape.PositionY + (oy * xLen));
        var yEndX = (int)Math.Round(shape.PositionX + (nx * yLen));
        var yEndY = (int)Math.Round(shape.PositionY + (ny * yLen));

        commands.Add(new LineCommand { X1 = sx, Y1 = sy, X2 = xEndX, Y2 = xEndY });
        commands.Add(new LineCommand { X1 = sx, Y1 = sy, X2 = yEndX, Y2 = yEndY });
    }

    private static void AddDimensionPreview(List<DrawingCommand> commands, SceneShapeDto shape)
    {
        var (sx, sy, ex, ey) = GetSegmentEndpoints(shape);
        commands.Add(new LineCommand { X1 = sx, Y1 = sy, X2 = ex, Y2 = ey });

        var ox = shape.OrientationX;
        var oy = shape.OrientationY;
        var nx = -oy;
        var ny = ox;
        var offset = shape.Offset ?? 24;
        var osx = (int)Math.Round(sx + (nx * offset));
        var osy = (int)Math.Round(sy + (ny * offset));
        var oex = (int)Math.Round(ex + (nx * offset));
        var oey = (int)Math.Round(ey + (ny * offset));

        commands.Add(new LineCommand { X1 = osx, Y1 = osy, X2 = oex, Y2 = oey });
        if (!string.IsNullOrWhiteSpace(shape.Text))
        {
            commands.Add(new TextCommand
            {
                X = (osx + oex) / 2,
                Y = (osy + oey) / 2,
                Size = 1,
                Content = shape.Text
            });
        }
    }

    private static void AddArcPreview(List<DrawingCommand> commands, SceneShapeDto shape, string fallbackLabel)
    {
        var radius = shape.Radius ?? 0;
        if (radius <= 0)
            return;

        var start = shape.StartAngleRad ?? 0;
        var sweep = shape.SweepAngleRad ?? (Math.PI / 2);
        const int segmentCount = 16;

        double? prevX = null;
        double? prevY = null;
        for (var i = 0; i <= segmentCount; i++)
        {
            var t = (double)i / segmentCount;
            var localAngle = start + (sweep * t);
            var localX = Math.Cos(localAngle) * radius;
            var localY = Math.Sin(localAngle) * radius;

            var worldX = shape.PositionX + (shape.OrientationX * localX) + (-shape.OrientationY * localY);
            var worldY = shape.PositionY + (shape.OrientationY * localX) + (shape.OrientationX * localY);

            if (prevX is not null && prevY is not null)
            {
                commands.Add(new LineCommand
                {
                    X1 = (int)Math.Round(prevX.Value),
                    Y1 = (int)Math.Round(prevY.Value),
                    X2 = (int)Math.Round(worldX),
                    Y2 = (int)Math.Round(worldY)
                });
            }

            prevX = worldX;
            prevY = worldY;
        }

        commands.Add(new TextCommand
        {
            X = (int)Math.Round(shape.PositionX),
            Y = (int)Math.Round(shape.PositionY),
            Size = 1,
            Content = string.IsNullOrWhiteSpace(shape.Text) ? fallbackLabel : shape.Text
        });
    }

    private static (int StartX, int StartY, int EndX, int EndY) GetSegmentEndpoints(SceneShapeDto shape)
    {
        var sx = (int)Math.Round(shape.PositionX);
        var sy = (int)Math.Round(shape.PositionY);
        var len = shape.Length ?? 0;
        var ex = (int)Math.Round(shape.PositionX + (shape.OrientationX * len));
        var ey = (int)Math.Round(shape.PositionY + (shape.OrientationY * len));
        return (sx, sy, ex, ey);
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
