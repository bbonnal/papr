using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Data;
using Microsoft.Extensions.DependencyInjection;
using rUI.Avalonia.Desktop.Controls.Docking;
using rUI.Avalonia.Desktop.Services.Shortcuts;
using rUI.Drawing.Core;
using rUI.Drawing.Avalonia.Controls.Drawing;
using paprUI.ViewModels;

namespace paprUI.Views;

public partial class DrawPageView : UserControl
{
    private readonly Dictionary<Guid, DockPane> _paneByCanvasId = [];
    private DrawPageViewModel? _currentViewModel;
    private IDisposable? _shortcutBinding;

    public DrawPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DockHost.FocusedPaneChanged += OnDockHostFocusedPaneChanged;
        DockHost.PaneClosed += OnDockHostPaneClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.Canvases.CollectionChanged -= OnCanvasCollectionChanged;
            _currentViewModel.EscapeToSelectRequested -= OnEscapeToSelectRequested;
        }

        _currentViewModel = null;
        _shortcutBinding?.Dispose();
        _shortcutBinding = null;

        if (DataContext is not DrawPageViewModel vm)
            return;

        _currentViewModel = vm;
        vm.Canvases.CollectionChanged += OnCanvasCollectionChanged;
        vm.EscapeToSelectRequested += OnEscapeToSelectRequested;
        RebuildDockPanes(vm);
        BindShortcuts(vm);
    }

    private void OnCanvasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not DrawPageViewModel vm)
            return;

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<CanvasDocumentViewModel>())
                AddPaneForCanvas(vm, item);
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<CanvasDocumentViewModel>())
                RemovePaneForCanvas(item.Id);
        }
    }

    private void RebuildDockPanes(DrawPageViewModel vm)
    {
        _paneByCanvasId.Clear();
        DockHost.Panes.Clear();

        foreach (var canvas in vm.Canvases)
            AddPaneForCanvas(vm, canvas);
    }

    private void AddPaneForCanvas(DrawPageViewModel vm, CanvasDocumentViewModel canvas)
    {
        if (_paneByCanvasId.ContainsKey(canvas.Id))
            return;

        var drawingCanvas = BuildDrawingCanvas(vm, canvas);
        var pane = new DockPane
        {
            Header = canvas.Title,
            PaneContent = drawingCanvas,
            Tag = canvas.Id
        };

        _paneByCanvasId[canvas.Id] = pane;
        DockHost.AddPane(pane);
    }

    private void RemovePaneForCanvas(Guid canvasId)
    {
        if (!_paneByCanvasId.TryGetValue(canvasId, out var pane))
            return;

        _paneByCanvasId.Remove(canvasId);
        DockHost.ClosePane(pane);
    }

    private DrawingCanvasControl BuildDrawingCanvas(DrawPageViewModel vm, CanvasDocumentViewModel canvas)
    {
        var control = new DrawingCanvasControl();
        control.Bind(DrawingCanvasControl.ShapesProperty, new Binding(nameof(CanvasDocumentViewModel.Shapes)) { Source = canvas });
        control.Bind(DrawingCanvasControl.ActiveToolProperty, new Binding(nameof(CanvasDocumentViewModel.ActiveTool)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.ZoomProperty, new Binding(nameof(CanvasDocumentViewModel.Zoom)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.PanProperty, new Binding(nameof(CanvasDocumentViewModel.Pan)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.CursorAvaloniaPositionProperty, new Binding(nameof(CanvasDocumentViewModel.CursorAvaloniaPosition)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.CursorCanvasPositionProperty, new Binding(nameof(CanvasDocumentViewModel.CursorCanvasPosition)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.ComputedShapeIdsProperty, new Binding(nameof(CanvasDocumentViewModel.ComputedShapeIds)) { Source = canvas });
        control.Bind(DrawingCanvasControl.CanvasBackgroundProperty, new Binding(nameof(CanvasDocumentViewModel.CanvasBackgroundBrush)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.ShowCanvasBoundaryProperty, new Binding(nameof(CanvasDocumentViewModel.ShowCanvasBoundary)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.CanvasBoundaryWidthProperty, new Binding(nameof(CanvasDocumentViewModel.CanvasBoundaryWidth)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.CanvasBoundaryHeightProperty, new Binding(nameof(CanvasDocumentViewModel.CanvasBoundaryHeight)) { Source = canvas, Mode = BindingMode.TwoWay });
        control.Bind(DrawingCanvasControl.DialogServiceProperty, new Binding(nameof(DrawPageViewModel.DialogService)) { Source = vm });
        control.Bind(DrawingCanvasControl.InfoBarServiceProperty, new Binding(nameof(DrawPageViewModel.InfoBarService)) { Source = vm });
        control.ShapeStroke = (Avalonia.Media.IBrush?)this.FindResource("rUIAccentBrush") ?? Avalonia.Media.Brushes.DeepSkyBlue;
        control.PreviewStroke = (Avalonia.Media.IBrush?)this.FindResource("rUIWarningBrush") ?? Avalonia.Media.Brushes.Orange;
        control.HoverStroke = (Avalonia.Media.IBrush?)this.FindResource("rUIWarningBrush") ?? Avalonia.Media.Brushes.Gold;
        return control;
    }

    private void OnDockHostFocusedPaneChanged(object? sender, DockPane? pane)
    {
        if (DataContext is not DrawPageViewModel vm || pane?.Tag is not Guid canvasId)
            return;

        vm.FocusCanvas(canvasId);
    }

    private void OnDockHostPaneClosed(object? sender, DockPane pane)
    {
        if (DataContext is not DrawPageViewModel vm || pane.Tag is not Guid canvasId)
            return;

        vm.RemoveCanvas(canvasId);
    }

    private void BindShortcuts(DrawPageViewModel vm)
    {
        var services = App.Services;
        if (services is null)
            return;

        var shortcutService = services.GetService<IShortcutService>();
        if (shortcutService is null)
            return;

        _shortcutBinding = shortcutService.Bind(this, vm.GetShortcutDefinitions());
    }

    private void OnEscapeToSelectRequested(object? sender, EventArgs e)
    {
        ClearFocusedCanvasSelection();
    }

    private void ClearFocusedCanvasSelection()
    {
        DrawingCanvasControl? drawingCanvas = null;

        if (DockHost.FocusedPane?.PaneContent is DrawingCanvasControl focusedCanvas)
            drawingCanvas = focusedCanvas;
        else if (DataContext is DrawPageViewModel vm &&
                 vm.FocusedCanvas is not null &&
                 _paneByCanvasId.TryGetValue(vm.FocusedCanvas.Id, out var pane) &&
                 pane.PaneContent is DrawingCanvasControl vmCanvas)
            drawingCanvas = vmCanvas;

        if (drawingCanvas is null)
            return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        drawingCanvas.GetType().GetField("_selectedShape", flags)?.SetValue(drawingCanvas, null);
        drawingCanvas.GetType().GetField("_hoveredShape", flags)?.SetValue(drawingCanvas, null);
        drawingCanvas.GetType().GetField("_contextMenuTargetShape", flags)?.SetValue(drawingCanvas, null);
        drawingCanvas.GetType().GetField("_previewShape", flags)?.SetValue(drawingCanvas, null);
        drawingCanvas.GetType().GetField("_lastDragWorld", flags)?.SetValue(drawingCanvas, null);
        drawingCanvas.GetType().GetField("_activeHandle", flags)?.SetValue(drawingCanvas, ShapeHandleKind.None);
        drawingCanvas.InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_currentViewModel is not null)
            _currentViewModel.EscapeToSelectRequested -= OnEscapeToSelectRequested;

        _shortcutBinding?.Dispose();
        _shortcutBinding = null;
        base.OnDetachedFromVisualTree(e);
    }
}
