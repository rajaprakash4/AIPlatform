using AIPlatform.Core.Models;

namespace AIPlatform.Core.Interfaces;

public interface IToolRepository
{
    Task<ToolDefinition?> GetToolAsync(string toolId);
    Task<AgentWorkflow?> GetWorkflowAsync(string workflowId);

    Task<List<ToolDefinition>> GetAllToolsAsync();
    Task<List<AgentWorkflow>> GetAllWorkflowsAsync();

    // Optional: Methods for Admin UI to create tools
    Task SaveToolAsync(ToolDefinition tool);
    Task SaveWorkflowAsync(AgentWorkflow workflow);
}