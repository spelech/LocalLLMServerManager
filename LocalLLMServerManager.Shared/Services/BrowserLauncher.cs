using System;
using System.Diagnostics;

namespace LocalLLMServerManager.Shared.Services;

public static class BrowserLauncher
{
    public static bool OpenUrl(string? urlString)
    {
        if (string.IsNullOrWhiteSpace(urlString))
        {
            ToastService.Instance.Show("Cannot open an empty URL.", ToastType.Warning);
            return false;
        }

        urlString = urlString.Trim();

        // 1. Parse URI and ensure it is an absolute URI
        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
        {
            ToastService.Instance.Show("Invalid URL format.", ToastType.Error);
            return false;
        }

        // 2. Restrict to HTTP and HTTPS protocols only
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            ToastService.Instance.Show($"Insecure URL protocol: {uri.Scheme}. Only HTTP and HTTPS are permitted.", ToastType.Error);
            return false;
        }

        // 3. Additional strict checks against shell injection or path traversal attempts
        // Ensure no quotes, newlines, backticks, dollar signs, or other control chars in the original string
        if (urlString.Contains('\"') ||
            urlString.Contains('\'') ||
            urlString.Contains('\r') ||
            urlString.Contains('\n') ||
            urlString.Contains('`') ||
            urlString.Contains('$'))
        {
            ToastService.Instance.Show("URL contains invalid or dangerous characters.", ToastType.Error);
            return false;
        }

        try
        {
            // We use the validated URI's AbsoluteUri to prevent any shell/argument injections
            var safeUrl = uri.AbsoluteUri;
            Process.Start(new ProcessStartInfo { FileName = safeUrl, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"Failed to open URL in browser: {ex.Message}", ToastType.Error);
            return false;
        }
    }
}
