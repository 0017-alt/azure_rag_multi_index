namespace RagMultiIndex.Api.Models;

public class OpenAIChatResponse
{
    public List<Choice> Choices { get; set; } = new();

    public class Choice
    {
        public Message Message { get; set; } = new();
    }

    public class Message
    {
        public string Role { get; set; } = "assistant";
        public string Content { get; set; } = string.Empty;
        public MessageContext? Context { get; set; }
    }

    public class MessageContext
    {
        public List<Citation>? Citations { get; set; }
    }

    public class Citation
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? FilePath { get; set; }
        public string? Url { get; set; }
    }
}
