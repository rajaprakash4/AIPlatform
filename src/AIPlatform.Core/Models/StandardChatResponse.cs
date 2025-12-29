namespace AIPlatform.Core.Models;

public class StandardChatResponse
{
    // The Markdown answer from the AI
    public string Content { get; set; }

    // Did the process succeed?
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    // Metadata: Token usage, which model was used, latency
    public Dictionary<string, object> Metadata { get; set; } = new();

    // If the AI decided to perform an action (e.g., "RefundIssued"), return it here
    public List<string> ActionsExecuted { get; set; } = new();
}