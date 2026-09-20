using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LocalLLMServerManager.Shared.Views;

namespace LocalLLMServerManager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static void SetThemeStyle(string styleName)
    {
        if (styleName.Equals("semi", StringComparison.OrdinalIgnoreCase))
        {
            LocalLLMServerManager.Shared.Services.ThemeService.Instance.SetTheme(
                LocalLLMServerManager.Shared.Services.AppTheme.MatteCarbon);
        }
        else if (styleName.Equals("fluent", StringComparison.OrdinalIgnoreCase))
        {
            LocalLLMServerManager.Shared.Services.ThemeService.Instance.SetTheme(
                LocalLLMServerManager.Shared.Services.AppTheme.OledBlack);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LocalLLMServerManager.Shared.ViewModels.MainViewModel.EnableAutomaticPolling = true;
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = new LocalLLMServerManager.Shared.ViewModels.MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
