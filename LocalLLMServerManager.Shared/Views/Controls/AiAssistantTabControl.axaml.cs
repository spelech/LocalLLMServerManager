using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public class AttachmentToImageConverter : IValueConverter
{
    public static readonly AttachmentToImageConverter Instance = new();
    private static readonly ConditionalWeakTable<AiChatMessageAttachment, Bitmap> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AiChatMessageAttachment attachment)
        {
            if (_cache.TryGetValue(attachment, out var cached))
            {
                return cached;
            }

            Bitmap? bitmap = null;
            if (attachment.RawBytes != null && attachment.RawBytes.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(attachment.RawBytes);
                    bitmap = new Bitmap(ms);
                }
                catch
                {
                    // Invalid image bytes
                }
            }

            if (bitmap == null && !string.IsNullOrWhiteSpace(attachment.Base64Data))
            {
                try
                {
                    var bytes = System.Convert.FromBase64String(attachment.Base64Data);
                    using var ms = new MemoryStream(bytes);
                    bitmap = new Bitmap(ms);
                }
                catch
                {
                    // Invalid base64
                }
            }

            if (bitmap != null)
            {
                _cache.AddOrUpdate(attachment, bitmap);
            }

            return bitmap;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public partial class AiAssistantTabControl : UserControl
{
    public AiAssistantTabControl()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is AiAssistantViewModel vm && vm.IsDrawerOpen && vm.AvailableModelCapabilities.Count <= 1)
        {
            _ = vm.LoadAvailableModelsAsync();
        }
    }

    public async void OnAttachFileButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AiAssistantViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        try
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Attach Image",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    FilePickerFileTypes.ImageAll,
                    new("All Files (*.*)") { Patterns = new[] { "*.*" } }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    try
                    {
                        using var stream = await file.OpenReadAsync();
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        var bytes = ms.ToArray();
                        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                        var contentType = ext switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".webp" => "image/webp",
                            ".gif" => "image/gif",
                            _ => "image/png"
                        };

                        vm.AddStagedAttachment(new AiChatMessageAttachment
                        {
                            FileName = file.Name,
                            ContentType = contentType,
                            Base64Data = Convert.ToBase64String(bytes),
                            RawBytes = bytes
                        });
                    }
                    catch
                    {
                        // Ignore unreadable individual file
                    }
                }
            }
        }
        catch
        {
            // Ignore file picker cancellation or error
        }
    }

    public async void OnPromptTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null && DataContext is AiAssistantViewModel vm)
            {
                try
                {
                    var dataTransfer = await topLevel.Clipboard.TryGetDataAsync();
                    if (dataTransfer != null)
                    {
                        // 1. Check for clipboard bitmap
                        var bitmap = await dataTransfer.TryGetBitmapAsync();
                        if (bitmap != null)
                        {
                            using var ms = new MemoryStream();
                            bitmap.Save(ms);
                            var bytes = ms.ToArray();
                            if (bytes.Length > 0)
                            {
                                vm.PasteImageBytes(bytes, "image/png");
                                e.Handled = true;
                                return;
                            }
                        }

                        // 2. Check for clipboard files (e.g. copied image file from Explorer)
                        var files = await dataTransfer.TryGetFilesAsync();
                        if (files != null && files.Length > 0)
                        {
                            bool pasted = false;
                            foreach (var item in files)
                            {
                                if (item is IStorageFile file)
                                {
                                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                                    if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp")
                                    {
                                        using var s = await file.OpenReadAsync();
                                        using var ms = new MemoryStream();
                                        await s.CopyToAsync(ms);
                                        var bytes = ms.ToArray();
                                        var mime = ext switch
                                        {
                                            ".jpg" or ".jpeg" => "image/jpeg",
                                            ".webp" => "image/webp",
                                            ".gif" => "image/gif",
                                            _ => "image/png"
                                        };
                                        vm.PasteImageBytes(bytes, mime);
                                        pasted = true;
                                    }
                                }
                            }

                            if (pasted)
                            {
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    // If clipboard access fails, let default paste behavior proceed
                }
            }
        }
    }
}
