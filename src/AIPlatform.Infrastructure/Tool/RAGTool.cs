using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Tools;

public class RAGTool : ITool
{
    private readonly IVectorStore _vectorStore;
    private readonly IAIServiceFactory _aiFactory; // Store the Factory, not the Service

    public RAGTool(IVectorStore vectorStore, IAIServiceFactory aiFactory)
    {
        _vectorStore = vectorStore;
        _aiFactory = aiFactory;
    }

    public string Name => "search_knowledge_base";
    public string Description => "Searches the internal document library. Use this when the user asks about company policies, technical manuals, or specific project data.";

    public async Task<string> ExecuteAsync(string argsJson)
    {
        // 1. Parse what the AI wants to search for
        using var doc = JsonDocument.Parse(argsJson);
        if (!doc.RootElement.TryGetProperty("query", out var queryElement))
            return "Error: Missing 'query' parameter.";

        string queryText = queryElement.GetString();

        // 2. Perform the RAG Search (Logic moved from Controller to here)
        var _aiService = _aiFactory.GetDefaultService();
        var queryVector = await _aiService.GenerateEmbeddingAsync(queryText);
        var results = await _vectorStore.SearchAsync(queryVector, limit: 3);

        if (!results.Any()) return "No relevant documents found.";

        // 3. Return the raw text facts to the AI
        return string.Join("\n\n", results.Select(r =>
            $"[Source: {r.SourceFileName}]\n{r.Content}"));
    }
}