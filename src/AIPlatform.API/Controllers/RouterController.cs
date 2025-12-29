using AIPlatform.Core.Models;
using AIPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class RouterController : ControllerBase
{
    private readonly PlannerService _planner;

    public RouterController(PlannerService planner)
    {
        _planner = planner;
    }

    [HttpPost("plan")]
    public async Task<IActionResult> CreatePlan([FromBody] StandardChatRequest request)
    {
        var plan = await _planner.GeneratePlanAsync(request.UserMessage,request.AllowedIds);
        return Ok(plan);
    }
}