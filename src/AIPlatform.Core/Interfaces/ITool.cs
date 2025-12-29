namespace AIPlatform.Core.Interfaces;

public interface ITool
{
    // The name the AI sees (e.g., "search_manuals")
    string Name { get; }

    // The description telling the AI WHEN to use it
    string Description { get; }

    // The logic to execute
    Task<string> ExecuteAsync(string arguments);
}