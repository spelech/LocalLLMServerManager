using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class EngineStudioTabControl : UserControl
{
    private MainViewModel? _boundVm;

    public EngineStudioTabControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundVm != null)
        {
            _boundVm.CopyStudioImageRequested -= OnCopyStudioImageRequested;
            _boundVm.SaveStudioImageRequested -= OnSaveStudioImageRequested;
            _boundVm.PickImageRequested -= OnPickImageRequested;
            _boundVm = null;
        }

        if (DataContext is MainViewModel vm)
        {
            _boundVm = vm;
            _boundVm.CopyStudioImageRequested += OnCopyStudioImageRequested;
            _boundVm.SaveStudioImageRequested += OnSaveStudioImageRequested;
            _boundVm.PickImageRequested += OnPickImageRequested;
        }
    }

    private async Task OnCopyStudioImageRequested(byte[] bytes)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null || bytes == null || bytes.Length == 0) return;

        try
        {
            using var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            await Avalonia.Input.Platform.ClipboardExtensions.SetBitmapAsync(topLevel.Clipboard, bitmap);
        }
        catch { }
    }

    private async Task<string?> OnSaveStudioImageRequested(byte[] bytes)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null || bytes == null || bytes.Length == 0) return null;

        try
        {
            var options = new FilePickerSaveOptions
            {
                Title = "Save Generated Image",
                DefaultExtension = ".png",
                SuggestedFileName = $"studio_render_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("PNG Image (*.png)") { Patterns = new[] { "*.png" } }
                }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file != null)
            {
                using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(bytes);
                return file.TryGetLocalPath() ?? file.Name;
            }
        }
        catch { }
        return null;
    }

    private async Task<byte[]?> OnPickImageRequested()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return null;

        try
        {
            var picturesFolder = await topLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures);
            var options = new FilePickerOpenOptions
            {
                Title = "Select Reference Image",
                AllowMultiple = false,
                SuggestedStartLocation = picturesFolder,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    FilePickerFileTypes.ImageAll,
                    new("All Files (*.*)") { Patterns = new[] { "*.*" } }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files != null && files.Count > 0)
            {
                using var stream = await files[0].OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
        catch { }
        return null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_boundVm != null)
        {
            _boundVm.CopyStudioImageRequested -= OnCopyStudioImageRequested;
            _boundVm.SaveStudioImageRequested -= OnSaveStudioImageRequested;
            _boundVm.PickImageRequested -= OnPickImageRequested;
            _boundVm = null;
        }
    }
}
