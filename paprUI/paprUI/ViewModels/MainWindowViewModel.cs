using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using paprUI.Models;
using System.IO;
using System.Text.Json;

namespace paprUI.ViewModels;

public enum DrawingTool
{
    None,
    Select,
    Text,
    Line,
    Rectangle,
    Circle,
    Delete
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SerialService _serialService;

    [ObservableProperty]
    private DrawingTool _currentTool = DrawingTool.None;

    [ObservableProperty]
    private CanvasModel? _currentCanvas;

    public ObservableCollection<CanvasModel> SavedCanvases { get; } = new();

    [ObservableProperty]
    private DrawingCommand? _selectedElement;

    [ObservableProperty]
    private string _statusText = "Ready";

    partial void OnSelectedElementChanged(DrawingCommand? value)
    {
        if (value != null)
        {
            foreach (var element in CurrentCanvas?.Elements ?? Enumerable.Empty<DrawingCommand>())
            {
                element.IsSelected = element == value;
            }
            StatusText = $"Selected: {value.GetType().Name}";
        }
        else
        {
            foreach (var element in CurrentCanvas?.Elements ?? Enumerable.Empty<DrawingCommand>())
            {
                element.IsSelected = false;
            }
            StatusText = "Nothing selected";
        }
    }

    public MainWindowViewModel(SerialService serialService)
    {
        _serialService = serialService;
        CurrentCanvas = new CanvasModel { Name = "New Canvas" };
        SavedCanvases.Add(CurrentCanvas);
    }

    [RelayCommand]
    private void SelectTool(string toolName)
    {
        if (Enum.TryParse<DrawingTool>(toolName, true, out var tool))
        {
            CurrentTool = tool;
            StatusText = $"Tool: {CurrentTool}";
        }
    }

    [RelayCommand]
    private void NewCanvas()
    {
        CurrentCanvas = new CanvasModel { Name = $"Canvas {SavedCanvases.Count + 1}" };
        SavedCanvases.Add(CurrentCanvas);
        StatusText = "New canvas created";
    }

    [RelayCommand]
    private void SaveCanvas()
    {
        // For this app, "Save" might mean persist to disk or just keep in list.
        // Let's implement a simple JSON persistence for all canvases.
        try
        {
            var json = JsonSerializer.Serialize(SavedCanvases);
            File.WriteAllText("saved_canvases.json", json);
            StatusText = "All canvases saved to disk";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UploadCanvas()
    {
        if (CurrentCanvas == null) return;

        try
        {
            if (!_serialService.IsOpen)
            {
                var ports = SerialService.GetAvailablePorts();
                if (ports.Length == 0)
                {
                    StatusText = "No serial ports found!";
                    return;
                }

                var port = ports[0];
                StatusText = $"Connecting to {port}...";
                _serialService.Connect(port);
            }

            var payload = new M5PaperPayload
            {
                clear = true,
                commands = CurrentCanvas.Elements.ToList()
            };

            StatusText = "Uploading...";
            await _serialService.UploadToM5Paper(payload);
            StatusText = $"Upload successful! (via {_serialService.CurrentPortName})";
        }
        catch (Exception ex)
        {
            StatusText = $"Upload failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteCanvas()
    {
        if (CurrentCanvas != null && SavedCanvases.Count > 1)
        {
            var toRemove = CurrentCanvas;
            SavedCanvases.Remove(toRemove);
            CurrentCanvas = SavedCanvases.LastOrDefault();
            StatusText = "Canvas deleted";
        }
        else if (CurrentCanvas != null)
        {
            CurrentCanvas.Elements.Clear();
            StatusText = "Canvas cleared (last one)";
        }
    }
}
