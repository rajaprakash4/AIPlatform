using AIPlatform.Core.Interfaces;
using AIPlatform.Core.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace AIPlatform.Infrastructure.Data;

public class MongoChatRepository : IChatRepository
{
    private readonly IMongoCollection<ChatSession> _sessions;

    public MongoChatRepository(IConfiguration config)
    {
        var connectionString = "mongodb://localhost:27017";
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("AIPlatformDB");
        _sessions = database.GetCollection<ChatSession>("ChatSessions");
    }

    public async Task AddMessageAsync(string sessionId, string userId, string role, string content)
    {
        var filter = Builders<ChatSession>.Filter.Eq(s => s.SessionId, sessionId);
        var newMessage = new ChatMessage
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        var update = Builders<ChatSession>.Update
            .Push(s => s.Messages, newMessage)
            .Set(s => s.LastUpdated, DateTime.UtcNow)
            .SetOnInsert(s => s.UserId, userId)
            .SetOnInsert(s => s.Title, "New Chat");

        await _sessions.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true } // Magic Mongo flag: "Create if not exists"
        );
    }

    public async Task<List<ChatMessage>> GetHistoryAsync(string sessionId)
    {
        var filter = Builders<ChatSession>.Filter.Eq(s => s.SessionId, sessionId);
        var session = await _sessions.Find(filter).FirstOrDefaultAsync();
        return session?.Messages ?? new List<ChatMessage>();
    }
}