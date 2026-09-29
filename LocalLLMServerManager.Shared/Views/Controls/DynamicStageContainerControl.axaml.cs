using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class DynamicStageContainerControl : UserControl
{
    public DynamicStageContainerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
