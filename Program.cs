using System.Text.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Basic VRAM Orchestration Middleware
// If a request is heading to SD Forge, we can check if Ollama needs to be unloaded.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isForgeRequest = path.StartsWith("/sdapi", StringComparison.OrdinalIgnoreCase) || 
                         path.StartsWith("/v1/images", StringComparison.OrdinalIgnoreCase);

    if (isForgeRequest)
    {
        // Simple orchestration: When an image request comes in, ensure Ollama isn't hogging VRAM
        // Note: In 16GB VRAM this is a fallback, we could fire and forget an unload command to Ollama if needed.
        // For demonstration, we'll log it. If VRAM is tight, we would send a request to Ollama to unload the active model.
        app.Logger.LogInformation("Image request detected. If VRAM is constrained, Ollama unload logic can trigger here.");
    }

    await next();
});

app.MapReverseProxy();

app.Run();
