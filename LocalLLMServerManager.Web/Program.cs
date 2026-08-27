using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using LocalLLMServerManager;
using LocalLLMServerManager.Shared.ViewModels;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    [JSImport("globalThis.getOrigin")]
    internal static partial string GetBrowserOrigin();

    private static async Task Main(string[] args)
    {
        try
        {
            if (OperatingSystem.IsBrowser())
            {
                var origin = GetBrowserOrigin();
                if (!string.IsNullOrWhiteSpace(origin))
                {
                    origin = origin.TrimEnd('/');
                    MainViewModel.BrowserOrigin = origin;
                    if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                    {
                        MainViewModel.DefaultHttpClient = new HttpClient { BaseAddress = originUri };
                    }
                }
            }
        }
        catch { }

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
