using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace paprUI.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextCommand), "text")]
[JsonDerivedType(typeof(RectCommand), "rect")]
[JsonDerivedType(typeof(LineCommand), "line")]
[JsonDerivedType(typeof(CircleCommand), "circle")]
public abstract partial class DrawingCommand : ObservableObject
{
    [property: JsonIgnore]
    [ObservableProperty]
    private bool _isSelected;

    [JsonIgnore]
    public virtual int CanvasX => 0;
    [JsonIgnore]
    public virtual int CanvasY => 0;
}

public partial class TextCommand : DrawingCommand
{
    [JsonPropertyName("x")]
    [ObservableProperty] private int _x;
    [JsonPropertyName("y")]
    [ObservableProperty] private int _y;

    [JsonIgnore]
    public override int CanvasX => X;
    [JsonIgnore]
    public override int CanvasY => Y;

    partial void OnXChanged(int value) => OnPropertyChanged(nameof(CanvasX));
    partial void OnYChanged(int value) => OnPropertyChanged(nameof(CanvasY));

    [JsonPropertyName("size")]
    [ObservableProperty] private int _size = 1;
    [JsonPropertyName("content")]
    [ObservableProperty] private string _content = string.Empty;
}

public partial class RectCommand : DrawingCommand
{
    [JsonPropertyName("x")]
    [ObservableProperty] private int _x;
    [JsonPropertyName("y")]
    [ObservableProperty] private int _y;

    [JsonIgnore]
    public override int CanvasX => X;
    [JsonIgnore]
    public override int CanvasY => Y;

    partial void OnXChanged(int value) => OnPropertyChanged(nameof(CanvasX));
    partial void OnYChanged(int value) => OnPropertyChanged(nameof(CanvasY));

    [JsonPropertyName("w")]
    [ObservableProperty] private int _w;

    [JsonPropertyName("h")]
    [ObservableProperty] private int _h;
    [JsonPropertyName("fill")]
    [ObservableProperty] private bool _fill = true;
}

public partial class LineCommand : DrawingCommand
{
    [JsonPropertyName("x1")]
    [ObservableProperty] private int _x1;
    [JsonPropertyName("y1")]
    [ObservableProperty] private int _y1;
    [JsonPropertyName("x2")]
    [ObservableProperty] private int _x2;
    [JsonPropertyName("y2")]
    [ObservableProperty] private int _y2;
}

public partial class CircleCommand : DrawingCommand
{
    [JsonPropertyName("x")]
    [ObservableProperty] private int _x;
    [JsonPropertyName("y")]
    [ObservableProperty] private int _y;

    [JsonPropertyName("r")]
    [ObservableProperty] private int _r;

    [JsonIgnore]
    public override int CanvasX => X - R;
    [JsonIgnore]
    public override int CanvasY => Y - R;

    partial void OnXChanged(int value) => OnPropertyChanged(nameof(CanvasX));
    partial void OnYChanged(int value) => OnPropertyChanged(nameof(CanvasY));
    partial void OnRChanged(int value) 
    { 
        OnPropertyChanged(nameof(CanvasX)); 
        OnPropertyChanged(nameof(CanvasY)); 
    }

    [JsonPropertyName("fill")]
    [ObservableProperty] private bool _fill = true;
}

public class M5PaperPayload
{
    [JsonPropertyName("clear")]
    public bool clear { get; set; }
    [JsonPropertyName("commands")]
    public List<DrawingCommand> commands { get; set; } = new();
}

public class CanvasModel
{
    [JsonIgnore]
    public string Name { get; set; } = "New Canvas";
    public ObservableCollection<DrawingCommand> Elements { get; set; } = new();
}
