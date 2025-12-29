using AIPlatform.Core.Enums;

namespace AIPlatform.Core.Interfaces;

public interface IAIServiceFactory
{
    // The "Switch" - give me a provider type, I give you the service
    IAIService GetService(AIProvider provider);

    // Helper to get the default one configured in appsettings
    IAIService GetDefaultService();
}