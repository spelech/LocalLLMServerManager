using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class DocumentationFloatingOverlay : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragging = false;

    public DocumentationFloatingOverlay()
    {
        InitializeComponent();

        var pillHandle = this.FindControl<TextBlock>("PillDragHandle");
        if (pillHandle != null)
        {
            pillHandle.PointerPressed += OnDragHandlePointerPressed;
            pillHandle.PointerMoved += OnDragHandlePointerMoved;
            pillHandle.PointerReleased += OnDragHandlePointerReleased;
        }

        var cardHandle = this.FindControl<Border>("CardDragHandle");
        if (cardHandle != null)
        {
            cardHandle.PointerPressed += OnDragHandlePointerPressed;
            cardHandle.PointerMoved += OnDragHandlePointerMoved;
            cardHandle.PointerReleased += OnDragHandlePointerReleased;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this.Parent as Visual);
            e.Pointer.Capture(sender as IInputElement);
            e.Handled = true;
        }
    }

    private void OnDragHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && DataContext is DocumentationViewModel vm && this.Parent is Visual parentVisual)
        {
            var currentPos = e.GetPosition(parentVisual);
            var deltaX = currentPos.X - _dragStartPoint.X;
            var deltaY = currentPos.Y - _dragStartPoint.Y;

            vm.OverlayX = Math.Max(10, vm.OverlayX + deltaX);
            vm.OverlayY = Math.Max(10, vm.OverlayY + deltaY);

            _dragStartPoint = currentPos;
            e.Handled = true;
        }
    }

    private void OnDragHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
