using AIPlatform.Core.Enums;
using AIPlatform.Core.Interfaces;
using AIPlatform.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIPlatform.Infrastructure.Factories;

public interface IRepositoryFactory
{
    IChatRepository GetRepository();
}

public class RepositoryFactory : IRepositoryFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;

    public RepositoryFactory(IServiceProvider serviceProvider, IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _config = config;
    }

    public IChatRepository GetRepository()
    {
        // Read "DatabaseProvider" from appsettings.json
        var providerStr = _config["Database:Provider"];
        if (!Enum.TryParse(providerStr, out DatabaseProvider provider))
        {
            provider = DatabaseProvider.MongoDB; // Default
        }

        return provider switch
        {
            DatabaseProvider.MongoDB => _serviceProvider.GetRequiredService<MongoChatRepository>(),
            // Case DatabaseProvider.SqlServer => ...
            _ => throw new ArgumentException("Invalid Database Provider")
        };
    }
}