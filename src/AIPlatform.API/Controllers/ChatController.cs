using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAIServiceFactory _aiFactory;
    private readonly IChatRepository _repo;
    private readonly IVectorStore _vectorStore; // We need this for RAG
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IAIServiceFactory aiFactory,
        IChatRepository repo,
        IVectorStore vectorStore,
        ILogger<ChatController> logger)
    {
        _aiFactory = aiFactory;
        _repo = repo;
        _vectorStore = vectorStore;
        _logger = logger;
    }


    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] StandardChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserMessage)) return BadRequest();

        // 1. Save User Message
        await _repo.AddMessageAsync(request.SessionId, request.UserId, "user", request.UserMessage);

        // 2. Fetch History
        request.History = await _repo.GetHistoryAsync(request.SessionId);

        // 3. Call AI (The Agent Logic is inside here now)
        var aiService = _aiFactory.GetDefaultService();
        var response = await aiService.GenerateResponseAsync(request);

        // 4. Save Response
        if (response.IsSuccess)
        {
            await _repo.AddMessageAsync(request.SessionId, request.UserId, "assistant", response.Content);
        }

        return Ok(response);
    }

    [HttpGet("history/{sessionId}")]
    public async Task<IActionResult> GetHistory(string sessionId)
    {
        var messages = await _repo.GetHistoryAsync(sessionId);
        return Ok(messages);
    }
}