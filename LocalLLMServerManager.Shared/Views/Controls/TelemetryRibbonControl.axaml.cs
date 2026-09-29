using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class TelemetryRibbonControl : UserControl
{
    public TelemetryRibbonControl()
    {
        InitializeComponent();
        Height = 34;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext != null && DataContext is not TelemetryViewModel)
        {
            var prop = DataContext.GetType().GetProperty("Telemetry");
            if (prop?.GetValue(DataContext) is TelemetryViewModel tvm)
            {
                DataContext = tvm;
            }
        }
    }
}
