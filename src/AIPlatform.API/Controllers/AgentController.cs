using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using AIPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IToolRepository _repository;
    private readonly AgentOrchestrator _orchestrator;
    private readonly DynamicToolHandler _toolHandler;
    private readonly ILogger<AgentController> _logger;

    // We inject all 3 services:
    // 1. Repo: To fetch lists for the UI.
    // 2. Orchestrator: To run complex Agents (with Trace).
    // 3. ToolHandler: To run simple atomic Tools.
    public AgentController(
        IToolRepository repository,
        AgentOrchestrator orchestrator,
        DynamicToolHandler toolHandler,
        ILogger<AgentController> logger)
    {
        _repository = repository;
        _orchestrator = orchestrator;
        _toolHandler = toolHandler;
        _logger = logger;
    }

    // --- 1. METADATA ENDPOINTS (For UI Dropdowns) ---

    [HttpGet("tools")]
    public async Task<IActionResult> GetAllTools()
    {
        var tools = await _repository.GetAllToolsAsync();
        // Return only what the UI needs (Id and Description)
        return Ok(tools.Select(t => new { t.Id, t.Description, t.InputKeys }));
    }

    [HttpGet("workflows")]
    public async Task<IActionResult> GetAllWorkflows()
    {
        var workflows = await _repository.GetAllWorkflowsAsync();
        return Ok(workflows.Select(w => new { w.Id, w.Description, w.Steps }));
    }

    // --- 2. ATOMIC TOOL EXECUTION (For "Tools" Tab) ---

    [HttpPost("tool/run/{toolId}")]
    public async Task<IActionResult> RunSingleTool(string toolId, [FromBody] Dictionary<string, object> input)
    {
        try
        {
            _logger.LogInformation("Executing Single Tool: {ToolId}", toolId);

            // 1. Fetch Definition
            var tool = await _repository.GetToolAsync(toolId);
            if (tool == null) return NotFound($"Tool '{toolId}' not found.");

            // 2. Validate Inputs (Optional but recommended)
            var missingKeys = tool.InputKeys.Where(k => !input.ContainsKey(k)).ToList();
            if (missingKeys.Any())
            {
                return BadRequest($"Missing required inputs: {string.Join(", ", missingKeys)}");
            }

            // 3. Execute directly via Handler (No Blackboard, No Trace needed)
            var result = await _toolHandler.ExecuteAsync(tool, input);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool Execution Failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // --- 3. AGENT EXECUTION (For "Agents" Tab) ---

    [HttpPost("run/{workflowId}")]
    public async Task<IActionResult> RunAgent(string workflowId, [FromBody] Dictionary<string, object> initialContext)
    {
        try
        {
            _logger.LogInformation("Starting Workflow: {WorkflowId}", workflowId);

            // The Orchestrator returns a 'WorkflowResult' containing the Trace + Final Context
            var result = await _orchestrator.RunWorkflowAsync(workflowId, initialContext);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow Failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}