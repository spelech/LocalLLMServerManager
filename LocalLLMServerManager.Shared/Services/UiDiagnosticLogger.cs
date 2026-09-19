using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LocalLLMServerManager.Shared.Services;

/// <summary>
/// Represents a captured UI binding or diagnostic error entry.
/// </summary>
public record BindingDiagnosticEntry(DateTime Timestamp, string Message, string? Source = null);

/// <summary>
/// Interface for thread-safe UI diagnostic error logging.
/// </summary>
public interface IUiDiagnosticLogger
{
    /// <summary>
    /// Records a new UI binding error.
    /// </summary>
    void RecordBindingError(string message, string? source = null);

    /// <summary>
    /// Retrieves the most recent binding diagnostic entries up to <paramref name="maxCount"/>.
    /// </summary>
    IReadOnlyList<BindingDiagnosticEntry> GetRecentErrors(int maxCount = 50);

    /// <summary>
    /// Clears all recorded diagnostic entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Exports a formatted diagnostic summary string.
    /// </summary>
    string ExportLogSummary();
}

/// <summary>
/// Thread-safe bounded diagnostic logger service for UI binding errors.
/// </summary>
public class UiDiagnosticLogger : IUiDiagnosticLogger
{
    public const int DefaultMaxEntries = 500;

    public static UiDiagnosticLogger Instance { get; } = new();

    private readonly int _maxCapacity;
    private readonly object _lock = new();
    private readonly Queue<BindingDiagnosticEntry> _entries;

    public int MaxCapacity => _maxCapacity;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public UiDiagnosticLogger(int maxCapacity = DefaultMaxEntries)
    {
        _maxCapacity = maxCapacity > 0 ? maxCapacity : DefaultMaxEntries;
        _entries = new Queue<BindingDiagnosticEntry>(_maxCapacity);
    }

    public void RecordBindingError(string message, string? source = null)
    {
        lock (_lock)
        {
            while (_entries.Count >= _maxCapacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(new BindingDiagnosticEntry(DateTime.UtcNow, message ?? string.Empty, source));
        }
    }

    public IReadOnlyList<BindingDiagnosticEntry> GetRecentErrors(int maxCount = 50)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<BindingDiagnosticEntry>();
        }

        lock (_lock)
        {
            if (_entries.Count == 0)
            {
                return Array.Empty<BindingDiagnosticEntry>();
            }

            int count = Math.Min(maxCount, _entries.Count);
            int skip = _entries.Count - count;
            return _entries.Skip(skip).Take(count).ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    public string ExportLogSummary()
    {
        lock (_lock)
        {
            if (_entries.Count == 0)
            {
                return "No binding errors recorded.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== UI Diagnostic Summary ({_entries.Count} error(s)) ===");
            foreach (var entry in _entries)
            {
                var sourceInfo = string.IsNullOrWhiteSpace(entry.Source) ? string.Empty : $" [{entry.Source}]";
                sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}]{sourceInfo}: {entry.Message}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
