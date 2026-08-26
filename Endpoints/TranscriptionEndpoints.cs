using System.Net.Http.Headers;
using System.Text.Json;
using LocalLLMServerManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class TranscriptionEndpoints
{
    public static WebApplication MapTranscriptionEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/audio/transcriptions", async (HttpContext context, ISettingsService settingsService, IHttpClientFactory clientFactory) =>
        {
            return await HandleAudioTranscriptionOrTranslationAsync(context, settingsService, clientFactory, isTranslation: false);
        });

        app.MapPost("/v1/audio/translations", async (HttpContext context, ISettingsService settingsService, IHttpClientFactory clientFactory) =>
        {
            return await HandleAudioTranscriptionOrTranslationAsync(context, settingsService, clientFactory, isTranslation: true);
        });

        return app;
    }

    private static async Task<IResult> HandleAudioTranscriptionOrTranslationAsync(
        HttpContext context,
        ISettingsService settingsService,
        IHttpClientFactory clientFactory,
        bool isTranslation)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new { error = new { message = "No file uploaded in 'file' form field." } });
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch
        {
            return Results.BadRequest(new { error = new { message = "No file uploaded in 'file' form field." } });
        }

        var file = form.Files["file"];
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = new { message = "No file uploaded in 'file' form field." } });
        }

        var model = form["model"].ToString();
        if (string.IsNullOrWhiteSpace(model)) model = "whisper-large-v3-turbo";

        var language = form["language"].ToString();
        var prompt = form["prompt"].ToString();
        var responseFormat = form["response_format"].ToString();
        if (string.IsNullOrWhiteSpace(responseFormat)) responseFormat = "json";
        var temperatureStr = form["temperature"].ToString();

        // Attempt proxying to configured upstream STT / audio engine
        try
        {
            var settings = settingsService.LoadSettings();
            var baseUrl = (string.IsNullOrWhiteSpace(settings.AudioEngineUrl) ? "http://127.0.0.1:8880" : settings.AudioEngineUrl).TrimEnd('/');
            var endpointPath = isTranslation ? "/v1/audio/translations" : "/v1/audio/transcriptions";
            var targetUrl = $"{baseUrl}{endpointPath}";

            using var formData = new MultipartFormDataContent();
            await using var fileStream = file.OpenReadStream();
            using var streamContent = new StreamContent(fileStream);
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            }
            formData.Add(streamContent, "file", file.FileName);

            if (!string.IsNullOrWhiteSpace(model)) formData.Add(new StringContent(model), "model");
            if (!string.IsNullOrWhiteSpace(language)) formData.Add(new StringContent(language), "language");
            if (!string.IsNullOrWhiteSpace(prompt)) formData.Add(new StringContent(prompt), "prompt");
            if (!string.IsNullOrWhiteSpace(responseFormat)) formData.Add(new StringContent(responseFormat), "response_format");
            if (!string.IsNullOrWhiteSpace(temperatureStr)) formData.Add(new StringContent(temperatureStr), "temperature");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var http = clientFactory.CreateClient();
            var targetResponse = await http.PostAsync(targetUrl, formData, cts.Token);

            var contentType = targetResponse.Content.Headers.ContentType?.ToString() ?? "application/json";
            context.Response.StatusCode = (int)targetResponse.StatusCode;
            context.Response.ContentType = contentType;
            await using var responseStream = await targetResponse.Content.ReadAsStreamAsync(context.RequestAborted);
            await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
            return Results.Empty;
        }
        catch (Exception ex)
        {
            return Results.Json(
                new { error = new { message = $"Upstream audio transcription engine unavailable: {ex.Message}", type = "bad_gateway" } },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    public static IResult FormatTranscriptionResponse(string responseFormat, string text, string language = "english", bool isTranslation = false)
    {
        return responseFormat.ToLowerInvariant() switch
        {
            "text" => Results.Text(text, "text/plain"),
            "srt" => Results.Text($"1\r\n00:00:00,000 --> 00:00:01,000\r\n{text}\r\n", "text/plain"),
            "vtt" => Results.Text($"WEBVTT\r\n\r\n1\r\n00:00:00.000 --> 00:00:01.000\r\n{text}\r\n", "text/vtt"),
            "verbose_json" => Results.Ok(new
            {
                task = isTranslation ? "translate" : "transcribe",
                language = language,
                duration = 1.0,
                text = text,
                segments = Array.Empty<object>()
            }),
            _ => Results.Ok(new
            {
                text = text,
                language = language,
                duration = 1.0,
                segments = Array.Empty<object>()
            })
        };
    }
}
