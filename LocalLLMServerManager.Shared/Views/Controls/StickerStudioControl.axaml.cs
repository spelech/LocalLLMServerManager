using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class StickerStudioControl : UserControl
{
    private StickerStudioViewModel? _boundVm;

    public StickerStudioControl()
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
            _boundVm.CopyToClipboardRequested -= OnCopyToClipboardRequested;
            _boundVm.SaveFileRequested -= OnSaveFileRequested;
            _boundVm = null;
        }

        if (DataContext is StickerStudioViewModel vm)
        {
            _boundVm = vm;
            _boundVm.CopyToClipboardRequested += OnCopyToClipboardRequested;
            _boundVm.SaveFileRequested += OnSaveFileRequested;
        }
    }

    private async Task OnCopyToClipboardRequested(byte[] bytes)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null || bytes == null || bytes.Length == 0) return;

        try
        {
            using var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            await Avalonia.Input.Platform.ClipboardExtensions.SetBitmapAsync(topLevel.Clipboard, bitmap);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    private async Task<string?> OnSaveFileRequested(byte[] bytes)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null || bytes == null || bytes.Length == 0) return null;

        try
        {
            var options = new FilePickerSaveOptions
            {
                Title = "Save Sticker PNG",
                DefaultExtension = ".png",
                SuggestedFileName = "sticker.png",
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
        catch
        {
            // Ignore save errors
        }

        return null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_boundVm != null)
        {
            _boundVm.CopyToClipboardRequested -= OnCopyToClipboardRequested;
            _boundVm.SaveFileRequested -= OnSaveFileRequested;
            _boundVm = null;
        }
    }
}

