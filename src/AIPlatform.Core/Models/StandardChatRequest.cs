namespace AIPlatform.Core.Models;

public class StandardChatRequest
{
    // The unique session ID (critical for history)
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    // The user's unique ID (for security/logging)
    public string UserId { get; set; } = "session-1";

    // The actual text message from the user
    public string UserMessage { get; set; }

    // Optional: Specific context variables (e.g., "CurrentPage": "Billing")
    // This helps the Router decide which Agent to call.
    public Dictionary<string, object> ContextData { get; set; } = new();

    // Optional: If the frontend wants to force a specific workflow
    public string? IntentOverride { get; set; }

    public List<ChatMessage> History { get; set; } = new();

    public string ContextScope { get; set; } = "General";
    public List<string>? AllowedIds { get; set; }
}