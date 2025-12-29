using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;

namespace AIPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IAIServiceFactory _aiFactory;
    private readonly IVectorStore _vectorStore;
    private readonly ITextChunker _chunker;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IAIServiceFactory aiFactory,
        IVectorStore vectorStore,
        ITextChunker chunker,
        ILogger<DocumentController> logger)
    {
        _aiFactory = aiFactory;
        _vectorStore = vectorStore;
        _chunker = chunker;
        _logger = logger;
    }

    // UPDATED: Accepts 'category' as a query parameter (default is "General")
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] string storeName = "General")
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        // Optional: Enforce valid categories if you want strict control
        // var validCategories = new[] { "HR", "Technical", "Medical", "General" };
        // if (!validCategories.Contains(category)) return BadRequest("Invalid Category");

        string fileName = file.FileName;
        string docId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Ingesting {File} into Category: {Category}", fileName, storeName);

            // 1. Extract Text
            string rawText = "";
            using (var stream = file.OpenReadStream())
            {
                if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    using var pdf = PdfDocument.Open(stream);
                    rawText = string.Join("\n", pdf.GetPages().Select(p => p.Text));
                }
                else
                {
                    using var reader = new StreamReader(stream);
                    rawText = await reader.ReadToEndAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(rawText)) return BadRequest("Could not extract text.");

            // 2. Chunk Text
            var segments = _chunker.SplitText(rawText);

            // 3. Embed & Tag Chunks
            var aiService = _aiFactory.GetDefaultService();
            var knowledgeChunks = new List<KnowledgeChunk>();

            for (int i = 0; i < segments.Count; i++)
            {
                var vector = await aiService.GenerateEmbeddingAsync(segments[i]);

                knowledgeChunks.Add(new KnowledgeChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    DocumentId = docId,
                    Content = segments[i],
                    SourceFileName = fileName,

                    // CRITICAL: This is where the file gets "locked" to the tool
                    Category = storeName,

                    ChunkIndex = i,
                    Embedding = vector
                });

                // Rate limiting protection
                if (i % 10 == 0) await Task.Delay(50);
            }

            // 4. Save to Qdrant
            await _vectorStore.SaveChunksAsync(knowledgeChunks);

            return Ok(new
            {
                message = "Ingestion successful",
                category = storeName, // Confirm back to user
                chunks = segments.Count,
                docId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion failed");
            return StatusCode(500, ex.Message);
        }
    }
}