using Avalonia;
using Avalonia.Headless;
using LocalLLMServerManager;
using LocalLLMServerManager.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace LocalLLMServerManager.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            });
}
