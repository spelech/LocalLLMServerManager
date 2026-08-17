using System;
using Avalonia;
using Avalonia.Controls;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class SettingsTabControl : UserControl
{
    public SettingsTabControl()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateStorageProvider();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateStorageProvider();
    }

    private void UpdateStorageProvider()
    {
        if (DataContext is SettingsViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                vm.StorageProvider = topLevel.StorageProvider;
            }
        }
    }
}
