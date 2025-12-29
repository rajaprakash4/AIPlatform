using AIPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly InputValidationService _validator;

    public ValidationController(InputValidationService validator)
    {
        _validator = validator;
    }

    public class ValidationRequest
    {
        public string ToolId { get; set; }
        public string InputKey { get; set; }
        public string InputValue { get; set; }
        public string Description { get; set; } // Helps AI understand context
    }

    [HttpPost]
    public async Task<IActionResult> Validate([FromBody] ValidationRequest request)
    {
        var result = await _validator.ValidateInputAsync(
            request.ToolId,
            request.InputKey,
            request.InputValue,
            request.Description
        );
        return Ok(result);
    }
}