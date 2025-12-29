namespace AIPlatform.Core.Models;

public class KnowledgeChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // The core data
    public string Content { get; set; }        // The actual text segment
    public float[] Embedding { get; set; }     // The math representation

    // Metadata for UI (Citations) & Filtering
    public string DocumentId { get; set; }     // e.g., "Doc-101"
    public string SourceFileName { get; set; } // e.g., "Pump_Manual.pdf"
    public string Category { get; set; }       // e.g., "Technical", "HR"
    public int ChunkIndex { get; set; }        // e.g., 0, 1, 2 (Preserves reading order)

    // Optional: If we extract this, it's gold for the UI
    public int? PageNumber { get; set; }
}