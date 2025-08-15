using Azure.Identity;
using RagMultiIndex.Api.Services;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks(); // Basic health checks (extend with dependencies later)

// Configuration & clients
var config = builder.Configuration;
builder.Services.AddHttpClient("azure-openai", (sp, http) =>
{
    var endpoint = config["AZURE_OPENAI_ENDPOINT"];
    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    }
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddScoped<IndexSelectionService>();

builder.Services.AddScoped<RagChatService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Health check endpoints (primary + backward compatibility)
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/api/health"); // Deprecated: migrate clients to /healthz

app.MapControllers();

app.Run();
