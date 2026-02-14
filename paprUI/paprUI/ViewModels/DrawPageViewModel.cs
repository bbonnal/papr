using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flowxel.Core.Geometry.Primitives;
using paprUI.Models;
using rUI.Avalonia.Desktop;
using rUI.Avalonia.Desktop.Services;
using rUI.Avalonia.Desktop.Services.Shortcuts;
using rUI.Drawing.Core;
using rUI.Drawing.Core.Shapes;
using rUI.Drawing.Core.Scene;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaVector = Avalonia.Vector;
using FlowCircle = Flowxel.Core.Geometry.Shapes.Circle;
using FlowRectangle = Flowxel.Core.Geometry.Shapes.Rectangle;
using FlowVector = Flowxel.Core.Geometry.Primitives.Vector;
using Line = Flowxel.Core.Geometry.Shapes.Line;
using Shape = Flowxel.Core.Geometry.Shapes.Shape;

namespace paprUI.ViewModels;

public partial class DrawPageViewModel : ViewModelBase, IShortcutBindingProvider
{
    private readonly SerialService _serialService;
    private readonly LibrarySettings _librarySettings;
    private readonly SceneImagePipeline _sceneImagePipeline;
    private readonly DeviceScenePayloadBuilder _deviceScenePayloadBuilder;
    private readonly INavigationService _navigation;
    private readonly ISceneSerializer _sceneSerializer = new JsonSceneSerializer();
    private int _nextCanvasNumber = 1;

    public DrawPageViewModel(
        SerialService serialService,
        LibrarySettings librarySettings,
        SceneImagePipeline sceneImagePipeline,
        DeviceScenePayloadBuilder deviceScenePayloadBuilder,
        INavigationService navigation,
        IContentDialogService dialogService,
        IInfoBarService infoBarService)
    {
        _serialService = serialService;
        _librarySettings = librarySettings;
        _sceneImagePipeline = sceneImagePipeline;
        _deviceScenePayloadBuilder = deviceScenePayloadBuilder;
        _navigation = navigation;
        DialogService = dialogService;
        InfoBarService = infoBarService;

        AddCanvasCommand = new RelayCommand(AddCanvasCommandExecute);
        SelectToolCommand = new RelayCommand<string>(SelectToolByName);
        SaveCanvasCommand = new AsyncRelayCommand(SaveFocusedCanvasAsync);
        UploadCanvasCommand = new AsyncRelayCommand(UploadFocusedCanvasAsync);
        DeleteCanvasCommand = new RelayCommand(ClearFocusedCanvas);
        ResetFocusedViewCommand = new RelayCommand(ResetFocusedView);
        OpenLibraryCommand = new AsyncRelayCommand(OpenLibraryAsync);
        EscapeToSelectToolCommand = new RelayCommand(HandleEscapeToSelect);
        ClearSerialConsoleCommand = new RelayCommand(ClearSerialConsole);

        foreach (var entry in _serialService.GetHistorySnapshot())
            SerialEntries.Add(entry);

        _serialService.TrafficCaptured += OnSerialTrafficCaptured;
        _serialService.HistoryCleared += OnSerialHistoryCleared;

        AddCanvas();
    }

    public IContentDialogService DialogService { get; }

    public IInfoBarService InfoBarService { get; }

    public ObservableCollection<CanvasDocumentViewModel> Canvases { get; } = [];

    [ObservableProperty]
    private CanvasDocumentViewModel? _focusedCanvas;

    public bool IsSelectToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Select;
    public bool IsTextToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Text;
    public bool IsLineToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Line;
    public bool IsRectangleToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Rectangle;
    public bool IsCircleToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Circle;
    public bool IsPointToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Point;
    public bool IsArcToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Arc;
    public bool IsArrowToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Arrow;
    public bool IsTextBoxToolActive => FocusedCanvas?.ActiveTool == DrawingTool.TextBox;
    public bool IsMultilineTextToolActive => FocusedCanvas?.ActiveTool == DrawingTool.MultilineText;
    public bool IsIconToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Icon;
    public bool IsImageToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Image;
    public bool IsCenterlineRectangleToolActive => FocusedCanvas?.ActiveTool == DrawingTool.CenterlineRectangle;
    public bool IsReferentialToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Referential;
    public bool IsDimensionToolActive => FocusedCanvas?.ActiveTool == DrawingTool.Dimension;
    public bool IsAngleDimensionToolActive => FocusedCanvas?.ActiveTool == DrawingTool.AngleDimension;

    public IRelayCommand AddCanvasCommand { get; }
    public IRelayCommand<string> SelectToolCommand { get; }
    public IAsyncRelayCommand SaveCanvasCommand { get; }
    public IAsyncRelayCommand UploadCanvasCommand { get; }
    public IRelayCommand DeleteCanvasCommand { get; }
    public IRelayCommand ResetFocusedViewCommand { get; }
    public IAsyncRelayCommand OpenLibraryCommand { get; }
    public IRelayCommand EscapeToSelectToolCommand { get; }
    public IRelayCommand ClearSerialConsoleCommand { get; }
    public ObservableCollection<SerialTrafficEntry> SerialEntries { get; } = [];

    public event EventHandler? EscapeToSelectRequested;

    public string StatusText => FocusedCanvas is null
        ? $"Canvases: {Canvases.Count} | Focused: none"
        : $"Canvases: {Canvases.Count} | Focused: {FocusedCanvas.Title} | Tool: {FocusedCanvas.ActiveTool} | Shapes: {FocusedCanvas.Shapes.Count}";

    partial void OnFocusedCanvasChanged(CanvasDocumentViewModel? value)
    {
        NotifyToolSelectionChanged();
        OnPropertyChanged(nameof(StatusText));
    }

    public CanvasDocumentViewModel AddCanvas(string? title = null)
    {
        var canvas = new CanvasDocumentViewModel(title ?? $"Canvas {_nextCanvasNumber++}");
        canvas.Shapes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
        canvas.PropertyChanged += OnCanvasPropertyChanged;
        Canvases.Add(canvas);
        FocusedCanvas = canvas;
        OnPropertyChanged(nameof(StatusText));
        return canvas;
    }

    public CanvasDocumentViewModel AddCanvasFromCommands(string title, IReadOnlyCollection<DrawingCommand> commands)
    {
        var canvas = AddCanvas(title);
        foreach (var command in commands)
        {
            var shape = FromDrawingCommand(command);
            if (shape is not null)
                canvas.Shapes.Add(shape);
        }

        FocusedCanvas = canvas;
        OnPropertyChanged(nameof(StatusText));
        return canvas;
    }

    public CanvasDocumentViewModel AddCanvasFromScene(string title, SceneDocument scene)
    {
        var canvas = AddCanvas(title);
        var sceneWithMaterializedImages = _sceneImagePipeline.MaterializeEmbeddedImages(scene);
        var loaded = SceneDocumentMapper.FromDocument(sceneWithMaterializedImages);

        foreach (var shape in loaded.Shapes)
            canvas.Shapes.Add(shape);

        foreach (var id in loaded.ComputedShapeIds)
            canvas.ComputedShapeIds.Add(id);

        canvas.ShowCanvasBoundary = scene.ShowCanvasBoundary;
        canvas.CanvasBoundaryWidth = scene.CanvasBoundaryWidth;
        canvas.CanvasBoundaryHeight = scene.CanvasBoundaryHeight;

        if (Color.TryParse(scene.CanvasBackgroundColor, out var color))
            canvas.CanvasBackgroundBrush = new SolidColorBrush(color);

        FocusedCanvas = canvas;
        OnPropertyChanged(nameof(StatusText));
        return canvas;
    }

    public void FocusCanvas(Guid canvasId)
    {
        var canvas = Canvases.FirstOrDefault(x => x.Id == canvasId);
        if (canvas is null)
            return;

        FocusedCanvas = canvas;
    }

    public void RemoveCanvas(Guid canvasId)
    {
        var canvas = Canvases.FirstOrDefault(x => x.Id == canvasId);
        if (canvas is null)
            return;

        canvas.PropertyChanged -= OnCanvasPropertyChanged;
        Canvases.Remove(canvas);
        if (ReferenceEquals(FocusedCanvas, canvas))
            FocusedCanvas = Canvases.LastOrDefault();

        OnPropertyChanged(nameof(StatusText));
        NotifyToolSelectionChanged();
    }

    private void AddCanvasCommandExecute()
    {
        AddCanvas();
    }

    private void SelectToolByName(string? toolName)
    {
        if (FocusedCanvas is null || string.IsNullOrWhiteSpace(toolName))
            return;

        if (Enum.TryParse<DrawingTool>(toolName, true, out var tool))
        {
            FocusedCanvas.ActiveTool = tool;
            NotifyToolSelectionChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private async Task SaveFocusedCanvasAsync()
    {
        if (FocusedCanvas is null)
            return;

        try
        {
            _librarySettings.EnsureLibraryDirectory();
            var computed = FocusedCanvas.ComputedShapeIds.ToHashSet(StringComparer.Ordinal);
            var baseScene = SceneDocumentMapper.ToDocument(FocusedCanvas.Shapes, computed);
            var scene = new SceneDocument
            {
                Version = baseScene.Version,
                Shapes = baseScene.Shapes,
                CanvasBackgroundColor = FocusedCanvas.CanvasBackgroundBrush is ISolidColorBrush solid ? solid.Color.ToString() : "#FFFFFFFF",
                ShowCanvasBoundary = FocusedCanvas.ShowCanvasBoundary,
                CanvasBoundaryWidth = FocusedCanvas.CanvasBoundaryWidth,
                CanvasBoundaryHeight = FocusedCanvas.CanvasBoundaryHeight
            };

            var fileSafeName = FocusedCanvas.Title.Replace(' ', '_');
            var fileName = $"{fileSafeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var outputPath = Path.Combine(_librarySettings.LibraryPath, fileName);

            var serializableScene = _sceneImagePipeline.EmbedImages(scene);
            var json = _sceneSerializer.Serialize(serializableScene);
            await File.WriteAllTextAsync(outputPath, json);
            OnPropertyChanged(nameof(StatusText));
        }
        catch
        {
            // keep UI stable
        }
    }

    private async Task UploadFocusedCanvasAsync()
    {
        if (FocusedCanvas is null)
            return;

        try
        {
            if (!_serialService.IsOpen)
            {
                var ports = SerialService.GetAvailablePorts();
                if (ports.Length == 0)
                    return;

                _serialService.Connect(ports[0]);
            }

            var computed = FocusedCanvas.ComputedShapeIds.ToHashSet(StringComparer.Ordinal);
            var baseScene = SceneDocumentMapper.ToDocument(FocusedCanvas.Shapes, computed);
            var scene = new SceneDocument
            {
                Version = baseScene.Version,
                Shapes = baseScene.Shapes,
                CanvasBackgroundColor = FocusedCanvas.CanvasBackgroundBrush is ISolidColorBrush solid ? solid.Color.ToString() : "#FFFFFFFF",
                ShowCanvasBoundary = FocusedCanvas.ShowCanvasBoundary,
                CanvasBoundaryWidth = FocusedCanvas.CanvasBoundaryWidth,
                CanvasBoundaryHeight = FocusedCanvas.CanvasBoundaryHeight
            };

            var payloadJson = _deviceScenePayloadBuilder.BuildPayload(scene, _librarySettings);
            await _serialService.UploadRawJsonAsync(payloadJson);
            OnPropertyChanged(nameof(StatusText));
        }
        catch
        {
            // keep UI stable
        }
    }

    private void ClearFocusedCanvas()
    {
        if (FocusedCanvas is null)
            return;

        FocusedCanvas.Shapes.Clear();
        FocusedCanvas.ComputedShapeIds.Clear();
        OnPropertyChanged(nameof(StatusText));
    }

    private void ResetFocusedView()
    {
        if (FocusedCanvas is null)
            return;

        FocusedCanvas.Zoom = 1d;
        FocusedCanvas.Pan = default;
    }

    public IEnumerable<ShortcutDefinition> GetShortcutDefinitions()
    {
        return
        [
            new ShortcutDefinition("Ctrl+S", SaveCanvasCommand, Description: "Save canvas"),
            new ShortcutDefinition("Ctrl+O", OpenLibraryCommand, Description: "Open library"),
            new ShortcutDefinition("Ctrl+U", UploadCanvasCommand, Description: "Upload canvas"),
            new ShortcutDefinition("Ctrl+R", ResetFocusedViewCommand, Description: "Reset view"),
            new ShortcutDefinition("Ctrl+Shift+Delete", DeleteCanvasCommand, Description: "Clear focused canvas"),

            new ShortcutDefinition("Escape", EscapeToSelectToolCommand, Description: "Clear selection and select tool"),
            new ShortcutDefinition("P", SelectToolCommand, "Point", Description: "Point tool"),
            new ShortcutDefinition("L", SelectToolCommand, "Line", Description: "Line tool"),
            new ShortcutDefinition("R", SelectToolCommand, "Rectangle", Description: "Rectangle tool"),
            new ShortcutDefinition("C", SelectToolCommand, "Circle", Description: "Circle tool"),
            new ShortcutDefinition("A", SelectToolCommand, "Arc", Description: "Arc tool"),
            new ShortcutDefinition("T", SelectToolCommand, "Text", Description: "Text tool"),
            new ShortcutDefinition("M", SelectToolCommand, "MultilineText", Description: "Multiline text tool"),
            new ShortcutDefinition("I", SelectToolCommand, "Image", Description: "Image tool")
        ];
    }

    private async Task OpenLibraryAsync()
    {
        await _navigation.NavigateToAsync<LibraryPageViewModel>();
    }

    private void HandleEscapeToSelect()
    {
        SelectToolByName("Select");
        EscapeToSelectRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSerialConsole()
    {
        _serialService.ClearHistory();
    }

    private static DrawingCommand? ToDrawingCommand(Shape shape)
    {
        switch (shape)
        {
            case TextShape text:
                return new TextCommand
                {
                    X = RoundToInt(text.Pose.Position.X),
                    Y = RoundToInt(text.Pose.Position.Y),
                    Content = text.Text,
                    Size = Math.Max(1, RoundToInt(text.FontSize / 16.0))
                };
            case Line line:
                return new LineCommand
                {
                    X1 = RoundToInt(line.StartPoint.Position.X),
                    Y1 = RoundToInt(line.StartPoint.Position.Y),
                    X2 = RoundToInt(line.EndPoint.Position.X),
                    Y2 = RoundToInt(line.EndPoint.Position.Y)
                };
            case FlowRectangle rectangle:
                return new RectCommand
                {
                    X = RoundToInt(rectangle.TopLeft.Position.X),
                    Y = RoundToInt(rectangle.TopLeft.Position.Y),
                    W = Math.Max(1, RoundToInt(rectangle.Width)),
                    H = Math.Max(1, RoundToInt(rectangle.Height)),
                    Fill = false
                };
            case FlowCircle circle:
                return new CircleCommand
                {
                    X = RoundToInt(circle.Pose.Position.X),
                    Y = RoundToInt(circle.Pose.Position.Y),
                    R = Math.Max(1, RoundToInt(circle.Radius)),
                    Fill = false
                };
            default:
                return null;
        }
    }

    private static Shape? FromDrawingCommand(DrawingCommand command)
    {
        switch (command)
        {
            case TextCommand text:
                return new TextShape
                {
                    Pose = CreatePose(text.X, text.Y),
                    Text = text.Content,
                    FontSize = Math.Max(8, text.Size * 16)
                };
            case RectCommand rect:
            {
                var centerX = rect.X + (rect.W / 2.0);
                var centerY = rect.Y + (rect.H / 2.0);
                return new FlowRectangle
                {
                    Pose = CreatePose(centerX, centerY),
                    Width = Math.Max(1, rect.W),
                    Height = Math.Max(1, rect.H)
                };
            }
            case CircleCommand circle:
                return new FlowCircle
                {
                    Pose = CreatePose(circle.X, circle.Y),
                    Radius = Math.Max(1, circle.R)
                };
            case LineCommand line:
            {
                var dx = line.X2 - line.X1;
                var dy = line.Y2 - line.Y1;
                var length = Math.Sqrt((dx * dx) + (dy * dy));
                if (length <= 0.0001)
                    return null;

                var orientation = new FlowVector(dx / length, dy / length);
                return new Line
                {
                    Pose = CreatePose(line.X1, line.Y1, orientation),
                    Length = length
                };
            }
            default:
                return null;
        }
    }

    private static Pose CreatePose(double x, double y, FlowVector? orientation = null)
        => new(new FlowVector(x, y), orientation ?? new FlowVector(1, 0));

    private static int RoundToInt(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == FocusedCanvas && e.PropertyName == nameof(CanvasDocumentViewModel.ActiveTool))
            NotifyToolSelectionChanged();

        if (sender == FocusedCanvas)
            OnPropertyChanged(nameof(StatusText));
    }

    private void NotifyToolSelectionChanged()
    {
        OnPropertyChanged(nameof(IsSelectToolActive));
        OnPropertyChanged(nameof(IsTextToolActive));
        OnPropertyChanged(nameof(IsLineToolActive));
        OnPropertyChanged(nameof(IsRectangleToolActive));
        OnPropertyChanged(nameof(IsCircleToolActive));
        OnPropertyChanged(nameof(IsPointToolActive));
        OnPropertyChanged(nameof(IsArcToolActive));
        OnPropertyChanged(nameof(IsArrowToolActive));
        OnPropertyChanged(nameof(IsTextBoxToolActive));
        OnPropertyChanged(nameof(IsMultilineTextToolActive));
        OnPropertyChanged(nameof(IsIconToolActive));
        OnPropertyChanged(nameof(IsImageToolActive));
        OnPropertyChanged(nameof(IsCenterlineRectangleToolActive));
        OnPropertyChanged(nameof(IsReferentialToolActive));
        OnPropertyChanged(nameof(IsDimensionToolActive));
        OnPropertyChanged(nameof(IsAngleDimensionToolActive));
    }

    private void OnSerialTrafficCaptured(object? sender, SerialTrafficEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SerialEntries.Add(entry);
            if (SerialEntries.Count > 1500)
                SerialEntries.RemoveAt(0);
        });
    }

    private void OnSerialHistoryCleared(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => SerialEntries.Clear());
    }
}

public partial class CanvasDocumentViewModel : ObservableObject
{
    public CanvasDocumentViewModel(string title)
    {
        Title = title;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Title { get; }

    public ObservableCollection<Shape> Shapes { get; } = [];

    public ObservableCollection<string> ComputedShapeIds { get; } = [];

    [ObservableProperty]
    private DrawingTool _activeTool = DrawingTool.Select;

    [ObservableProperty]
    private double _zoom = 1d;

    [ObservableProperty]
    private AvaloniaVector _pan;

    [ObservableProperty]
    private AvaloniaPoint _cursorAvaloniaPosition;

    [ObservableProperty]
    private AvaloniaPoint _cursorCanvasPosition;

    [ObservableProperty]
    private IBrush _canvasBackgroundBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));

    [ObservableProperty]
    private bool _showCanvasBoundary = true;

    [ObservableProperty]
    private double _canvasBoundaryWidth = 960;

    [ObservableProperty]
    private double _canvasBoundaryHeight = 540;
}
