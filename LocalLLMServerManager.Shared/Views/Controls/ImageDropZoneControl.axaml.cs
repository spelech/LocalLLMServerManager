using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class ImageDropZoneControl : UserControl
{
    public static readonly StyledProperty<string?> ImagePathProperty =
        AvaloniaProperty.Register<ImageDropZoneControl, string?>(nameof(ImagePath));

    public static readonly StyledProperty<byte[]?> ImageBytesProperty =
        AvaloniaProperty.Register<ImageDropZoneControl, byte[]?>(nameof(ImageBytes));

    public static readonly StyledProperty<Bitmap?> PreviewBitmapProperty =
        AvaloniaProperty.Register<ImageDropZoneControl, Bitmap?>(nameof(PreviewBitmap));

    public static readonly StyledProperty<bool> HasImageProperty =
        AvaloniaProperty.Register<ImageDropZoneControl, bool>(nameof(HasImage), defaultValue: false);

    public static readonly StyledProperty<bool> IsDragOverProperty =
        AvaloniaProperty.Register<ImageDropZoneControl, bool>(nameof(IsDragOver), defaultValue: false);

    public static readonly StyledProperty<string?> DisplayFileNameProperty =
        AvaloniaProperty.Register<ImageDropZoneControl, string?>(nameof(DisplayFileName));

    public string? ImagePath
    {
        get => GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public byte[]? ImageBytes
    {
        get => GetValue(ImageBytesProperty);
        set => SetValue(ImageBytesProperty, value);
    }

    public Bitmap? PreviewBitmap
    {
        get => GetValue(PreviewBitmapProperty);
        set => SetValue(PreviewBitmapProperty, value);
    }

    public bool HasImage
    {
        get => GetValue(HasImageProperty);
        set => SetValue(HasImageProperty, value);
    }

    public bool IsDragOver
    {
        get => GetValue(IsDragOverProperty);
        set => SetValue(IsDragOverProperty, value);
    }

    public string? DisplayFileName
    {
        get => GetValue(DisplayFileNameProperty);
        set => SetValue(DisplayFileNameProperty, value);
    }

    private StickerStudioViewModel? _boundVm;

    public ImageDropZoneControl()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
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
            _boundVm.PropertyChanged -= OnVmPropertyChanged;
            _boundVm = null;
        }

        if (DataContext is StickerStudioViewModel vm)
        {
            _boundVm = vm;
            _boundVm.PropertyChanged += OnVmPropertyChanged;
            SyncFromViewModel();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StickerStudioViewModel.InputImagePath) or
            nameof(StickerStudioViewModel.InputImageBytes) or
            nameof(StickerStudioViewModel.HasInputImage))
        {
            SyncFromViewModel();
        }
    }

    private void SyncFromViewModel()
    {
        if (_boundVm == null) return;

        ImagePath = _boundVm.InputImagePath;
        ImageBytes = _boundVm.InputImageBytes;
        UpdatePreview(ImagePath, ImageBytes);
    }

    public void SetImage(string? path, byte[]? bytes)
    {
        ImagePath = path;
        ImageBytes = bytes;
        UpdatePreview(path, bytes);

        if (_boundVm != null)
        {
            _boundVm.SetInputImage(path, bytes);
        }
    }

    public void ClearImage()
    {
        ImagePath = null;
        ImageBytes = null;
        PreviewBitmap?.Dispose();
        PreviewBitmap = null;
        HasImage = false;
        DisplayFileName = null;

        if (_boundVm != null)
        {
            _boundVm.ClearInput();
        }
    }

    private void UpdatePreview(string? path, byte[]? bytes)
    {
        try
        {
            if (bytes != null && bytes.Length > 0)
            {
                using var ms = new MemoryStream(bytes);
                PreviewBitmap = new Bitmap(ms);
                HasImage = true;
                DisplayFileName = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "Reference Image";
                return;
            }

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                PreviewBitmap = new Bitmap(path);
                HasImage = true;
                DisplayFileName = Path.GetFileName(path);
                return;
            }
        }
        catch
        {
            // Invalid image stream or file
        }

        HasImage = !string.IsNullOrEmpty(path) || (bytes != null && bytes.Length > 0);
        DisplayFileName = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : null;
        if (!HasImage)
        {
            PreviewBitmap = null;
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        IsDragOver = true;
        e.DragEffects = DragDropEffects.Copy;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        IsDragOver = true;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        IsDragOver = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        IsDragOver = false;

        try
        {
            IStorageItem[]? files = null;
            if (e.DataTransfer is IAsyncDataTransfer asyncTransfer)
            {
                files = await asyncTransfer.TryGetFilesAsync();
            }
            else
            {
                files = e.DataTransfer.TryGetFiles();
            }

            if (files != null && files.Length > 0)
            {
                foreach (var item in files)
                {
                    if (item is IStorageFile file)
                    {
                        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                        if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp")
                        {
                            var localPath = file.TryGetLocalPath();
                            using var stream = await file.OpenReadAsync();
                            using var ms = new MemoryStream();
                            await stream.CopyToAsync(ms);
                            var bytes = ms.ToArray();
                            SetImage(localPath ?? file.Name, bytes);
                            return;
                        }
                    }
                }
            }

            Bitmap? bitmap = null;
            if (e.DataTransfer is IAsyncDataTransfer asyncTransferBitmap)
            {
                bitmap = await asyncTransferBitmap.TryGetBitmapAsync();
            }
            else
            {
                bitmap = e.DataTransfer.TryGetBitmap();
            }

            if (bitmap != null)
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms);
                var bytes = ms.ToArray();
                if (bytes.Length > 0)
                {
                    SetImage("dropped_image.png", bytes);
                }
            }
        }
        catch
        {
            // Fall back gracefully
        }
    }

    public async void OnBrowseFilesClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        try
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Select Reference Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    FilePickerFileTypes.ImageAll,
                    new("Supported Images (*.png;*.jpg;*.jpeg;*.webp)")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" }
                    },
                    new("All Files (*.*)") { Patterns = new[] { "*.*" } }
                }
            };

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result != null && result.Count > 0)
            {
                var file = result[0];
                var localPath = file.TryGetLocalPath();
                using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                SetImage(localPath ?? file.Name, bytes);
            }
        }
        catch
        {
            // Ignore file picker cancellation or error
        }
    }

    public void OnRemoveImageClick(object? sender, RoutedEventArgs e)
    {
        ClearImage();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnVmPropertyChanged;
            _boundVm = null;
        }
    }
}
