using System.Text;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using RagMultiIndex.Api.Models;
using Azure.Core;
using System.Text.Json;

namespace RagMultiIndex.Api.Services;

public class RagChatService
{
    private readonly IndexSelectionService _indexSelectionService;
    private readonly ILogger<RagChatService> _logger;
    private readonly string _gptDeployment;
    private readonly string _systemPrompt;
    private readonly SearchClient _searchInventories;
    private readonly SearchClient _searchIncidents;
    private readonly SearchClient _searchArc;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential _credential = new DefaultAzureCredential();

    public RagChatService(
        IndexSelectionService indexSelectionService,
        ILogger<RagChatService> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _indexSelectionService = indexSelectionService;
        _logger = logger;
        _gptDeployment = config["AZURE_OPENAI_GPT_DEPLOYMENT"] ?? throw new ArgumentNullException("AZURE_OPENAI_GPT_DEPLOYMENT");
        _systemPrompt = config["SYSTEM_PROMPT"] ?? "You are a helpful assistant.\nQuery: {query}\nSources:\n{sources}";

        var searchUrl = config["AZURE_SEARCH_SERVICE_URL"] ?? throw new ArgumentNullException("AZURE_SEARCH_SERVICE_URL");
        var invIndex = config["AZURE_SEARCH_INDEX_NAME_INVENTORIES"] ?? "index-inventories";
        var incIndex = config["AZURE_SEARCH_INDEX_NAME_INCIDENTS"] ?? "index-incidents";
        var arcIndex = config["AZURE_SEARCH_INDEX_NAME_ARC"] ?? "index-arc";
        var credential = new DefaultAzureCredential();
        _searchInventories = new SearchClient(new Uri(searchUrl), invIndex, credential);
        _searchIncidents = new SearchClient(new Uri(searchUrl), incIndex, credential);
        _searchArc = new SearchClient(new Uri(searchUrl), arcIndex, credential);
        _httpClientFactory = httpClientFactory;
    }

    public async Task<OpenAIChatResponse> GetChatCompletionAsync(List<ChatMessage> history, CancellationToken ct)
    {
        var recent = history.TakeLast(20).ToList();
        var userQuery = recent.LastOrDefault()?.Content ?? string.Empty;

        var (useInv, useInc, useArc) = await _indexSelectionService.DecideIndexesAsync(userQuery, ct);
        var sourcesBuilder = new StringBuilder();

        async Task AppendAsync(SearchClient client, string label)
        {
            try
            {
                var options = new SearchOptions
                {
                    Size = label == "incidents" ? 3 : 1,
                    Select = { "content" }
                };
                var results = await client.SearchAsync<SearchDocument>(userQuery, options, ct);
                foreach (var r in results.Value.GetResults())
                {
                    if (r.Document.TryGetValue("content", out var contentObj))
                    {
                        var text = contentObj as string;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sourcesBuilder.AppendLine(text);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search query failed for {Index}", label);
            }
        }

        if (useInv) await AppendAsync(_searchInventories, "inventories");
        if (useInc) await AppendAsync(_searchIncidents, "incidents");
        if (useArc) await AppendAsync(_searchArc, "arc");

        var prompt = _systemPrompt.Replace("{query}", userQuery).Replace("{sources}", sourcesBuilder.ToString());

        var client = _httpClientFactory.CreateClient("azure-openai");
        if (client.BaseAddress == null)
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not configured.");
        }
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }), ct);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        var body = new
        {
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.2
        };
        var json = JsonSerializer.Serialize(body);
        var url = $"openai/deployments/{_gptDeployment}/chat/completions?api-version=2024-05-01-preview";
        using var resp = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var answer = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        var response = new OpenAIChatResponse
        {
            Choices =
            {
                new OpenAIChatResponse.Choice
                {
                    Message = new OpenAIChatResponse.Message
                    {
                        Role = "assistant",
                        Content = answer,
                        Context = new OpenAIChatResponse.MessageContext
                        {
                            Citations = new List<OpenAIChatResponse.Citation>() // Placeholder for future citation extraction
                        }
                    }
                }
            }
        };
        return response;
    }
}
