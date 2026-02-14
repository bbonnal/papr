using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using paprUI.Models;

namespace paprUI.Views.Controls;

public partial class SerialConsoleControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SerialConsoleControl, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<ICommand?> ClearCommandProperty =
        AvaloniaProperty.Register<SerialConsoleControl, ICommand?>(nameof(ClearCommand));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SerialConsoleControl, string>(nameof(Title), "Serial Console");

    private ScrollViewer? _scrollViewer;
    private ListBox? _listBox;
    private Button? _copyButton;
    private INotifyCollectionChanged? _currentCollection;

    public SerialConsoleControl()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            _scrollViewer = this.FindControl<ScrollViewer>("PART_ScrollViewer");
            _listBox = this.FindControl<ListBox>("PART_ListBox");
            _copyButton = this.FindControl<Button>("PART_CopyButton");
            if (_listBox is not null)
                _listBox.KeyDown += OnListBoxKeyDown;
            if (_copyButton is not null)
                _copyButton.Click += OnCopyButtonClick;
            SubscribeToCollection(ItemsSource as INotifyCollectionChanged);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_listBox is not null)
                _listBox.KeyDown -= OnListBoxKeyDown;
            if (_copyButton is not null)
                _copyButton.Click -= OnCopyButtonClick;
            SubscribeToCollection(null);
        };
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? ClearCommand
    {
        get => GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
            SubscribeToCollection(change.NewValue as INotifyCollectionChanged);
    }

    private void SubscribeToCollection(INotifyCollectionChanged? collection)
    {
        if (_currentCollection is not null)
            _currentCollection.CollectionChanged -= OnCollectionChanged;

        _currentCollection = collection;

        if (_currentCollection is not null)
            _currentCollection.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _scrollViewer?.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.C)
        {
            _ = CopySelectedAsync();
            e.Handled = true;
        }
    }

    private void OnCopyButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Console.WriteLine("SerialConsole copy button clicked");
        _ = CopySelectedAsync();
    }

    private async Task CopySelectedAsync()
    {
        if (_listBox is null)
            return;

        var builder = new StringBuilder();
        IEnumerable selectedItems = _listBox.SelectedItems ?? Array.Empty<object>();
        var selectedEntries = selectedItems.OfType<SerialTrafficEntry>().ToList();
        if (selectedEntries.Count == 0)
        {
            var source = ItemsSource;
            if (source is not null)
                selectedEntries = source!.OfType<SerialTrafficEntry>().ToList();
        }

        foreach (var selected in selectedEntries)
        {
            var direction = selected.Direction switch
            {
                SerialTrafficDirection.Outgoing => "TX",
                SerialTrafficDirection.Incoming => "RX",
                _ => "SYS"
            };

            _ = builder.Append(selected.TimestampText)
                .Append(' ')
                .Append(direction)
                .Append(' ')
                .AppendLine(selected.Message);
        }

        if (builder.Length == 0)
            return;

        var text = builder.ToString();
        var copied = await TryCopyWithAvaloniaClipboardAsync(text);
        if (!copied)
            copied = await TryCopyWithWlCopyAsync(text);

        Console.WriteLine($"SerialConsole copy result: {(copied ? "ok" : "failed")}");
    }

    private async Task<bool> TryCopyWithAvaloniaClipboardAsync(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return false;

            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SerialConsole Avalonia clipboard failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> TryCopyWithWlCopyAsync(string text)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wl-copy",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
                return true;

            var err = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"SerialConsole wl-copy failed: exit={process.ExitCode} err={err}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SerialConsole wl-copy exception: {ex.Message}");
            return false;
        }
    }
}

public sealed class SerialDirectionToBrushConverter : IValueConverter
{
    private static readonly IBrush OutgoingBrush = new SolidColorBrush(Color.Parse("#2ECC71"));
    private static readonly IBrush IncomingBrush = new SolidColorBrush(Color.Parse("#E74C3C"));
    private static readonly IBrush SystemBrush = new SolidColorBrush(Color.Parse("#9AA5B1"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is SerialTrafficDirection direction
            ? direction switch
            {
                SerialTrafficDirection.Outgoing => OutgoingBrush,
                SerialTrafficDirection.Incoming => IncomingBrush,
                _ => SystemBrush
            }
            : SystemBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SerialDirectionToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is SerialTrafficDirection direction
            ? direction switch
            {
                SerialTrafficDirection.Outgoing => "TX",
                SerialTrafficDirection.Incoming => "RX",
                _ => "SYS"
            }
            : "SYS";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
