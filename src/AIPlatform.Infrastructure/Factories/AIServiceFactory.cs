using AIPlatform.Core.Enums;
using AIPlatform.Core.Interfaces;
using AIPlatform.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIPlatform.Infrastructure.Factories;

public class AIServiceFactory : IAIServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;

    public AIServiceFactory(IServiceProvider serviceProvider, IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _config = config;
    }

    public IAIService GetService(AIProvider provider)
    {
        // We use Keyed Services (a .NET 8 feature) or manual resolution
        // Here we resolve the specific implementation based on the Enum
        return provider switch
        {
            AIProvider.Google => _serviceProvider.GetRequiredService<GeminiService>(),
            AIProvider.OpenAI => _serviceProvider.GetRequiredService<OpenAIService>(),
            _ => throw new ArgumentException("Invalid AI Provider requested")
        };
    }

    public IAIService GetDefaultService()
    {
        // Read "ActiveProvider" from appsettings.json
        var defaultProviderStr = _config["AI:ActiveProvider"];
        if (Enum.TryParse(defaultProviderStr, out AIProvider provider))
        {
            return GetService(provider);
        }

        // Fallback
        return GetService(AIProvider.Google);
    }
}