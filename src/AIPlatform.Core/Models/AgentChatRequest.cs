using System.Text.Json.Serialization;

namespace AIPlatform.Core.Models;

public class AgentChatRequest
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "user-1";
    public string UserMessage { get; set; }

    // The "Persona" the user selected (e.g., "HR_Helper")
    public string AgentId { get; set; }

    public List<ChatMessage> History { get; set; } = new();
}

