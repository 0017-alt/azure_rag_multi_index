using Microsoft.AspNetCore.Mvc;
using RagMultiIndex.Api.Models;
using RagMultiIndex.Api.Services;

namespace RagMultiIndex.Api.Controllers;

[ApiController]
[Route("api/chat")] 
public class ChatController : ControllerBase
{
    private readonly RagChatService _ragChatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(RagChatService ragChatService, ILogger<ChatController> logger)
    {
        _ragChatService = ragChatService;
        _logger = logger;
    }

    [HttpPost("completion")]
    public async Task<IActionResult> Completion([FromBody] ChatRequest request, CancellationToken ct)
    {
        try
        {
            if (request.Messages == null || request.Messages.Count == 0)
            {
                return BadRequest(new { message = "Messages cannot be empty" });
            }
            var resp = await _ragChatService.GetChatCompletionAsync(request.Messages, ct);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat completion");
            var msg = ex.Message.ToLower();
            if (msg.Contains("rate limit") || msg.Contains("capacity") || msg.Contains("quota"))
            {
                return Ok(new
                {
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = "The AI service is currently experiencing high demand. Please wait a moment and try again." } }
                    }
                });
            }
            return Ok(new
            {
                choices = new[]
                {
                    new { message = new { role = "assistant", content = $"An error occurred: {ex.Message}" } }
                }
            });
        }
    }
}
