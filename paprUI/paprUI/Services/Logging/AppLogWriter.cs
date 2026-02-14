using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace paprUI.Services.Logging;

public sealed class AppLogWriter
{
    private readonly object _sync = new();
    private readonly string _logDirectory;

    public AppLogWriter()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "papr",
            "paprUI",
            "logs");

        Directory.CreateDirectory(_logDirectory);
    }

    public string GetCurrentLogPath()
    {
        var fileName = $"paprui-{DateTime.UtcNow:yyyyMMdd}.log";
        return Path.Combine(_logDirectory, fileName);
    }

    public void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        var now = DateTimeOffset.UtcNow;
        var eventFragment = eventId.Id == 0 ? string.Empty : $" [{eventId.Id}:{eventId.Name}]";
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{now:O} [{level}] {category}{eventFragment} {message}");

        lock (_sync)
        {
            var path = GetCurrentLogPath();
            File.AppendAllText(path, line + Environment.NewLine);
            if (exception is not null)
            {
                File.AppendAllText(path, exception + Environment.NewLine);
            }
        }
    }
}
