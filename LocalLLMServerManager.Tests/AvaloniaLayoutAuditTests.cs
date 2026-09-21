using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LayoutInspector.Engine;
using Avalonia.LayoutInspector.Models;
using Avalonia.LayoutInspector.Responsive;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using LocalLLMServerManager.Views;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AvaloniaLayoutAuditTests
{
    private readonly ITestOutputHelper _output;

    public AvaloniaLayoutAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public void MainWindow_ResponsiveLayoutAudit()
    {
        var window = new MainWindow();
        try
        {
            var runner = new ResponsiveAuditRunner();
            var responsiveReport = runner.Run(window, StandardBreakpoints.AllStandard);

            _output.WriteLine($"MainWindow Responsive Audit - AllPassed: {responsiveReport.AllPassed}, TotalViolations: {responsiveReport.TotalViolations}");
            foreach (var (bp, report) in responsiveReport.BreakpointReports)
            {
                _output.WriteLine($"--- Breakpoint {bp.Name} ({bp.Width}x{bp.Height}): Health={report.HealthScore}/100, Violations={report.Violations.Count} ---");
                if (report.Violations.Count > 0)
                {
                    _output.WriteLine(report.ToDetailedReport());
                }
            }

            Assert.NotNull(responsiveReport);
            Assert.NotEmpty(responsiveReport.BreakpointReports);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CivitaiTabControl_LayoutAudit()
    {
        var vm = new MainViewModel();
        var control = new CivitaiTabControl { DataContext = vm.Civitai };
        var window = new Window { Content = control, Width = 1280, Height = 800 };
        try
        {
            window.Show();

            var auditor = new LayoutAuditor();
            var options = new AuditOptions
            {
                CheckBoundaryOverflow = true,
                CheckSiblingCollisions = true,
                CheckTouchErgonomics = false,
                CheckTextClipping = false
            };

            var report = auditor.Audit(control, options);
            _output.WriteLine($"CivitaiTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            Assert.NotNull(report);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HuggingFaceTabControl_LayoutAudit()
    {
        var vm = new MainViewModel();
        var control = new HuggingFaceTabControl { DataContext = vm.HuggingFace };
        var window = new Window { Content = control, Width = 1280, Height = 800 };
        try
        {
            window.Show();

            var auditor = new LayoutAuditor();
            var report = auditor.Audit(control);

            _output.WriteLine($"HuggingFaceTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            Assert.NotNull(report);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OllamaModelsTabControl_LayoutAudit()
    {
        var vm = new MainViewModel();
        var control = new OllamaModelsTabControl { DataContext = vm.Ollama };
        var window = new Window { Content = control, Width = 1280, Height = 800 };
        try
        {
            window.Show();

            var auditor = new LayoutAuditor();
            var report = auditor.Audit(control);

            _output.WriteLine($"OllamaModelsTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            Assert.NotNull(report);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SettingsTabControl_LayoutAudit()
    {
        var vm = new MainViewModel();
        var control = new SettingsTabControl { DataContext = vm.Settings };
        var window = new Window { Content = control, Width = 1280, Height = 800 };
        try
        {
            window.Show();

            var auditor = new LayoutAuditor();
            var report = auditor.Audit(control);

            _output.WriteLine($"SettingsTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            Assert.NotNull(report);
        }
        finally
        {
            window.Close();
        }
    }
}
