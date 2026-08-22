using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace LocalLLMServerManager.Shared.Services;

public class ThemeService : IThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private readonly IResourceDictionary? _resourceDictionary;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.MatteCarbon;

    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService(IResourceDictionary? resourceDictionary = null)
    {
        _resourceDictionary = resourceDictionary;
    }

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;

        var resources = _resourceDictionary ?? Application.Current?.Resources;
        if (resources != null)
        {
            ApplyThemeResources(resources, theme);
        }

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        ThemeChanged?.Invoke(this, theme);
    }

    public static void ApplyThemeResources(IResourceDictionary resources, AppTheme theme)
    {
        Color bgDark, bgSurface, bgCard, border, textMain, textMuted, primary, secondary, accent;

        switch (theme)
        {
            case AppTheme.OledBlack:
                bgDark = Color.Parse("#000000");
                bgSurface = Color.Parse("#121212");
                bgCard = Color.Parse("#18181b");
                border = Color.Parse("#27272a");
                textMain = Color.Parse("#f4f4f5");
                textMuted = Color.Parse("#71717a");
                primary = Color.Parse("#3b82f6");
                secondary = Color.Parse("#60a5fa");
                accent = Color.Parse("#60a5fa");
                break;

            case AppTheme.Light:
                bgDark = Color.Parse("#ffffff");
                bgSurface = Color.Parse("#f6f8fa");
                bgCard = Color.Parse("#ffffff");
                border = Color.Parse("#d0d7de");
                textMain = Color.Parse("#1f2328");
                textMuted = Color.Parse("#656d76");
                primary = Color.Parse("#0969da");
                secondary = Color.Parse("#218bff");
                accent = Color.Parse("#218bff");
                break;

            case AppTheme.MatteCarbon:
            default:
                bgDark = Color.Parse("#0d1117");
                bgSurface = Color.Parse("#161b22");
                bgCard = Color.Parse("#1c2128");
                border = Color.Parse("#30363d");
                textMain = Color.Parse("#f0f6fc");
                textMuted = Color.Parse("#8b949e");
                primary = Color.Parse("#388bfd");
                secondary = Color.Parse("#58a6ff");
                accent = Color.Parse("#79c0ff");
                break;
        }

        resources["BgDarkColor"] = bgDark;
        resources["BgSurfaceColor"] = bgSurface;
        resources["BgCardColor"] = bgCard;
        resources["BorderColor"] = border;
        resources["TextMainColor"] = textMain;
        resources["TextMutedColor"] = textMuted;
        resources["PrimaryColor"] = primary;
        resources["SecondaryColor"] = secondary;
        resources["AccentColor"] = accent;

        SetOrUpdateBrush(resources, "BgDarkBrush", bgDark);
        SetOrUpdateBrush(resources, "BgSurfaceBrush", bgSurface);
        SetOrUpdateBrush(resources, "BgCardBrush", bgCard);
        SetOrUpdateBrush(resources, "GlassBorderBrush", border);
        SetOrUpdateBrush(resources, "BorderColorBrush", border);
        SetOrUpdateBrush(resources, "BorderGlowBrush", primary, 0.4);
        SetOrUpdateBrush(resources, "TextMainBrush", textMain);
        SetOrUpdateBrush(resources, "TextMutedBrush", textMuted);
        SetOrUpdateBrush(resources, "PrimaryBrush", primary);
        SetOrUpdateBrush(resources, "SecondaryBrush", secondary);
        SetOrUpdateBrush(resources, "AccentBrush", accent);
        SetOrUpdateBrush(resources, "GlassBackgroundBrush", bgDark);
        SetOrUpdateBrush(resources, "PrimaryGradientBrush", primary);
    }

    private static void SetOrUpdateBrush(IResourceDictionary resources, string key, Color color, double opacity = 1.0)
    {
        if (resources.TryGetResource(key, null, out var existing) && existing is SolidColorBrush existingBrush)
        {
            existingBrush.Color = color;
            existingBrush.Opacity = opacity;
            resources[key] = existingBrush;
        }
        else if (resources.TryGetValue(key, out var dictVal) && dictVal is SolidColorBrush dictBrush)
        {
            dictBrush.Color = color;
            dictBrush.Opacity = opacity;
            resources[key] = dictBrush;
        }
        else
        {
            resources[key] = new SolidColorBrush(color) { Opacity = opacity };
        }
    }
}
