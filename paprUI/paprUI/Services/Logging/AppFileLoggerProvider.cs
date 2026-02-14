using System;
using Microsoft.Extensions.Logging;

namespace paprUI.Services.Logging;

public sealed class AppFileLoggerProvider : ILoggerProvider
{
    private readonly AppLogWriter _writer;
    private readonly LogLevel _minimumLevel;

    public AppFileLoggerProvider(AppLogWriter writer, LogLevel minimumLevel)
    {
        _writer = writer;
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new AppFileLogger(categoryName, _writer, _minimumLevel);
    }

    public void Dispose()
    {
    }

    private sealed class AppFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly AppLogWriter _writer;
        private readonly LogLevel _minimumLevel;

        public AppFileLogger(string categoryName, AppLogWriter writer, LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _writer = writer;
            _minimumLevel = minimumLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minimumLevel && logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            _writer.Write(_categoryName, logLevel, eventId, message, exception);
        }
    }
}
