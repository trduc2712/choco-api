using System.Collections.Concurrent;
using System.Globalization;

using Microsoft.Extensions.Logging;

namespace Choco.IntegrationTests.Logging;

internal sealed record CapturedLog(
    string Category, LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, string> Scope);

internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<CapturedLog> Entries => _entries;

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(categoryName, this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
    }

    private void Add(string category, LogLevel level, string message, Exception? exception)
    {
        Dictionary<string, string> scope = [];

        _scopeProvider.ForEachScope(
            (state, values) =>
            {
                if (state is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    foreach (KeyValuePair<string, object> pair in pairs)
                    {
                        values[pair.Key] = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                }
            },
            scope);

        _entries.Enqueue(new CapturedLog(category, level, message, exception, scope));
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly CapturingLoggerProvider _provider;

        public CapturingLogger(string category, CapturingLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _provider._scopeProvider.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _provider.Add(_category, logLevel, formatter(state, exception), exception);
        }
    }
}
