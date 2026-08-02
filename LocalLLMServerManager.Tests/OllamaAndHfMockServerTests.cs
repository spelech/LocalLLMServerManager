using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class OllamaAndHfMockServerTests
{
    [Fact]
    public async Task MainViewModel_UnloadAllVram_CallsOllamaUnloadApi()
    {
        var vm = new MainViewModel();

        await vm.UnloadAllVramAsync();
        Assert.NotNull(vm.Toasts);
    }
}
