// Pure POCOs (Plain Old CLR Objects) - No Database Dependencies
namespace AIPlatform.Core.Models;

public class ToolDefinition
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public List<string> InputKeys { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
    public Dictionary<string, string> OutputAlias { get; set; } = new();
}

public class AgentWorkflow
{
    public string Id { get; set; }
    public string Description { get; set; }
    public List<string> Steps { get; set; } = new();
}