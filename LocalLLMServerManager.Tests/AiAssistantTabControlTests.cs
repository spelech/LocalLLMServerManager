using System;
using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AiAssistantTabControlTests
{
    // Minimal valid 1x1 PNG data
    private const string Base641x1Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    [AvaloniaFact]
    public void AiAssistantTabControl_InstantiatesSuccessfully()
    {
        var control = new AiAssistantTabControl();
        Assert.NotNull(control);

        var vm = new AiAssistantViewModel();
        control.DataContext = vm;
        Assert.Same(vm, control.DataContext);
    }

    [AvaloniaFact]
    public void AttachmentToImageConverter_NullOrInvalid_ReturnsNull()
    {
        var converter = AttachmentToImageConverter.Instance;

        Assert.Null(converter.Convert(null, typeof(Bitmap), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert("not-valid-base64!!!", typeof(Bitmap), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert(12345, typeof(Bitmap), null, CultureInfo.InvariantCulture));

        var corruptAttachment = new AiChatMessageAttachment
        {
            FileName = "bad.png",
            Base64Data = "not-valid-base64!!!",
            RawBytes = null
        };
        Assert.Null(converter.Convert(corruptAttachment, typeof(Bitmap), null, CultureInfo.InvariantCulture));

        var emptyAttachment = new AiChatMessageAttachment
        {
            FileName = "empty.png",
            Base64Data = "",
            RawBytes = null
        };
        Assert.Null(converter.Convert(emptyAttachment, typeof(Bitmap), null, CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public void AttachmentToImageConverter_ValidPngBytes_ReturnsBitmapAndCaches()
    {
        var converter = AttachmentToImageConverter.Instance;
        var bytes = Convert.FromBase64String(Base641x1Png);

        var attachment = new AiChatMessageAttachment
        {
            FileName = "test.png",
            ContentType = "image/png",
            RawBytes = bytes
        };

        var result1 = converter.Convert(attachment, typeof(Bitmap), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result1);
        Assert.IsAssignableFrom<Bitmap>(result1);

        // Second call should return cached instance
        var result2 = converter.Convert(attachment, typeof(Bitmap), null, CultureInfo.InvariantCulture);
        Assert.Same(result1, result2);
    }

    [AvaloniaFact]
    public void AttachmentToImageConverter_ValidBase64_ReturnsBitmap()
    {
        var converter = AttachmentToImageConverter.Instance;
        var attachment = new AiChatMessageAttachment
        {
            FileName = "pixel.png",
            ContentType = "image/png",
            Base64Data = Base641x1Png
        };

        var result = converter.Convert(attachment, typeof(Bitmap), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        Assert.IsAssignableFrom<Bitmap>(result);
    }

    [Fact]
    public void AttachmentToImageConverter_ConvertBack_ThrowsNotSupportedException()
    {
        var converter = AttachmentToImageConverter.Instance;
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public void AiAssistantTabControl_EventHandlers_HandleNullTopLevelSafely()
    {
        var control = new AiAssistantTabControl();
        control.DataContext = new AiAssistantViewModel();

        // Calling click without visual tree / storage provider attached must not throw
        control.OnAttachFileButtonClick(null, new RoutedEventArgs());

        // Calling key down without visual tree / clipboard attached must not throw
        var e = new KeyEventArgs
        {
            Key = Key.V,
            KeyModifiers = KeyModifiers.Control,
            RoutedEvent = InputElement.KeyDownEvent
        };
        control.OnPromptTextBoxKeyDown(null, e);
        Assert.False(e.Handled);
    }
}
