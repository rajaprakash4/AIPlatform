using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Services;

public class PlannerService
{
    private readonly IToolRepository _repo;
    private readonly IAIServiceFactory _aiFactory;

    public PlannerService(IToolRepository repo, IAIServiceFactory aiFactory)
    {
        _repo = repo;
        _aiFactory = aiFactory;
    }

    public async Task<ExecutionPlanResponse> GeneratePlanAsync(string userRequest, List<string>? restrictedToIds = null)
    {
        // 1. Get Inventory
        var allTools = await _repo.GetAllToolsAsync();
        var allWorkflows = await _repo.GetAllWorkflowsAsync();
        // 2. FILTER Inventory (The Logic Change)
        if (restrictedToIds != null && restrictedToIds.Any())
        {
            allTools = allTools.Where(t => restrictedToIds.Contains(t.Id)).ToList();
            allWorkflows = allWorkflows.Where(w => restrictedToIds.Contains(w.Id)).ToList();
        }

        // 2. Serialize Inventory for the Brain
        var inventory = new
        {
            Tools = allTools.Select(t => new { t.Id, t.Type, t.Description, t.InputKeys }),
            Agents = allWorkflows.Select(a => new { Id = a.Id, Type = "Agent", a.Description })
        };

        var prompt = $@"
You are an AI Architect. Create a sequential execution plan.
Inventory: {JsonSerializer.Serialize(inventory)}

User Request: ""{userRequest}""

Rules:
1. Break the request into logical steps using the available tools.
2. **Input Extraction Logic:**
   - **IF** the User Request contains the specific data needed for an input (e.g., ID '1', Name 'John'), **EXTRACT** it and set it as the value.
   - **ELSE** (if the data is missing), set the value STRICTLY to '{{{{USER_INPUT}}}}'.
   - **DO NOT** guess or hallucinate values (e.g., do not invent '123' if the user didn't say it).

3. **Chaining:**
   - If a tool needs output from a previous step, use '{{{{StepX.Output.Key}}}}'.

Examples:
- Case A (Data Present):
  User: ""Get details for ID 55""
  Tool Input: {{ ""EmployeeId"": ""55"" }}

- Case B (Data Missing):
  User: ""Get details for an employee""
  Tool Input: {{ ""EmployeeId"": ""{{{{USER_INPUT}}}}"" }}

Output JSON format:
{{
  ""Goal"": ""Brief summary"",
  ""Steps"": [
    {{ ""StepId"": 1, ""ToolId"": ""..."", ""Description"": ""..."", ""InputMapping"": {{...}} }}
  ]
}}";

        // 4. Call Gemini
        var aiService = _aiFactory.GetDefaultService();
        var result = await aiService.GenerateResponseAsync(new StandardChatRequest { UserMessage = prompt });

        // 5. Parse
        var cleanJson = result.Content.Replace("```json", "").Replace("```", "").Trim();
        return JsonSerializer.Deserialize<ExecutionPlanResponse>(cleanJson);
    }
}