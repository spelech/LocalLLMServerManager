using System;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class BrowserLauncherTests
{
    public BrowserLauncherTests()
    {
        BrowserLauncher.SuppressProcessStart = true;
    }

    [Theory]
    [InlineData("http://localhost:5246")]
    [InlineData("https://google.com")]
    [InlineData("http://127.0.0.1:5246/health")]
    public void OpenUrl_WithValidHttpOrHttps_ShouldBeAllowedOrThrowOnlyProcessStartException(string url)
    {
        try
        {
            bool result = BrowserLauncher.OpenUrl(url);
            Assert.True(result);
        }
        catch (Exception ex)
        {
            Assert.True(ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenUrl_WithNullOrEmpty_ReturnsFalseAndShowsToast(string? url)
    {
        ToastItem? emittedToast = null;
        Action<ToastItem> handler = t => emittedToast = t;
        ToastService.Instance.OnToastShow += handler;
        try
        {
            bool result = BrowserLauncher.OpenUrl(url);
            Assert.False(result);
            Assert.NotNull(emittedToast);
            Assert.Contains("empty", emittedToast!.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ToastService.Instance.OnToastShow -= handler;
        }
    }

    [Theory]
    [InlineData("invalid-url")]
    [InlineData("relative/path")]
    public void OpenUrl_WithInvalidUrlFormat_ReturnsFalseAndShowsToast(string url)
    {
        ToastItem? emittedToast = null;
        Action<ToastItem> handler = t => emittedToast = t;
        ToastService.Instance.OnToastShow += handler;
        try
        {
            bool result = BrowserLauncher.OpenUrl(url);
            Assert.False(result);
            Assert.NotNull(emittedToast);
            Assert.Contains("format", emittedToast!.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ToastService.Instance.OnToastShow -= handler;
        }
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://secure-ftp.com")]
    [InlineData("gopher://something")]
    [InlineData("mailto:test@example.com")]
    public void OpenUrl_WithNonHttpOrHttpsScheme_ReturnsFalseAndShowsToast(string url)
    {
        ToastItem? emittedToast = null;
        Action<ToastItem> handler = t => emittedToast = t;
        ToastService.Instance.OnToastShow += handler;
        try
        {
            bool result = BrowserLauncher.OpenUrl(url);
            Assert.False(result);
            Assert.NotNull(emittedToast);
            Assert.Contains("HTTP", emittedToast!.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ToastService.Instance.OnToastShow -= handler;
        }
    }
}
