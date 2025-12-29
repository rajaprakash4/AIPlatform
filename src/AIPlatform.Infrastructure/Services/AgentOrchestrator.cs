using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using System.Text.Json;

namespace AIPlatform.Infrastructure.Services;

public class AgentOrchestrator
{
    // 1. Define the dependencies
    private readonly IToolRepository _repository; // Renamed from _registry
    private readonly DynamicToolHandler _toolHandler;

    // 2. Inject them in the constructor
    public AgentOrchestrator(IToolRepository repository, DynamicToolHandler toolHandler)
    {
        _repository = repository; 
        _toolHandler = toolHandler;
    }

    public async Task<WorkflowResult> RunWorkflowAsync(string workflowId, Dictionary<string, object> initialContext)
    {
        var workflow = await _repository.GetWorkflowAsync(workflowId);
        var context = new Dictionary<string, object>(initialContext, StringComparer.OrdinalIgnoreCase);
        var traceLog = new List<StepTrace>(); // <--- The Flight Recorder

        foreach (var toolId in workflow.Steps)
        {
            var tool = await _repository.GetToolAsync(toolId);
            if (tool == null) continue;

            // 1. Capture Input Snapshot
            var toolInputs = new Dictionary<string, object>();
            foreach (var key in tool.InputKeys)
            {
                if (context.TryGetValue(key, out var value)) toolInputs[key] = value;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 2. Execute
            var outputs = await _toolHandler.ExecuteAsync(tool, toolInputs);

            stopwatch.Stop();

            // 3. Record Trace
            traceLog.Add(new StepTrace
            {
                ToolId = toolId,
                Input = toolInputs,   // What went IN
                Output = outputs,     // What came OUT
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                Success = true
            });

            // 4. Update Blackboard Context
            foreach (var kvp in outputs)
            {
                string finalKey = tool.OutputAlias.ContainsKey(kvp.Key) ? tool.OutputAlias[kvp.Key] : kvp.Key;
                context[finalKey] = kvp.Value;
            }
        }

        return new WorkflowResult { FinalContext = context, Trace = traceLog };
    }
}