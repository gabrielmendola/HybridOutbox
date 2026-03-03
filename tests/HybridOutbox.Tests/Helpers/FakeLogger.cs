using Microsoft.Extensions.Logging;

namespace HybridOutbox.Tests.Helpers;

internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    public bool HasWarning(string fragment) =>
        Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains(fragment));

    public bool HasError(string fragment) =>
        Entries.Any(e => e.Level == LogLevel.Error && e.Message.Contains(fragment));
}
