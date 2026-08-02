using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
