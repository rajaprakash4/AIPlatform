using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Tools;

public class ScopedRAGTool : ITool
{
    private readonly IVectorStore _vectorStore;
    private readonly IAIServiceFactory _aiFactory;
    private readonly string _category; // The lock 🔒

    public ScopedRAGTool(IVectorStore vectorStore, IAIServiceFactory aiFactory, string category)
    {
        _vectorStore = vectorStore;
        _aiFactory = aiFactory;
        _category = category;
    }

    // Dynamic Name: "search_technical_manuals" or "search_hr_policy"
    public string Name => $"search_{_category.ToLower()}_documents";

    public string Description => $"Searches ONLY the {_category} document library. Use this for questions specifically about {_category} topics.";

    public async Task<string> ExecuteAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            string queryText = "";

            // Handle JSON variations
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                queryText = doc.RootElement.GetString();
            else if (doc.RootElement.TryGetProperty("query", out var q))
                queryText = q.GetString();

            if (string.IsNullOrWhiteSpace(queryText)) return "Error: No query provided.";

            var aiService = _aiFactory.GetDefaultService();
            var queryVector = await aiService.GenerateEmbeddingAsync(queryText);

            // 🔒 SEARCH LOCKED TO THIS CATEGORY
            var results = await _vectorStore.SearchAsync(queryVector, categoryFilter: _category, limit: 3);

            if (!results.Any()) return $"No relevant info found in {_category} documents.";

            return string.Join("\n---\n", results.Select(r =>
                $"[Source: {r.SourceFileName}]\n{r.Content}"));
        }
        catch (Exception ex)
        {
            return $"Tool Error: {ex.Message}";
        }
    }
}