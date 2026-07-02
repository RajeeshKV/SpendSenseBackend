using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;

namespace SpendSense.Infrastructure.StatementParsers;

public sealed class AiStatementParser(HttpClient httpClient, IOptions<AiOptions> options, ILogger<AiStatementParser> logger) : IStatementParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AiExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".heic", ".txt", ".csv", ".tsv", ".ofx", ".qif"
    };

    public string Name => "AiStatementParser";

    public bool CanParse(string fileName, string contentType)
    {
        var settings = options.Value;
        if (!settings.StatementParsingEnabled || string.IsNullOrWhiteSpace(settings.ApiKey) || settings.Provider.Equals("None", StringComparison.OrdinalIgnoreCase)) return false;
        var extension = Path.GetExtension(fileName);
        return AiExtensions.Contains(extension) || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    public string DetectBank(string fileName, Stream content) => StatementParsingSupport.DetectBank(fileName, fileName);

    public async Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var payload = await BuildPromptPayloadAsync(content, settings.StatementParsingMaxChars, cancellationToken);
        var response = settings.Provider.ToLowerInvariant() switch
        {
            "gemini" => await SendGeminiAsync(settings, payload, cancellationToken),
            "groq" => await SendOpenAiCompatibleAsync(settings, payload, "https://api.groq.com/openai/v1/chat/completions", cancellationToken),
            "openrouter" => await SendOpenAiCompatibleAsync(settings, payload, "https://openrouter.ai/api/v1/chat/completions", cancellationToken),
            "openai" => await SendOpenAiCompatibleAsync(settings, payload, "https://api.openai.com/v1/chat/completions", cancellationToken),
            _ => await SendOpenAiCompatibleAsync(settings, payload, settings.Endpoint, cancellationToken)
        };

        return StatementParsingSupport.ParseAiJson(response);
    }

    private static async Task<PromptPayload> BuildPromptPayloadAsync(Stream content, int maxChars, CancellationToken cancellationToken)
    {
        if (content.CanSeek) content.Position = 0;
        await using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (content.CanSeek) content.Position = 0;

        if (bytes.Length >= 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F')
        {
            try
            {
                await using var pdfStream = new MemoryStream(bytes);
                var text = PdfTextStatementParser.ExtractText(pdfStream, maxPages: 0);
                return new PromptPayload(Trim(text, maxChars), null, null);
            }
            catch
            {
                return new PromptPayload(string.Empty, Convert.ToBase64String(bytes), "application/pdf");
            }
        }

        var mime = DetectImageMime(bytes);
        if (mime is not null) return new PromptPayload(string.Empty, Convert.ToBase64String(bytes), mime);

        var textContent = Encoding.UTF8.GetString(bytes);
        return new PromptPayload(Trim(textContent, maxChars), null, null);
    }

    private async Task<string> SendOpenAiCompatibleAsync(AiOptions settings, PromptPayload payload, string defaultEndpoint, CancellationToken cancellationToken)
    {
        var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint) ? defaultEndpoint : settings.Endpoint;
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "gpt-4o-mini" : settings.Model;
        var content = new List<object> { new { type = "text", text = BuildInstruction(payload.Text) } };
        if (!string.IsNullOrWhiteSpace(payload.Base64) && !string.IsNullOrWhiteSpace(payload.MimeType))
        {
            content.Add(new { type = "image_url", image_url = new { url = $"data:{payload.MimeType};base64,{payload.Base64}" } });
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You extract bank statement transactions and return only valid JSON." },
                new { role = "user", content }
            }
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AI statement parsing failed with {StatusCode}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private async Task<string> SendGeminiAsync(AiOptions settings, PromptPayload payload, CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "gemini-1.5-flash" : settings.Model;
        var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={settings.ApiKey}"
            : settings.Endpoint;
        var parts = new List<object> { new { text = BuildInstruction(payload.Text) } };
        if (!string.IsNullOrWhiteSpace(payload.Base64) && !string.IsNullOrWhiteSpace(payload.MimeType))
        {
            parts.Add(new { inlineData = new { mimeType = payload.MimeType, data = payload.Base64 } });
        }

        using var response = await httpClient.PostAsJsonAsync(endpoint, new
        {
            generationConfig = new { temperature = 0, responseMimeType = "application/json" },
            contents = new[] { new { role = "user", parts } }
        }, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini statement parsing failed with {StatusCode}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private static string BuildInstruction(string text) =>
        "Extract all bank statement transactions. Return only JSON with shape " +
        "{\"transactions\":[{\"date\":\"yyyy-MM-dd\",\"description\":\"...\",\"merchant\":\"...\",\"amount\":123.45,\"debitCredit\":\"Debit|Credit\"}]}. " +
        "Use positive amount values. Ignore balances, opening/closing summaries, failed/reversed duplicates, headers, and account metadata. " +
        "If no transactions are visible, return {\"transactions\":[]}. Statement text follows:\n" + text;

    private static string? DetectImageMime(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 8, 4).Equals("WEBP", StringComparison.OrdinalIgnoreCase)) return "image/webp";
        return null;
    }

    private static string Trim(string value, int maxChars) => value.Length <= maxChars ? value : value[..maxChars];

    private sealed record PromptPayload(string Text, string? Base64, string? MimeType);
}

