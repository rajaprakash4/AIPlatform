using AIPlatform.Core.Interfaces;
using AIPlatform.Infrastructure.Tools;

namespace AIPlatform.Infrastructure.Services;

// Ensure this is registered as SCOPED in Program.cs
public class ToolRegistry
{
    private readonly IVectorStore _vectorStore;
    private readonly IAIServiceFactory _aiFactory;

    public ToolRegistry(IVectorStore vectorStore, IAIServiceFactory aiFactory)
    {
        _vectorStore = vectorStore;
        _aiFactory = aiFactory;
    }

    // 1. Get Tools Dynamic to the User's Scope
    public List<ITool> GetToolsForScope(string scope)
    {
        var tools = new List<ITool>();

        if (string.IsNullOrEmpty(scope) || scope.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            // If General, maybe we give them a generic search or access to everything?
            // For now, let's say "General" means searching the "General" category.
            tools.Add(new ScopedRAGTool(_vectorStore, _aiFactory, "General"));
        }
        else
        {
            // If scope is "HR", we create ONLY the HR tool.
            tools.Add(new ScopedRAGTool(_vectorStore, _aiFactory, scope));
        }

        return tools;
    }

    // 2. Helper to find the specific tool instance during execution
    public ITool? GetToolByName(string name, string scope)
    {
        // We regenerate the list for this scope to find the matching tool
        var tools = GetToolsForScope(scope);
        return tools.FirstOrDefault(t => t.Name == name);
    }
}