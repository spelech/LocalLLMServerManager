using System;
using System.Linq;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class UiDiagnosticLoggerTests
{
    [Fact]
    public void RecordBindingError_AddsEntryWithAccurateTimestampAndSource()
    {
        var logger = new UiDiagnosticLogger();
        var beforeUtc = DateTime.UtcNow;

        logger.RecordBindingError("Cannot find property 'TestProp' on ViewModel", "TestView.axaml");

        var afterUtc = DateTime.UtcNow;

        var entries = logger.GetRecentErrors();
        Assert.Single(entries);

        var entry = entries[0];
        Assert.Equal("Cannot find property 'TestProp' on ViewModel", entry.Message);
        Assert.Equal("TestView.axaml", entry.Source);
        Assert.InRange(entry.Timestamp, beforeUtc, afterUtc);

        // Also verify entry with null source
        logger.RecordBindingError("Null source error");
        var allEntries = logger.GetRecentErrors();
        Assert.Equal(2, allEntries.Count);
        Assert.Null(allEntries[1].Source);
        Assert.Equal("Null source error", allEntries[1].Message);
    }

    [Fact]
    public void GetRecentErrors_LimitsReturnCount()
    {
        var logger = new UiDiagnosticLogger();

        for (int i = 1; i <= 20; i++)
        {
            logger.RecordBindingError($"Error {i}", $"Source{i}");
        }

        // Limit to 5
        var recent5 = logger.GetRecentErrors(5);
        Assert.Equal(5, recent5.Count);
        Assert.Equal("Error 16", recent5[0].Message);
        Assert.Equal("Error 20", recent5[4].Message);

        // When maxCount exceeds available items, return all available
        var recent30 = logger.GetRecentErrors(30);
        Assert.Equal(20, recent30.Count);
        Assert.Equal("Error 1", recent30[0].Message);
        Assert.Equal("Error 20", recent30[19].Message);

        // Edge case: maxCount <= 0 returns empty list
        var recentZero = logger.GetRecentErrors(0);
        Assert.Empty(recentZero);

        var recentNegative = logger.GetRecentErrors(-5);
        Assert.Empty(recentNegative);

        // Default count is 50, so should return all 20
        var recentDefault = logger.GetRecentErrors();
        Assert.Equal(20, recentDefault.Count);
    }

    [Fact]
    public void BoundedHistory_DiscardsOldestEntriesWhenCapacityExceeded()
    {
        // Test with custom small capacity
        const int capacity = 5;
        var logger = new UiDiagnosticLogger(capacity);

        for (int i = 1; i <= 8; i++)
        {
            logger.RecordBindingError($"Error {i}");
        }

        var entries = logger.GetRecentErrors(10);
        Assert.Equal(capacity, entries.Count);

        // Oldest entries 1, 2, 3 should have been discarded
        Assert.DoesNotContain(entries, e => e.Message == "Error 1");
        Assert.DoesNotContain(entries, e => e.Message == "Error 2");
        Assert.DoesNotContain(entries, e => e.Message == "Error 3");

        // Remaining entries should be 4 through 8 in FIFO order
        Assert.Equal("Error 4", entries[0].Message);
        Assert.Equal("Error 5", entries[1].Message);
        Assert.Equal("Error 6", entries[2].Message);
        Assert.Equal("Error 7", entries[3].Message);
        Assert.Equal("Error 8", entries[4].Message);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var logger = new UiDiagnosticLogger();
        logger.RecordBindingError("Error 1");
        logger.RecordBindingError("Error 2");
        Assert.Equal(2, logger.GetRecentErrors().Count);

        logger.Clear();

        var entries = logger.GetRecentErrors();
        Assert.Empty(entries);

        var summary = logger.ExportLogSummary();
        Assert.Contains("No binding errors recorded", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportLogSummary_ReturnsFormattedSummary()
    {
        var logger = new UiDiagnosticLogger();

        // Empty state
        var emptySummary = logger.ExportLogSummary();
        Assert.Contains("No binding errors recorded", emptySummary, StringComparison.OrdinalIgnoreCase);

        // Populated state
        logger.RecordBindingError("Binding failed for ItemCount", "DashboardView.axaml");
        logger.RecordBindingError("NullReference during converter evaluation", null);

        var summary = logger.ExportLogSummary();

        Assert.Contains("DashboardView.axaml", summary);
        Assert.Contains("Binding failed for ItemCount", summary);
        Assert.Contains("NullReference during converter evaluation", summary);
        Assert.Contains("2 error", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordBindingError_IsThreadSafeUnderConcurrentAccess()
    {
        const int threadCount = 10;
        const int iterationsPerThread = 50;
        const int capacity = 200;
        var logger = new UiDiagnosticLogger(capacity);

        Parallel.For(0, threadCount, threadIndex =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                logger.RecordBindingError($"Thread {threadIndex} message {i}", $"Source{threadIndex}");
                _ = logger.GetRecentErrors(10);
            }
        });

        var entries = logger.GetRecentErrors(capacity);
        Assert.Equal(capacity, entries.Count);
        Assert.True(entries.All(e => !string.IsNullOrEmpty(e.Message)));
    }

    [Fact]
    public void UiDiagnosticTraceListener_InterceptsBindingMessages_AndRecordsToLogger()
    {
        var logger = new UiDiagnosticLogger();
        var listener = new UiDiagnosticTraceListener(logger);

        listener.WriteLine("[Binding] Error in binding to 'MissingProperty': Path not found on ViewModel");
        listener.Write("Error in binding to 'Command': Null reference");
        listener.WriteLine("[Warning][Binding] Cannot convert 'abc' to integer");

        var errors = logger.GetRecentErrors();
        Assert.Equal(3, errors.Count);
        Assert.Contains("MissingProperty", errors[0].Message);
        Assert.Equal("Avalonia.Binding", errors[0].Source);
        Assert.Contains("Command", errors[1].Message);
        Assert.Contains("Cannot convert", errors[2].Message);
    }

    [Fact]
    public void UiDiagnosticTraceListener_IgnoresUnrelatedTraceMessages()
    {
        var logger = new UiDiagnosticLogger();
        var listener = new UiDiagnosticTraceListener(logger);

        listener.WriteLine("[Http] GET /api/health 200 OK");
        listener.WriteLine("Application started successfully on port 5246");
        listener.Write(null);
        listener.WriteLine("   ");

        var errors = logger.GetRecentErrors();
        Assert.Empty(errors);
    }
}

