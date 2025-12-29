using AIPlatform.Core.Models;

namespace AIPlatform.Core.Interfaces;

public interface IChatRepository
{
    Task AddMessageAsync(string sessionId, string userId, string role, string content);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId);
}