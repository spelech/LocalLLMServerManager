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
}
