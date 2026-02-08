using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using paprUI.ViewModels;
using paprUI.Models;
using System.Linq;
using Avalonia.Data.Converters;

namespace paprUI.Views;

public partial class MainWindow : Window
{
    private Point? _firstPoint;
    private Point _lastPointerPosition;
    private bool _isDragging;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.CurrentCanvas == null) return;

        var canvas = this.FindControl<Canvas>("DrawingCanvas");
        if (canvas == null) return;
        var pos = e.GetPosition(canvas);
        var x = (int)pos.X;
        var y = (int)pos.Y;

        if (vm.CurrentTool == DrawingTool.Select)
        {
            // Simple hit testing - find the last element that contains the point
            var element = vm.CurrentCanvas.Elements.LastOrDefault(el => HitTest(el, pos));
            vm.SelectedElement = element;
            if (element != null)
            {
                _isDragging = true;
                _lastPointerPosition = pos;
            }
            return;
        }

        switch (vm.CurrentTool)
        {
            case DrawingTool.Text:
                vm.CurrentCanvas.Elements.Add(new TextCommand { X = x, Y = y, Content = "New Text", Size = 2 });
                break;
            case DrawingTool.Rectangle:
                vm.CurrentCanvas.Elements.Add(new RectCommand { X = x, Y = y, W = 100, H = 50, Fill = false });
                break;
            case DrawingTool.Circle:
                vm.CurrentCanvas.Elements.Add(new CircleCommand { X = x, Y = y, R = 30, Fill = true });
                break;
            case DrawingTool.Line:
                if (_firstPoint == null)
                {
                    _firstPoint = pos;
                    vm.StatusText = "Click second point for line";
                }
                else
                {
                    vm.CurrentCanvas.Elements.Add(new LineCommand
                    {
                        X1 = (int)_firstPoint.Value.X,
                        Y1 = (int)_firstPoint.Value.Y,
                        X2 = x,
                        Y2 = y
                    });
                    _firstPoint = null;
                    vm.StatusText = "Line added";
                }

                break;
            case DrawingTool.Delete:
                var toDelete = vm.CurrentCanvas.Elements.LastOrDefault(el => HitTest(el, pos));
                if (toDelete != null)
                {
                    vm.CurrentCanvas.Elements.Remove(toDelete);
                    if (vm.SelectedElement == toDelete) vm.SelectedElement = null;
                }
                break;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || DataContext is not MainWindowViewModel vm || vm.SelectedElement == null) return;

        var canvas = this.FindControl<Canvas>("DrawingCanvas");
        if (canvas == null) return;
        
        var pos = e.GetPosition(canvas);
        var deltaX = (int)(pos.X - _lastPointerPosition.X);
        var deltaY = (int)(pos.Y - _lastPointerPosition.Y);

        if (deltaX == 0 && deltaY == 0) return;

        MoveElement(vm.SelectedElement, deltaX, deltaY);
        _lastPointerPosition = pos;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    private bool HitTest(DrawingCommand el, Point p)
    {
        // Simple hit testing logic
        if (el is TextCommand t)
        {
            // Approximate text size
            return p.X >= t.X && p.X <= t.X + 100 && p.Y >= t.Y && p.Y <= t.Y + (t.Size * 16);
        }
        if (el is RectCommand r)
        {
            return p.X >= r.X && p.X <= r.X + r.W && p.Y >= r.Y && p.Y <= r.Y + r.H;
        }
        if (el is CircleCommand c)
        {
            var dx = p.X - c.X;
            var dy = p.Y - c.Y;
            return (dx * dx + dy * dy) <= (c.R * c.R);
        }
        if (el is LineCommand l)
        {
            // Distance from point to line segment
            double L2 = (l.X2 - l.X1) * (l.X2 - l.X1) + (l.Y2 - l.Y1) * (l.Y2 - l.Y1);
            if (L2 == 0) return false;
            double tParam = ((p.X - l.X1) * (l.X2 - l.X1) + (p.Y - l.Y1) * (l.Y2 - l.Y1)) / L2;
            tParam = Math.Max(0, Math.Min(1, tParam));
            double distSq = (p.X - (l.X1 + tParam * (l.X2 - l.X1))) * (p.X - (l.X1 + tParam * (l.X2 - l.X1))) +
                            (p.Y - (l.Y1 + tParam * (l.Y2 - l.Y1))) * (p.Y - (l.Y1 + tParam * (l.Y2 - l.Y1)));
            return distSq < 100; // 10px tolerance
        }
        return false;
    }

    private void MoveElement(DrawingCommand el, int dx, int dy)
    {
        if (el is TextCommand t) { t.X += dx; t.Y += dy; }
        else if (el is RectCommand r) { r.X += dx; r.Y += dy; }
        else if (el is CircleCommand c) { c.X += dx; c.Y += dy; }
        else if (el is LineCommand l) { l.X1 += dx; l.Y1 += dy; l.X2 += dx; l.Y2 += dy; }
    }
}