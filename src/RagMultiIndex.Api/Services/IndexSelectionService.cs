using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;

namespace RagMultiIndex.Api.Services;

public class IndexSelectionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<IndexSelectionService> _logger;
    private readonly TokenCredential _credential = new DefaultAzureCredential();

    private const string SelectionPromptTemplate = "Given the following user query, decide which index(es) should be used to answer it. There are two indexes: 'inventories' and 'incidents'.\\n- If the query is about responsible department or contact information, set 'inventories' to True.\\n- If the query is about past incident information, set 'incidents' to True.\\n- If the query is about Azure Arc information, set 'arc' to True.\\n- If some apply, set them to True.\\nReturn a JSON object like: {\\\"inventories\\\": true, \\\"incidents\\\": false, \\\"arc\\\": false} or {\\\"inventories\\\": true, \\\"incidents\\\": true, \\\"arc\\\": true}.\\nUser query: {0}";

    public IndexSelectionService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<IndexSelectionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<(bool inventories, bool incidents, bool arc)> DecideIndexesAsync(string userQuery, CancellationToken ct)
    {
        try
        {
            var prompt = string.Format(SelectionPromptTemplate, userQuery);
            var deployment = _config["AZURE_OPENAI_GPT_DEPLOYMENT"] ?? string.Empty;
            var client = _httpClientFactory.CreateClient("azure-openai");
            if (client.BaseAddress == null)
            {
                _logger.LogWarning("OpenAI endpoint not configured; defaulting to all indexes.");
                return (true, true, true);
            }
            // Acquire AAD token
            var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }), ct);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var body = new
            {
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.0
            };
            var jsonBody = JsonSerializer.Serialize(body);
            var url = $"openai/deployments/{deployment}/chat/completions?api-version=2024-05-01-preview";
            using var resp = await client.PostAsync(url, new StringContent(jsonBody, Encoding.UTF8, "application/json"), ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            _logger.LogInformation("Index selection raw response: {Raw}", content);
            try
            {
                var jsonContent = JsonDocument.Parse(content);
                bool inv = jsonContent.RootElement.TryGetProperty("inventories", out var iProp) && iProp.GetBoolean();
                bool inc = jsonContent.RootElement.TryGetProperty("incidents", out var nProp) && nProp.GetBoolean();
                bool arc = jsonContent.RootElement.TryGetProperty("arc", out var aProp) && aProp.GetBoolean();
                return (inv, inc, arc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse index selection JSON. Falling back to all indexes.");
                return (true, true, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during index selection. Falling back to all indexes.");
            return (true, true, true);
        }
    }
}
