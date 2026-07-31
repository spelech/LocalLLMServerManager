using Avalonia.Controls;
using LocalLLMServerManager.ViewModels;

namespace LocalLLMServerManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide window to system tray when user clicks X close button
        e.Cancel = true;
        Hide();
    }
}
