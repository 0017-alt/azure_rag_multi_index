using System.Collections.Generic;

namespace RagMultiIndex.Api.Models;

public class ChatRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
}
