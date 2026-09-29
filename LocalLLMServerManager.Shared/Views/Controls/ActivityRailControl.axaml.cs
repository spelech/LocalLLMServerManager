using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class ActivityRailControl : UserControl
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ActivityRailControl, bool>(nameof(IsExpanded), defaultValue: false);

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private NavigationRailViewModel? _boundNavVm;

    public ActivityRailControl()
    {
        InitializeComponent();
        Width = 56;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            Width = expanded ? 200 : 56;
            if (_boundNavVm != null && _boundNavVm.IsExpanded != expanded)
            {
                _boundNavVm.IsExpanded = expanded;
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundNavVm != null)
        {
            _boundNavVm.PropertyChanged -= OnNavVmPropertyChanged;
            _boundNavVm = null;
        }

        if (DataContext is NavigationRailViewModel navVm)
        {
            _boundNavVm = navVm;
        }
        else if (DataContext != null)
        {
            var prop = DataContext.GetType().GetProperty("NavigationRail");
            if (prop?.GetValue(DataContext) is NavigationRailViewModel navFromMain)
            {
                DataContext = navFromMain;
                return;
            }
        }

        if (_boundNavVm != null)
        {
            IsExpanded = _boundNavVm.IsExpanded;
            Width = _boundNavVm.IsExpanded ? 200 : 56;
            _boundNavVm.PropertyChanged += OnNavVmPropertyChanged;
        }
    }

    private void OnNavVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationRailViewModel.IsExpanded) && _boundNavVm != null)
        {
            IsExpanded = _boundNavVm.IsExpanded;
            Width = _boundNavVm.IsExpanded ? 200 : 56;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_boundNavVm != null)
        {
            _boundNavVm.PropertyChanged -= OnNavVmPropertyChanged;
            _boundNavVm = null;
        }
    }
}
