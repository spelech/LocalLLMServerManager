using Avalonia.Controls;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide window to system tray when user clicks X close button
        e.Cancel = true;
        Hide();
    }
}
