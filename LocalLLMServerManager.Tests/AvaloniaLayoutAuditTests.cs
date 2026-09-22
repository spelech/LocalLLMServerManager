using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    private static AuditOptions CreateConfiguredAuditOptions(bool checkTouchErgonomics = true, bool checkTextClipping = true)
    {
        var options = new AuditOptions
        {
            CheckBoundaryOverflow = true,
            CheckSiblingCollisions = true,
            CheckTouchErgonomics = checkTouchErgonomics,
            CheckTextClipping = checkTextClipping
        };
        options.IgnoredControlTypes.Add(typeof(ScrollBar));
        options.IgnoredControlTypes.Add(typeof(Track));
        options.IgnoredControlTypes.Add(typeof(RepeatButton));
        return options;
    }

    private static List<LayoutViolation> FilterAppViolations(AuditReport report)
    {
        return report.Violations.Where(v =>
        {
            var path = v.VisualPath;
            var firstSeparator = path.IndexOf(" > ", StringComparison.Ordinal);
            if (firstSeparator > 0 && path.Substring(firstSeparator + 3).StartsWith("ContentPresenter#PART_ContentPresenter > "))
            {
                path = path.Substring(firstSeparator + 3 + "ContentPresenter#PART_ContentPresenter > ".Length);
            }

            return !path.Contains("ScrollBar") &&
                   !path.Contains("PART_") &&
                   !path.Contains("FocusTarget") &&
                   !path.Contains("TrackBackground") &&
                   v.Severity == ViolationSeverity.Error;
        }).ToList();
    }

    [AvaloniaFact]
    public void MainWindow_ResponsiveLayoutAudit()
    {
        var window = new MainWindow();
        try
        {
            var runner = new ResponsiveAuditRunner();
            var options = CreateConfiguredAuditOptions();
            var breakpoints = new[]
            {
                StandardBreakpoints.Desktop1440p,
                StandardBreakpoints.Desktop1080p,
                StandardBreakpoints.Desktop720p,
                StandardBreakpoints.TabletiPad
            };

            var responsiveReport = runner.Run(window, breakpoints, options);

            _output.WriteLine($"MainWindow Responsive Audit - AllPassed: {responsiveReport.AllPassed}, TotalViolations: {responsiveReport.TotalViolations}");
            foreach (var (bp, report) in responsiveReport.BreakpointReports)
            {
                _output.WriteLine($"--- Breakpoint {bp.Name} ({bp.Width}x{bp.Height}): Health={report.HealthScore}/100, Violations={report.Violations.Count} ---");
                if (report.Violations.Count > 0)
                {
                    _output.WriteLine(report.ToDetailedReport());
                }

                var appViolations = FilterAppViolations(report);
                Assert.Empty(appViolations);
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
    public void AiAssistantTabControl_LayoutAudit()
    {
        var breakpoints = new (string Name, double Width, double Height)[]
        {
            ("Companion", 440, 700),
            ("Drawer", 480, 700),
            ("Desktop", 1280, 800)
        };

        foreach (var bp in breakpoints)
        {
            foreach (var isSetupVisible in new[] { false, true })
            {
                var vm = new MainViewModel();
                vm.Assistant.IsSetupCardVisible = isSetupVisible;
                var control = new AiAssistantTabControl { DataContext = vm.Assistant };
                var window = new Window { Content = control, Width = bp.Width, Height = bp.Height };
                try
                {
                    window.Show();

                    var auditor = new LayoutAuditor();
                    var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: false);

                    var report = auditor.Audit(control, options);
                    _output.WriteLine($"AiAssistantTabControl Audit ({bp.Name} {bp.Width}x{bp.Height}, Setup={isSetupVisible}) - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
                    if (report.Violations.Count > 0)
                    {
                        _output.WriteLine(report.ToDetailedReport());
                    }

                    var appViolations = FilterAppViolations(report);
                    Assert.Empty(appViolations);
                }
                finally
                {
                    window.Close();
                }
            }
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
            var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: false);

            var report = auditor.Audit(control, options);
            _output.WriteLine($"CivitaiTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            var appViolations = FilterAppViolations(report);
            Assert.NotNull(report);
            Assert.Empty(appViolations);
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
            var options = CreateConfiguredAuditOptions();
            var report = auditor.Audit(control, options);

            _output.WriteLine($"HuggingFaceTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            var appViolations = FilterAppViolations(report);
            Assert.NotNull(report);
            Assert.Empty(appViolations);
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
            var options = CreateConfiguredAuditOptions();
            var report = auditor.Audit(control, options);

            _output.WriteLine($"OllamaModelsTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            var appViolations = FilterAppViolations(report);
            Assert.NotNull(report);
            Assert.Empty(appViolations);
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
            var options = CreateConfiguredAuditOptions();
            var report = auditor.Audit(control, options);

            _output.WriteLine($"SettingsTabControl Audit - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
            if (report.Violations.Count > 0)
            {
                _output.WriteLine(report.ToDetailedReport());
            }

            var appViolations = FilterAppViolations(report);
            Assert.NotNull(report);
            Assert.Empty(appViolations);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DocumentationTabControl_LayoutAudit()
    {
        var testCases = new (string Name, double Width, double Height, bool IsDetailActive)[]
        {
            ("Companion Master", 440, 700, false),
            ("Companion Detail", 440, 700, true),
            ("Desktop Wide", 1280, 800, false),
            ("Desktop Wide Detail", 1280, 800, true)
        };

        foreach (var tc in testCases)
        {
            var vm = new MainViewModel();
            vm.Documentation.IsDetailActive = tc.IsDetailActive;
            var control = new DocumentationTabControl { DataContext = vm.Documentation };
            var window = new Window { Content = control, Width = tc.Width, Height = tc.Height };
            try
            {
                window.Show();

                var auditor = new LayoutAuditor();
                var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: false);

                var report = auditor.Audit(control, options);
                _output.WriteLine($"DocumentationTabControl Audit ({tc.Name} {tc.Width}x{tc.Height}, IsDetailActive={tc.IsDetailActive}) - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
                if (report.Violations.Count > 0)
                {
                    _output.WriteLine(report.ToDetailedReport());
                }

                var appViolations = FilterAppViolations(report);
                Assert.Empty(appViolations);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
