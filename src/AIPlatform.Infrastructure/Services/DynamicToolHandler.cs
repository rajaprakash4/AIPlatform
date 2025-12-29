using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Services;

public class DynamicToolHandler
{
    private readonly IVectorStore _vectorStore;
    private readonly IAIServiceFactory _aiFactory;
    private readonly HttpClient _httpClient;

    public DynamicToolHandler(IVectorStore vectorStore, IAIServiceFactory aiFactory, HttpClient httpClient)
    {
        _vectorStore = vectorStore;
        _aiFactory = aiFactory;
        _httpClient = httpClient;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(ToolDefinition tool, Dictionary<string, object> inputs)
    {
        switch (tool.Type)
        {
            case "DataService":
                return await ExecuteDataService(tool, inputs);
            case "AIService":
                return await ExecuteAIService(tool, inputs);
            case "KnowledgeStore":
                return await ExecuteRAG(tool, inputs);
            default:
                throw new Exception($"Unknown Tool Type: {tool.Type}");
        }
    }

    // --- HANDLER 1: Data Service (API Call) ---
    private async Task<Dictionary<string, object>> ExecuteDataService(ToolDefinition tool, Dictionary<string, object> inputs)
    {
        string url = tool.Configuration["Url"].ToString();

        // Replace placeholders like {UserId} with actual values
        foreach (var kvp in inputs)
        {
            url = url.Replace($"{{{kvp.Key}}}", kvp.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        var response = await _httpClient.GetStringAsync(url);

        // Flatten the JSON response
        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(response);
        return json ?? new Dictionary<string, object>();
    }

    // --- HANDLER 2: AI Service (Prompting) ---
    private async Task<Dictionary<string, object>> ExecuteAIService(ToolDefinition tool, Dictionary<string, object> inputs)
    {
        string prompt = tool.Configuration["SystemPrompt"].ToString();

        // Inject data into prompt (e.g., {{username}})
        foreach (var kvp in inputs)
        {
            prompt = prompt.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }

        var aiService = _aiFactory.GetDefaultService();
        // Sending this as a simple request (Simulated chat for one-shot prompt)
        var result = await aiService.GenerateResponseAsync(new StandardChatRequest
        {
            UserMessage = prompt,
            UserId = "System",
            SessionId = "System"
        });

        return new Dictionary<string, object> { { "AI_Response", result.Content } };
    }

    // --- HANDLER 3: RAG (Knowledge Store) ---
    private async Task<Dictionary<string, object>> ExecuteRAG(ToolDefinition tool, Dictionary<string, object> inputs)
    {
        // 1. Extract Query & Store safely
        // Prefer "Query" key, fallback to first value if missing
        string query = inputs.ContainsKey("Query")
            ? inputs["Query"].ToString()
            : (inputs.Values.FirstOrDefault()?.ToString() ?? "");

        // 2. Determine Category (StoreName)
        // Priority: 1. Input param (dynamic) -> 2. Config (static) -> 3. Default "General"
        string category = "General";

        if (inputs.ContainsKey("StoreName") && !string.IsNullOrWhiteSpace(inputs["StoreName"].ToString()))
        {
            category = inputs["StoreName"].ToString();
        }
        else if (tool.Configuration.ContainsKey("Category"))
        {
            category = tool.Configuration["Category"].ToString();
        }
        else if (tool.Configuration.ContainsKey("DefaultStore"))
        {
            category = tool.Configuration["DefaultStore"].ToString();
        }

        // 3. Generate Embedding & Search
        var aiService = _aiFactory.GetDefaultService();
        var embedding = await aiService.GenerateEmbeddingAsync(query);
        var results = await _vectorStore.SearchAsync(embedding, category, limit: 3);

        // =========================================================
        // 4. ✅ CHECK FOR MISSING DATA (The Fix)
        // =========================================================
        if (results == null || results.Count == 0)
        {
            return new Dictionary<string, object>
        {
            { "success", false },
            { "isMissingData", true }, // <--- The Flag for UI
            { "targetStore", category },
            { "message", $"I checked the '{category}' knowledge store but found no documents related to: '{query}'." },
            
            // Return empty result string so subsequent agents don't crash, 
            // but they will likely stop and wait for user.
            { "RAG_Results", "No documents found." }
        };
        }

        // 5. Success Path
        string combinedText = string.Join("\n\n---\n\n", results.Select((r, index) =>
         $"**Source Fragment {index + 1}:**\n{r.Content.Trim()}"));

        return new Dictionary<string, object>
        {
            { "success", true },
            { "isMissingData", false },
            { "targetStore", category },
            { "RAG_Results", combinedText }
        };
    }
}