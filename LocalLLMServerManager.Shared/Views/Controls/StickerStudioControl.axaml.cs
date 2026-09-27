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

public class ImageSourceConverter : IValueConverter
{
    public static readonly ImageSourceConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return new Bitmap(path);
            }
        }
        catch
        {
            // Ignore decode exceptions
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class PresetMatchConverter : IMultiValueConverter
{
    public static readonly PresetMatchConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is string id1 && values[1] is string id2)
        {
            return string.Equals(id1, id2, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

public class StageStatusBrushConverter : IValueConverter
{
    public static readonly StageStatusBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StickerPipelineStage stage && parameter is string stepStr && int.TryParse(stepStr, out int step))
        {
            if (stage == StickerPipelineStage.Failed)
                return new SolidColorBrush(Color.Parse("#da3633"));

            int current = (int)stage;
            if (stage != StickerPipelineStage.Idle)
            {
                if (stage == StickerPipelineStage.Ready)
                    return new SolidColorBrush(Color.Parse("#238636")); // Complete green
                if (current == step)
                    return new SolidColorBrush(Color.Parse("#388bfd")); // Active blue
                if (current > step)
                    return new SolidColorBrush(Color.Parse("#238636")); // Complete green
            }
        }
        return new SolidColorBrush(Color.Parse("#8b949e")); // TextMutedBrush
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class StageIconConverter : IValueConverter
{
    public static readonly StageIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StickerPipelineStage stage && parameter is string stepStr && int.TryParse(stepStr, out int step))
        {
            if (stage == StickerPipelineStage.Failed && (int)stage == step)
                return "✕";

            int current = (int)stage;
            if (stage != StickerPipelineStage.Idle)
            {
                if (stage == StickerPipelineStage.Ready)
                    return "✓";
                if (current > step)
                    return "✓";
                if (current == step)
                    return "●";
            }
        }
        return "○";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
