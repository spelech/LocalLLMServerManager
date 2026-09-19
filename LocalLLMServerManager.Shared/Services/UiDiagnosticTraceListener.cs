using System;
using System.Diagnostics;

namespace LocalLLMServerManager.Shared.Services;

/// <summary>
/// TraceListener that intercepts Avalonia UI binding diagnostic and warning messages
/// and records them into an IUiDiagnosticLogger instance.
/// </summary>
public class UiDiagnosticTraceListener : TraceListener
{
    private readonly IUiDiagnosticLogger _logger;

    public UiDiagnosticTraceListener(IUiDiagnosticLogger logger)
        : base("UiDiagnosticTraceListener")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Write(string? message)
    {
        ProcessMessage(message);
    }

    public override void WriteLine(string? message)
    {
        ProcessMessage(message);
    }

    private void ProcessMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Intercept messages related to Avalonia data binding errors and warnings
        if (message.Contains("[Binding]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Error in binding", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Binding error", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("Binding", StringComparison.OrdinalIgnoreCase) && 
             (message.Contains("Error", StringComparison.OrdinalIgnoreCase) || message.Contains("Warning", StringComparison.OrdinalIgnoreCase))))
        {
            _logger.RecordBindingError(message.Trim(), "Avalonia.Binding");
        }
    }
}
