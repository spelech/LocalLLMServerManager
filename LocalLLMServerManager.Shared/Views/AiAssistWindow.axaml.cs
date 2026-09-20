using Avalonia.Controls;
using Avalonia.Interactivity;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views;

public partial class AiAssistWindow : Window
{
    public AiAssistWindow()
    {
        InitializeComponent();
    }

    public AiAssistWindow(AiAssistantViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
