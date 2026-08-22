using System;

namespace LocalLLMServerManager.Shared.Services;

public enum AppTheme
{
    MatteCarbon,
    OledBlack,
    Light
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void SetTheme(AppTheme theme);
    event EventHandler<AppTheme>? ThemeChanged;
}
