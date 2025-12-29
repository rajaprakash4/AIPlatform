using AIPlatform.Core.Models;

namespace AIPlatform.Core.Interfaces;

public interface IAgentOrchestrator
{
    // The main entry point for the API
    Task<StandardChatResponse> ProcessRequestAsync(StandardChatRequest request);
}