using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Services;

public class GeminiService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;
    private readonly ToolRegistry _toolRegistry;

    // Endpoint for Chat
    private const string ChatUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent";

    // Endpoint for Embeddings (Converting text to numbers)
    private const string EmbeddingUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent";

    public GeminiService(
    HttpClient httpClient,
    IConfiguration config,
    ILogger<GeminiService> logger,
    ToolRegistry toolRegistry) // <--- Inject here
    {
        _httpClient = httpClient;
        _apiKey = config["AI:Gemini:ApiKey"];
        _logger = logger;
        _toolRegistry = toolRegistry;
    }

    // 1. CHAT GENERATION
    // 2. The New Agent-Aware Response Generation
    public async Task<StandardChatResponse> GenerateResponseAsync(StandardChatRequest request)
    {
        var url = $"{ChatUrl}?key={_apiKey}";
        var tools = _toolRegistry.GetToolsForScope(request.ContextScope);
        // --- STEP A: Build the Request with Tools ---
        var geminiContents = new List<object>();

        // Add History
        foreach (var msg in request.History)
        {
            geminiContents.Add(new
            {
                role = msg.Role == "user" ? "user" : "model",
                parts = new[] { new { text = msg.Content } }
            });
        }
        // Add Current User Message
        geminiContents.Add(new { role = "user", parts = new[] { new { text = request.UserMessage } } });

        // Construct Tool Definitions (The Schema)
        var toolConfig = new
        {
            function_declarations = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = new
                {
                    type = "OBJECT",
                    properties = new { query = new { type = "STRING", description = "The search query" } },
                    required = new[] { "query" }
                }
            }).ToArray()
        };

        var payload = new
        {
            contents = geminiContents,
            tools = new[] { toolConfig } // Send tools to Gemini
        };

        try
        {
            // --- STEP B: First Call (Does AI want to use a tool?) ---
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Check for error
            if (!response.IsSuccessStatusCode)
                return new StandardChatResponse { IsSuccess = false, ErrorMessage = json.ToString() };

            var candidate = json.GetProperty("candidates")[0];
            var content = candidate.GetProperty("content");
            var parts = content.GetProperty("parts")[0];

            // --- STEP C: Check for Function Call ---
            if (parts.TryGetProperty("functionCall", out var functionCall))
            {
                var functionName = functionCall.GetProperty("name").GetString();

                // We look up the tool using the SAME scope to ensure consistency
                var tool = _toolRegistry.GetToolByName(functionName, request.ContextScope);

                string toolResult = "Error: Tool not found.";
                if (tool != null)
                {
                    // Execute the search
                    // This will call ScopedRAGTool.ExecuteAsync -> Qdrant.Search(filter: "HR")
                    toolResult = await tool.ExecuteAsync(functionCall.GetProperty("args").ToString());
                }

                // --- STEP D: The "ReAct" - Send Result back to AI ---
                // We must send the whole conversation history again:
                // 1. User Prompt
                // 2. AI's Function Call request
                // 3. Our Function Response

                // Add the AI's "I want to call a function" message to history
                geminiContents.Add(new
                {
                    role = "model",
                    parts = new[] { new { functionCall = functionCall } } // Raw function call object
                });

                // Add the Tool Result
                geminiContents.Add(new
                {
                    role = "function",
                    parts = new[]
                    {
                    new
                    {
                        functionResponse = new
                        {
                            name = functionName,
                            response = new { content = toolResult }
                        }
                    }
                }
                });

                // Call Gemini Again (Round 2)
                var round2Payload = new { contents = geminiContents, tools = new[] { toolConfig } };
                var response2 = await _httpClient.PostAsJsonAsync(url, round2Payload);
                var json2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

                var finalText = json2.GetProperty("candidates")[0]
                                     .GetProperty("content")
                                     .GetProperty("parts")[0]
                                     .GetProperty("text")
                                     .GetString();

                return new StandardChatResponse { IsSuccess = true, Content = finalText };
            }

            // If no function call, just return the text
            return new StandardChatResponse { IsSuccess = true, Content = parts.GetProperty("text").GetString() };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent Loop Failed");
            return new StandardChatResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    // 2. EMBEDDING GENERATION (This was missing!)
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var url = $"{EmbeddingUrl}?key={_apiKey}";

            var payload = new
            {
                model = "models/text-embedding-004",
                content = new { parts = new[] { new { text = text } } }
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Embedding API Failed: {error}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Extract the "values" array from the JSON response
            if (json.TryGetProperty("embedding", out var embeddingElement) &&
                embeddingElement.TryGetProperty("values", out var valuesElement))
            {
                return valuesElement.EnumerateArray()
                                    .Select(x => x.GetSingle())
                                    .ToArray();
            }

            throw new Exception("Invalid embedding response format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini Embedding Failed");
            throw; // Re-throw so the controller knows it failed
        }
    }

    public Task<T> MapDataAsync<T>(object sourceData, string targetSchemaDescription)
    {
        throw new NotImplementedException();
    }
}