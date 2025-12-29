using AIPlatform.Core.Interfaces;
using AIPlatform.Infrastructure.Data;
using System.Xml.Linq;

namespace AIPlatform.API.Extensions;

public static class RepositoryServiceExtensions
{
    public static IServiceCollection AddToolRepository(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"]; // "Mongo", "SQL", "Oracle"
        var connectionString = config["Database:ConnectionString"];
        var dbName = config["Database:Name"] ?? "ai_platform";

        switch (provider?.ToUpper())
        {
            case "MONGO":
                services.AddScoped<IToolRepository>(sp =>
                     new MongoToolRepository(connectionString, dbName));

                // Also register the concrete class just in case
                services.AddScoped<MongoToolRepository>(sp =>
                     new MongoToolRepository(connectionString, dbName));
                break;
            case "SQL":
            case "ORACLE":
                services.AddScoped<IToolRepository>(sp =>
                    new SqlToolRepository(connectionString));
                break;

            default:
                throw new Exception($"Unsupported Database Provider: {provider}");
        }

        return services;
    }
}