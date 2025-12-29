using Microsoft.AspNetCore.Mvc;

namespace AIPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeController : ControllerBase
{
    private readonly string _storagePath;

    public KnowledgeController(IConfiguration config)
    {
        // Define where to save files. Ensure this folder exists!
        _storagePath = config["KnowledgeStore:Path"] ?? Path.Combine(Directory.GetCurrentDirectory(), "knowledge_store");
        if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var filePath = Path.Combine(_storagePath, file.FileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { message = "File uploaded successfully", fileName = file.FileName });
    }
}