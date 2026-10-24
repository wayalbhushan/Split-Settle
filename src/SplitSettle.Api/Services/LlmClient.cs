namespace SplitSettle.Api.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SplitSettle.Api.Errors;
using SplitSettle.Core.Services;

public class LlmClient : ILlmClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmClient> _logger;

    public LlmClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LlmClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var provider = _configuration["Llm:Provider"]?.Trim() ?? "Gemini";
        var apiKey = _configuration["Llm:ApiKey"]?.Trim()
                     ?? _configuration["GEMINI_API_KEY"]?.Trim()
                     ?? _configuration["GROQ_API_KEY"]?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new LlmNotConfiguredException("AI parse service is not configured. An API key is required.");
        }

        var isGroq = provider.Equals("Groq", StringComparison.OrdinalIgnoreCase);
        var model = _configuration["Llm:Model"]?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new LlmNotConfiguredException("AI parse service is not configured. A model is required.");
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        Exception? lastException = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                if (isGroq)
                {
                    return await CallGroqAsync(client, model, apiKey, prompt, cts.Token);
                }

                return await CallGeminiAsync(client, model, apiKey, prompt, cts.Token);
            }
            catch (Exception ex) when (attempt == 1 && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                _logger.LogWarning("LLM provider attempt 1 failed with {ExceptionType}. Retrying once.", ex.GetType().Name);
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError("LLM provider call failed on attempt {Attempt} with {ExceptionType}.", attempt, ex.GetType().Name);
                break;
            }
        }

        throw new LlmProviderException("LLM provider failed to respond or returned an error.", lastException);
    }

    private static async Task<string> CallGeminiAsync(
        HttpClient client,
        string model,
        string apiKey,
        string prompt,
        CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.0
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(url, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API error (Status: {response.StatusCode})");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var contentElem) &&
            contentElem.TryGetProperty("parts", out var parts) &&
            parts.GetArrayLength() > 0 &&
            parts[0].TryGetProperty("text", out var textElem))
        {
            return textElem.GetString() ?? string.Empty;
        }

        throw new JsonException("Gemini response missing candidate text part.");
    }

    private static async Task<string> CallGroqAsync(
        HttpClient client,
        string model,
        string apiKey,
        string prompt,
        CancellationToken cancellationToken)
    {
        const string url = "https://api.groq.com/openai/v1/chat/completions";

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.0
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Groq API error (Status: {response.StatusCode})");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var messageElem) &&
            messageElem.TryGetProperty("content", out var contentElem))
        {
            return contentElem.GetString() ?? string.Empty;
        }

        throw new JsonException("Groq response missing message content.");
    }
}
