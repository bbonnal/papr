using System;
using System.IO.Ports;
using System.Text.Json;
using System.Threading.Tasks;
using paprUI.Models;

namespace paprUI.Models;

public class SerialService : IDisposable
{
    private SerialPort? _port;
    private readonly int _baudRate = 115200;

    public bool IsOpen => _port?.IsOpen ?? false;
    public string? CurrentPortName => _port?.PortName;

    public void Connect(string portName)
    {
        if (_port != null)
        {
            if (_port.PortName == portName && _port.IsOpen) return;
            Disconnect();
        }

        _port = new SerialPort(portName, _baudRate);
        _port.Open();
    }

    public void Disconnect()
    {
        if (_port != null)
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
            _port.Dispose();
            _port = null;
        }
    }

    public async Task UploadToM5Paper(M5PaperPayload payload)
    {
        if (_port == null || !_port.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }

        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(payload, options);
        
        _port.WriteLine(json);
        await Task.Delay(500);
        _port.WriteLine("");
        
        // Give it a moment to send
        await Task.Delay(100);
    }

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public void Dispose()
    {
        Disconnect();
    }
}
