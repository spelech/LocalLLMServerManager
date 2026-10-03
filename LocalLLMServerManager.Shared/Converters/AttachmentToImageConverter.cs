using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using LocalLLMServerManager.Shared.Models;

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
