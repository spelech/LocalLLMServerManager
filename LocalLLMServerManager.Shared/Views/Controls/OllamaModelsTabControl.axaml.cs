using Avalonia.Controls;
using Avalonia.Input;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class OllamaModelsTabControl : UserControl
{
    public OllamaModelsTabControl()
    {
        InitializeComponent();
    }

    private void OnModelCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.DataContext is OllamaModelItem item && DataContext is OllamaLibraryViewModel vm)
        {
            vm.SelectModel(item);
        }
    }
}
