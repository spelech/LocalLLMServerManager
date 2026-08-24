using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class FeaturePackBannerControl : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FeaturePackBannerControl, string>(nameof(Title), "Feature Pack Available");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<FeaturePackBannerControl, string>(nameof(Description), "Optional feature pack description.");

    public static readonly StyledProperty<string> DiskSizeProperty =
        AvaloniaProperty.Register<FeaturePackBannerControl, string>(nameof(DiskSize), "0 MB");

    public static readonly StyledProperty<string> MinVramProperty =
        AvaloniaProperty.Register<FeaturePackBannerControl, string>(nameof(MinVram), "4 GB");

    public static readonly StyledProperty<ICommand?> InstallCommandProperty =
        AvaloniaProperty.Register<FeaturePackBannerControl, ICommand?>(nameof(InstallCommand));

    public static readonly StyledProperty<object?> InstallCommandParameterProperty =
        AvaloniaProperty.Register<FeaturePackBannerControl, object?>(nameof(InstallCommandParameter));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string DiskSize
    {
        get => GetValue(DiskSizeProperty);
        set => SetValue(DiskSizeProperty, value);
    }

    public string MinVram
    {
        get => GetValue(MinVramProperty);
        set => SetValue(MinVramProperty, value);
    }

    public ICommand? InstallCommand
    {
        get => GetValue(InstallCommandProperty);
        set => SetValue(InstallCommandProperty, value);
    }

    public object? InstallCommandParameter
    {
        get => GetValue(InstallCommandParameterProperty);
        set => SetValue(InstallCommandParameterProperty, value);
    }

    public FeaturePackBannerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
