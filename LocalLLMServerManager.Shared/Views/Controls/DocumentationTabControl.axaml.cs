using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class DocumentationTabControl : UserControl
{
    private DocumentationViewModel? _subscribedVm;

    public DocumentationTabControl()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is DocumentationViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
        }
        else
        {
            _subscribedVm = null;
        }

        UpdateLayoutMode(Bounds.Width > 0 ? Bounds.Width : Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentationViewModel.IsDetailActive))
        {
            UpdateLayoutMode(Bounds.Width > 0 ? Bounds.Width : Width);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width) && availableSize.Width > 0)
        {
            UpdateLayoutMode(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        if (LayoutGrid == null || TopicsBorder == null || DetailBorder == null)
            return;

        bool isNarrow = width > 0 && width < 600;
        bool isDetailActive = _subscribedVm?.IsDetailActive ?? false;

        if (isNarrow)
        {
            // Narrow mode (< 600px):
            // Adaptive master-detail layout
            LayoutGrid.ColumnDefinitions = new ColumnDefinitions("*");

            if (!isDetailActive)
            {
                // Master view: Topics list cards full width
                TopicsBorder.IsVisible = true;
                Grid.SetColumn(TopicsBorder, 0);
                TopicsBorder.Margin = new Thickness(0);

                DetailBorder.IsVisible = false;
                if (BackToTopicsButton != null)
                {
                    BackToTopicsButton.IsVisible = false;
                }
            }
            else
            {
                // Detail view: Detail reader full width with Back button
                TopicsBorder.IsVisible = false;

                DetailBorder.IsVisible = true;
                Grid.SetColumn(DetailBorder, 0);
                DetailBorder.Margin = new Thickness(0);

                if (BackToTopicsButton != null)
                {
                    BackToTopicsButton.IsVisible = true;
                }
            }
        }
        else
        {
            // Wide mode (>= 600px):
            // Two-column side-by-side view (Left 280px, Right *)
            LayoutGrid.ColumnDefinitions = new ColumnDefinitions("280, *");

            TopicsBorder.IsVisible = true;
            Grid.SetColumn(TopicsBorder, 0);
            TopicsBorder.Margin = new Thickness(0, 0, 12, 0);

            DetailBorder.IsVisible = true;
            Grid.SetColumn(DetailBorder, 1);
            DetailBorder.Margin = new Thickness(0);

            if (BackToTopicsButton != null)
            {
                BackToTopicsButton.IsVisible = false;
            }
        }
    }
}
