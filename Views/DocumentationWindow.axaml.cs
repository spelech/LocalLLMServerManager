using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Views;

public partial class DocumentationWindow : Window
{
    public DocumentationWindow()
    {
        InitializeComponent();
        PositionChanged += OnWindowPositionChanged;
        Opened += (s, e) => UpdateSnapVisuals();
    }

    public DocumentationWindow(DocumentationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (WindowSnapManager.Instance.IsSnapped(this))
        {
            if (WindowSnapManager.Instance.CheckDragDetachment(this))
            {
                UpdateSnapVisuals();
            }
        }
        else
        {
            if (WindowSnapManager.Instance.CheckProximitySnap(this))
            {
                UpdateSnapVisuals();
            }
        }
    }

    private void OnSnapToggleClicked(object? sender, RoutedEventArgs e)
    {
        WindowSnapManager.Instance.ToggleSnap(this);
        UpdateSnapVisuals();
    }

    public void UpdateSnapVisuals()
    {
        bool isSnapped = WindowSnapManager.Instance.IsSnapped(this);
        var snapText = this.FindControl<TextBlock>("SnapButtonText");
        var snapBtn = this.FindControl<Button>("SnapToggleButton");
        if (snapText != null)
        {
            snapText.Text = isSnapped ? "Attached" : "Snap to Side";
        }
        if (snapBtn != null)
        {
            if (isSnapped)
            {
                if (!snapBtn.Classes.Contains("snapped")) snapBtn.Classes.Add("snapped");
            }
            else
            {
                snapBtn.Classes.Remove("snapped");
            }
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        var maxBtn = this.FindControl<Button>("MaximizeButton");
        if (maxBtn != null)
        {
            maxBtn.Content = WindowState == WindowState.Maximized ? "❐" : "🗖";
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
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
