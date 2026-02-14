using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace paprUI.Models;

public class SerialService : IDisposable
{
    private const int MaxHistoryEntries = 1500;
    private SerialPort? _port;
    private readonly int _baudRate = 115200;
    private readonly object _sync = new();
    private readonly List<SerialTrafficEntry> _history = [];
    private string _incomingBuffer = string.Empty;

    public bool IsOpen => _port?.IsOpen ?? false;
    public string? CurrentPortName => _port?.PortName;
    public event EventHandler<SerialTrafficEntry>? TrafficCaptured;
    public event EventHandler? HistoryCleared;

    public void Connect(string portName)
    {
        if (_port != null)
        {
            if (_port.PortName == portName && _port.IsOpen) return;
            Disconnect();
        }

        _port = new SerialPort(portName, _baudRate);
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;
        _port.Open();
        AppendTraffic(SerialTrafficDirection.System, $"Connected to {portName} @ {_baudRate} baud");
    }

    public void Disconnect()
    {
        if (_port != null)
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }

            _port.DataReceived -= OnDataReceived;
            _port.ErrorReceived -= OnErrorReceived;
            _port.Dispose();
            AppendTraffic(SerialTrafficDirection.System, "Serial disconnected");
            _port = null;
        }
    }

    public async Task UploadRawJsonAsync(string json)
    {
        if (_port == null || !_port.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }

        // Serial receiver reads line-by-line; force a single-line payload.
        var singleLineJson = json.Replace("\r", string.Empty).Replace("\n", string.Empty);
        AppendTraffic(SerialTrafficDirection.Outgoing, singleLineJson);
        _port.WriteLine(singleLineJson);
        await Task.Delay(500);
        _port.WriteLine("");
        
        // Give it a moment to send
        await Task.Delay(100);
    }

    public Task UploadToM5Paper(M5PaperPayload payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        return UploadRawJsonAsync(json);
    }

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public IReadOnlyList<SerialTrafficEntry> GetHistorySnapshot()
    {
        lock (_sync)
        {
            return _history.ToList();
        }
    }

    public void ClearHistory()
    {
        lock (_sync)
        {
            _history.Clear();
        }

        HistoryCleared?.Invoke(this, EventArgs.Empty);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var chunk = _port?.ReadExisting();
            if (string.IsNullOrEmpty(chunk))
                return;

            ProcessIncomingChunk(chunk);
        }
        catch (Exception ex)
        {
            AppendTraffic(SerialTrafficDirection.System, $"RX error: {ex.Message}");
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        AppendTraffic(SerialTrafficDirection.System, $"Serial error: {e.EventType}");
    }

    private void ProcessIncomingChunk(string chunk)
    {
        string[] lines;

        lock (_sync)
        {
            _incomingBuffer += chunk;
            var normalized = _incomingBuffer.Replace("\r", "\n");
            lines = normalized.Split('\n');
            _incomingBuffer = normalized.EndsWith("\n", StringComparison.Ordinal)
                ? string.Empty
                : lines[^1];
        }

        for (var i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            AppendTraffic(SerialTrafficDirection.Incoming, line);
        }
    }

    private void AppendTraffic(SerialTrafficDirection direction, string message)
    {
        var entry = new SerialTrafficEntry(DateTimeOffset.Now, direction, message);

        lock (_sync)
        {
            _history.Add(entry);
            if (_history.Count > MaxHistoryEntries)
                _history.RemoveRange(0, _history.Count - MaxHistoryEntries);
        }

        TrafficCaptured?.Invoke(this, entry);
    }

    public void Dispose()
    {
        Disconnect();
    }
}
