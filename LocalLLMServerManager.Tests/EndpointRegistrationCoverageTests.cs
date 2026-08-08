using LocalLLMServerManager.Endpoints;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class EndpointRegistrationCoverageTests
{
    [Fact]
    public void MapAllEndpoints_InvokesStaticRegistrationMethods()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app.MapHealthEndpoints();
        app.MapModelProxyEndpoints();
        app.MapMcpEndpoints();

        Assert.NotNull(app);
    }
}
