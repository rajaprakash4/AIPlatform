using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AIPlatform.Infrastructure.Data;

public class MongoToolRepository : IToolRepository
{
    private readonly IMongoCollection<ToolDefinition> _tools;
    private readonly IMongoCollection<AgentWorkflow> _workflows;

    static MongoToolRepository()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(ToolDefinition)))
        {
            BsonClassMap.RegisterClassMap<ToolDefinition>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(c => c.Id);
                cm.SetIgnoreExtraElements(true); // Prevents crashing if DB has extra fields
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(AgentWorkflow)))
        {
            BsonClassMap.RegisterClassMap<AgentWorkflow>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(c => c.Id);
                cm.SetIgnoreExtraElements(true);
            });
        }
    }
    public MongoToolRepository(string connectionString, string dbName)
    {
        // Only connection logic here
        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(dbName);
        _tools = db.GetCollection<ToolDefinition>("tool_definitions");
        _workflows = db.GetCollection<AgentWorkflow>("agent_workflows");
    }

    public async Task<ToolDefinition?> GetToolAsync(string toolId) =>
        await _tools.Find(t => t.Id == toolId).FirstOrDefaultAsync();

    public async Task<AgentWorkflow?> GetWorkflowAsync(string workflowId) =>
        await _workflows.Find(w => w.Id == workflowId).FirstOrDefaultAsync();

    // ✅ NEW: Get All Tools (Optimized to return only needed fields if you want, here returning full obj)
    public async Task<List<ToolDefinition>> GetAllToolsAsync() =>
        await _tools.Find(_ => true).ToListAsync();

    // ✅ NEW: Get All Workflows
    public async Task<List<AgentWorkflow>> GetAllWorkflowsAsync() =>
        await _workflows.Find(_ => true).ToListAsync();

    public async Task SaveToolAsync(ToolDefinition tool) =>
        await _tools.ReplaceOneAsync(t => t.Id == tool.Id, tool, new ReplaceOptions { IsUpsert = true });

    public async Task SaveWorkflowAsync(AgentWorkflow workflow) =>
        await _workflows.ReplaceOneAsync(w => w.Id == workflow.Id, workflow, new ReplaceOptions { IsUpsert = true });
}