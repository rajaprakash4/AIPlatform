using AIPlatform.Core.Models;

namespace AIPlatform.Core.Interfaces;

public interface IAIService
{
    // The core method to get a simple text completion
    Task<StandardChatResponse> GenerateResponseAsync(StandardChatRequest request);

    // The method for "Smart Mapping" (User's special requirement)
    // Takes messy JSON -> Returns clean mapped object
    Task<T> MapDataAsync<T>(object sourceData, string targetSchemaDescription);

    // NEW: Method for RAG Embeddings (The missing piece)
    Task<float[]> GenerateEmbeddingAsync(string text);
}