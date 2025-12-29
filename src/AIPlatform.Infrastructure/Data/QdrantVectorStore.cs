using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models; // Ensure this has KnowledgeChunk
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text.RegularExpressions;

namespace AIPlatform.Infrastructure.Data;

public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStore> _logger;
    private const string CollectionName = "enterprise_knowledge";
    private const int VectorSize = 3072; // Gemini 1.5 Embedding Size

    public QdrantVectorStore(ILogger<QdrantVectorStore> logger)
    {
        _logger = logger;
        // Connect to Docker container
        _client = new QdrantClient(new Uri("http://localhost:6334"));
    }

    private async Task EnsureCollectionExists()
    {
        var collections = await _client.ListCollectionsAsync();
        if (!collections.Contains(CollectionName))
        {
            _logger.LogInformation("Creating Qdrant Collection {Name}...", CollectionName);

            // 1. Create the Collection
            await _client.CreateCollectionAsync(CollectionName,
                new VectorParams { Size = VectorSize, Distance = Distance.Cosine });

            // 2. CRITICAL: Create Indices for Speed
            // This allows us to filter by Category or DocId instantly, even with 1M+ chunks.
            await _client.CreatePayloadIndexAsync(CollectionName, "category", PayloadSchemaType.Keyword);
            await _client.CreatePayloadIndexAsync(CollectionName, "docId", PayloadSchemaType.Keyword);
            await _client.CreatePayloadIndexAsync(CollectionName, "source", PayloadSchemaType.Keyword);
        }
    }

    public async Task SaveChunksAsync(List<KnowledgeChunk> chunks)
    {
        if (chunks.Count == 0) return;

        await EnsureCollectionExists();

        var points = new List<PointStruct>();

        foreach (var chunk in chunks)
        {
            // Map our Clean Model -> Qdrant Internal Model
            var point = new PointStruct
            {
                Id = (PointId)Guid.NewGuid(),
                Vectors = chunk.Embedding,
                Payload =
                {
                    ["content"] = chunk.Content,
                    ["source"] = chunk.SourceFileName,
                    ["category"] = chunk.Category ?? "General",
                    ["docId"] = chunk.DocumentId,
                    ["chunkIndex"] = chunk.ChunkIndex,
                    ["page"] = chunk.PageNumber.HasValue ? chunk.PageNumber.Value : 0
                }
            };
            points.Add(point);
        }

        // 3. Bulk Upsert (Performance optimization for Books)
        // If we have > 100 points, we send them all in one network request.
        await _client.UpsertAsync(CollectionName, points);

        _logger.LogInformation("Indexed {Count} chunks. Category: {Cat}", chunks.Count, chunks[0].Category);
    }

    public async Task<List<KnowledgeChunk>> SearchAsync(float[] queryVector, string? categoryFilter = null, int limit = 5)
    {
        await EnsureCollectionExists();

        // 4. Construct Filter
        Filter? filter = null;
        if (!string.IsNullOrEmpty(categoryFilter))
        {
            filter = new Filter
            {
                Must = { new Condition { Field = new FieldCondition { Key = "category", Match = new Qdrant.Client.Grpc.Match { Keyword = categoryFilter } } } }
            };
        }

        // 5. Execute Search
        var results = await _client.SearchAsync(
            CollectionName,
            queryVector,
            filter: filter,
            limit: (ulong)limit
        );

        // 6. Map back to Domain Model
        return results.Select(hit => new KnowledgeChunk
        {
            Id = hit.Id.ToString(),
            Content = hit.Payload["content"].StringValue,
            SourceFileName = hit.Payload["source"].StringValue,
            Category = hit.Payload["category"].StringValue,
            DocumentId = hit.Payload["docId"].StringValue,
            // Score = hit.Score // (Useful for debugging relevance)
        }).ToList();
    }

    public async Task DeleteDocumentAsync(string documentId)
    {
        await EnsureCollectionExists();

        // Delete everything belonging to this specific Book/File
        var filter = new Filter
        {
            Must = { new Condition { Field = new FieldCondition { Key = "docId", Match = new Qdrant.Client.Grpc.Match { Keyword = documentId } } } }
        };

        await _client.DeleteAsync(CollectionName, filter);
        _logger.LogInformation("Deleted document {DocId} from Knowledge Base", documentId);
    }
}