using AIPlatform.Core.Models;

namespace AIPlatform.Core.Interfaces;

public interface IVectorStore
{
    Task SaveChunksAsync(List<KnowledgeChunk> chunks);
    Task<List<KnowledgeChunk>> SearchAsync(float[] queryVector, string? categoryFilter = null, int limit = 3);
    Task DeleteDocumentAsync(string documentId);
}