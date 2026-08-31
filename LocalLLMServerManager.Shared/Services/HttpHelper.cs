using System;
using System.Net.Http;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Services;

public static class HttpHelper
{
    public static HttpClient CreateClient(string? apiBase = null)
    {
        var client = new HttpClient();
        if (!string.IsNullOrWhiteSpace(apiBase) && Uri.TryCreate(apiBase, UriKind.Absolute, out var baseUri))
        {
            client.BaseAddress = baseUri;
        }
        else if (MainViewModel.DefaultHttpClient.BaseAddress != null)
        {
            client.BaseAddress = MainViewModel.DefaultHttpClient.BaseAddress;
        }
        else if (!string.IsNullOrWhiteSpace(MainViewModel.BrowserOrigin) && Uri.TryCreate(MainViewModel.BrowserOrigin, UriKind.Absolute, out var originUri))
        {
            client.BaseAddress = originUri;
        }
        else if (!OperatingSystem.IsBrowser())
        {
            client.BaseAddress = new Uri("http://127.0.0.1:5246");
        }
        return client;
    }

    public static string FormatEndpoint(string? apiBase, string relativePath)
    {
        var rel = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        if (string.IsNullOrWhiteSpace(apiBase))
        {
            return OperatingSystem.IsBrowser() ? rel : $"http://127.0.0.1:5246{rel}";
        }
        return apiBase.TrimEnd('/') + rel;
    }
}
