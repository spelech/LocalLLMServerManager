using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Views.Controls;

public class ImageSourceConverter : IValueConverter
{
    public static readonly ImageSourceConverter Instance = new();
    private static readonly ConditionalWeakTable<byte[], Bitmap> BitmapCache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                return BitmapCache.GetValue(bytes, static b =>
                {
                    using var ms = new MemoryStream(b);
                    return new Bitmap(ms);
                });
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

    [ThreadStatic] private static SolidColorBrush? _completedBrush;
    [ThreadStatic] private static SolidColorBrush? _runningBrush;
    [ThreadStatic] private static SolidColorBrush? _errorBrush;
    [ThreadStatic] private static SolidColorBrush? _idleBrush;

    public static SolidColorBrush CompletedBrush => _completedBrush ??= new(Color.Parse("#238636"));
    public static SolidColorBrush RunningBrush => _runningBrush ??= new(Color.Parse("#388bfd"));
    public static SolidColorBrush ErrorBrush => _errorBrush ??= new(Color.Parse("#da3633"));
    public static SolidColorBrush IdleBrush => _idleBrush ??= new(Color.Parse("#8b949e"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StickerPipelineStage stage && parameter is string stepStr && int.TryParse(stepStr, out int step))
        {
            if (stage == StickerPipelineStage.Failed)
                return ErrorBrush;

            int current = (int)stage;
            if (stage != StickerPipelineStage.Idle)
            {
                if (stage == StickerPipelineStage.Ready)
                    return CompletedBrush; // Complete green
                if (current == step)
                    return RunningBrush; // Active blue
                if (current > step)
                    return CompletedBrush; // Complete green
            }
        }
        return IdleBrush; // TextMutedBrush
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
