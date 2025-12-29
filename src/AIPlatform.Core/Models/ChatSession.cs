using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AIPlatform.Core.Models;

public class ChatSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } // Mongo internal ID

    public string SessionId { get; set; } // Your friendly ID (e.g., "session-1")
    public string UserId { get; set; }
    public string Title { get; set; } // e.g., "Budget Review"
    public DateTime LastUpdated { get; set; }

    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } // "user" or "assistant"
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
}