using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Views;

public partial class DocumentationWindow : Window
{
    public DocumentationWindow()
    {
        InitializeComponent();
    }

    public DocumentationWindow(DocumentationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnPinClicked(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        var pinText = this.FindControl<TextBlock>("PinText");
        if (pinText != null)
        {
            pinText.Text = Topmost ? "📌 Pinned (Topmost)" : "📌 Pin to Top";
            pinText.Foreground = Topmost ? Brushes.LimeGreen : Brushes.Gray;
        }
    }
}
