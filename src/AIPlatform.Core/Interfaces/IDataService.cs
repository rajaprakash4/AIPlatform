namespace AIPlatform.Core.Interfaces;

public interface IDataService
{
    string Name { get; }

    // Generic method to execute a tool/API call
    // args: {"city": "New York"}
    Task<string> ExecuteAsync(Dictionary<string, object> args);
}